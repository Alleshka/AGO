namespace Ago.Core.Agents.CodeReview
{
    /// <summary>
    /// Reviews code style: naming, formatting, readability, C# conventions.
    /// Returns structured findings with line numbers and proposed fixes.
    /// </summary>
    internal class StyleReviewAgent : LlmAgentBase
    {
        public override string Id => AgoConstants.AgentIds.StyleReview;

        public StyleReviewAgent(LlmProviderFactory factory, PromptResolver promptResolver) : base(factory, promptResolver) { }

        protected override string BuildSystemPrompt(AnalysisContext context)
        {
            return $"""
            You are an expert C# code reviewer focused on code style.
            Your task is to find real style violations — not performance issues, not security concerns.
            Only report issues that affect readability and maintainability.

            Look for:
            - Naming violations: PascalCase for types/methods, camelCase for locals/parameters
            - Unnecessary complexity: nested ternaries, overly long methods, deep nesting
            - Non-idiomatic C#: missing expression bodies, pattern matching opportunities, var misuse
            - Missing XML documentation on public members
            - Dead code: commented-out code, unused variables, unreachable branches

            Do NOT report:
            - Performance issues
            - Security vulnerabilities
            - Formatting that is auto-fixed by .editorconfig or Roslyn analyzers

            Respond ONLY with a JSON array. No prose, no markdown fences.
            Each item must have this exact shape:
            {schema}

            Priority rules:
            - High: violations that cause confusion or bugs — misleading names, shadowed variables
            - Medium: clear style violations — wrong casing, missing docs on public API
            - Low: minor improvements — expression body opportunity, minor simplification

            If there are no issues, respond with an empty array: []
            """;
        }
    }
}
