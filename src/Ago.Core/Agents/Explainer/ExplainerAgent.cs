using Ago.Core.LLM;

namespace Ago.Core.Agents.Explainer
{
    /// <summary>
    /// Explains code in plain language.
    /// Supports: diff, file, class, method, or raw snippet.
    /// Returns free-form text in AgentResult.Explanation — no Findings.
    /// </summary>
    internal class ExplainerAgent : LlmAgentBase
    {
        public override string Id => AgoConstants.AgentIds.Explainer;
        public override AgentScope AgentScope => AgentScope.FileSet;

        public ExplainerAgent(LlmProviderFactory factory, PromptResolver promptResolver) : base(factory, promptResolver) { }

        public override async Task<AgentResult> AnalyseAsync(AnalysisContext context, CancellationToken ct = default)
        {
            if (context.Files.Count == 0)
                return new AgentResult
                {
                    AgentId = Id,
                    Success = false,
                    Error = "No files to explain."
                };

            if (context.Files.Count <= 1)
            {
                return await base.AnalyseSingleAsync(context, ct);
            }

            return await ExplainFilesAsync(context, ct);
        }

        // TODO: directory explanation does not go through PromptResolver
        private async Task<AgentResult> ExplainFilesAsync(AnalysisContext context, CancellationToken ct)
        {
            var summaries = await Task.WhenAll(
                    context.Files.Select(async kv =>
                    {
                        var (path, content) = kv;
                        var fileContext = context with
                        {
                            Files = new Dictionary<string, string> { [path] = content },
                        };
                        var result = await AnalyseSingleAsync(fileContext, ct);
                        return $"### {Path.GetFileName(path)}\n{result.Explanation}";
                    }));


            var finalMessages = new[]
            {
                ChatMessage.System("""
                    You are an expert C# developer and teacher.                  
                    Explain how the provided components work together —
                    overall architecture, responsibilities, relationships
                    """),
                ChatMessage.User($"""
                    Here are summaries of {context.Files.Count} files in a directory.
                    {string.Join("\n\n", summaries)}
                    Explain how they work together        
                    """)
            };

            var llm = _factory.GetForAgent(Id);
            var response = await llm.SendAsync(finalMessages, ct);

            return new AgentResult
            {
                AgentId = Id,
                Success = true,
                Explanation = response.Content
            };
        }
        protected override AgentResult ParseResponse(string rawResponse, AnalysisContext context)
        {
            return new AgentResult
            {
                AgentId = Id,
                Success = true,
                Explanation = rawResponse.Trim(),
                // No Findings — ExplainerAgent only explains, never flags issues
            };
        }

        protected override string BuildSystemPrompt(AnalysisContext context)
        {
            return $"""
            You are an expert C# developer and teacher.
            Explain the provided {(context.Scope == Orchestrator.RunScope.Diff ? "diff" : "code")} clearly and concisely.

            Structure your explanation as:
            1. **Purpose** — what this code does in one sentence
            2. **How it works** — step by step, plain English
            3. **Key concepts** — any patterns, algorithms, or C# features worth noting
            4. **Potential concerns** — anything that looks risky or worth reviewing (if any)

            Write for an audience of intermediate developers.
            Be direct. Avoid filler phrases.
            """;
        }
    }

}
