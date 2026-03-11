using Ago.Core.LLM;

namespace Ago.Core.Agents.Explainer
{
    /// <summary>
    /// Explains code in plain language.
    /// Supports: diff, file, class, method, or raw snippet.
    /// Returns free-form text in AgentResult.Explanation — no Findings.
    /// </summary>
    public class ExplainerAgent : LlmAgentBase
    {
        public override string Id => AgoConstants.AgentIds.Explainer;
        protected override bool UseFileChanking => false;

        public ExplainerAgent(LlmProviderFactory factory, PromptResolver promptResolver) : base(factory, promptResolver) { }

        public override async Task<AgentResult> AnalyseAsync(AnalysisContext context, CancellationToken ct = default)
        {
            if (context.Path is not null && Directory.Exists(context.Path))
            {
                return await ExplainDirectoryAsync(context, ct);
            }

            return await base.AnalyseAsync(context, ct);
        }

        private async Task<AgentResult> ExplainDirectoryAsync(AnalysisContext context, CancellationToken ct)
        {
            var files = Directory.GetFiles(context.Path!, "*", SearchOption.AllDirectories);

            var summaries = await Task.WhenAll(files.Select(async file =>
            {
                var fileContext = context with { Path = file };
                var result = await base.AnalyseAsync(fileContext, ct);
                return $"### {Path.GetFileName(file)}\n{result.Explanation}";
            }));

            var finalMessages = new[]
            {
                ChatMessage.System("You are an expert C# developer and teacher"),
                ChatMessage.User($"""
                    Here are summaries of individual files in a directory.
                    Explain how they work together — the overall architecture,
                    responsibilities, and relationships between components.
                    {string.Join("\n\n", summaries)}
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

        protected override IReadOnlyList<ChatMessage> BuildPrompt(AnalysisContext context, PromptResolver promptResolver)
        {
            var (subject, code) = ResolveSubject(context);

            var system = promptResolver.Resolve(this.Id, context.ProjectRoot) ?? $"""
            You are an expert C# developer and teacher.
            Explain the provided {subject} clearly and concisely.

            Structure your explanation as:
            1. **Purpose** — what this code does in one sentence
            2. **How it works** — step by step, plain English
            3. **Key concepts** — any patterns, algorithms, or C# features worth noting
            4. **Potential concerns** — anything that looks risky or worth reviewing (if any)

            Write for an audience of intermediate developers.
            Be direct. Avoid filler phrases.
            """;

            return [
                ChatMessage.System(system),
                ChatMessage.User($"Explain this {subject}:\n\n{code}"),
            ];
        }

        private static (string subject, string code) ResolveSubject(AnalysisContext context)
        {
            if (context.Diff is not null)
            {
                return ("diff", FormatDiffForPrompt(context.Diff));
            }

            if (context.RawCode is not null)
            {
                var subject = context.ClassName is not null
                    ? $"class {context.ClassName}"
                    : "code snippet";
                return (subject, context.RawCode);
            }

            if (context.Path is not null)
            {
                var code = File.ReadAllText(context.Path);
                return ($"file {Path.GetFileName(context.Path)}", code);
            }

            throw new InvalidOperationException(
                $"{AgoConstants.AgentIds.Explainer}: context must have Diff, RawCode, or FilePath.");
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
    }

}
