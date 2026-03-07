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

        public ExplainerAgent(LlmProviderFactory factory) : base(factory) { }

        protected override IReadOnlyList<ChatMessage> BuildPrompt(AnalysisContext context)
        {
            var (subject, code) = ResolveSubject(context);

            var system = $"""
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

            if (context.FilePath is not null)
            {
                var code = File.ReadAllText(context.FilePath);
                return ($"file {Path.GetFileName(context.FilePath)}", code);
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
