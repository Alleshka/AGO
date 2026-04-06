using Ago.Core.LLM;

namespace Ago.Core.Agents.CodeReview
{
    internal class PerformanceReviewAgent : LlmAgentBase
    {
        public PerformanceReviewAgent(LlmProviderFactory factory, PromptResolver promptResolver) : base(factory, promptResolver)
        {
        }

        public override string Id => AgoConstants.AgentIds.PerformanceReview;

        protected override string BuildSystemPrompt(AnalysisContext context)
        {
            return $"""
            You are an expert C# performance reviewer.
            Your task is to find real performance problems — not style issues, not theoretical concerns.
            Only report issues that have measurable impact in production.

            Look for:
            - Blocking async code: .Result, .Wait(), Task.GetAwaiter().GetResult()
            - Missing CancellationToken propagation in async methods
            - Allocations in hot paths: closures in loops, string concatenation with +, boxing
            - Inefficient LINQ: multiple enumeration of IEnumerable, Count() instead of Any()
            - Wrong collection type: List<T> for lookup-heavy code (use Dictionary or HashSet)
            - N+1 patterns: database or API calls inside loops
            - Missing StringBuilder for string building in loops
            - Synchronous I/O: File.ReadAllText, WebClient instead of async alternatives
            - Large object allocations that could be pooled or reused

            Do NOT report:
            - Minor style issues
            - Theoretical micro-optimizations without clear impact
            - Issues that require profiler data to confirm

            Respond ONLY with a JSON array. No prose, no markdown fences.
            Each item must have this exact shape: {schema}

            Priority rules:
            - High: blocks threads, causes OOM, N+1 in loops
            - Medium: unnecessary allocations, wrong collection, multiple enumeration
            - Low: minor inefficiency with small impact

            If there are no issues, respond with an empty array: []
            """;
        }
    }
}
