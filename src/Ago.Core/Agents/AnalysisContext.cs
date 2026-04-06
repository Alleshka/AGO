using Ago.Core.Config;
using Ago.Core.Orchestrator;

namespace Ago.Core.Agents
{
    /// <summary>
    /// Context — what every agent receives
    /// </summary>
    public record AnalysisContext
    {
        public required string ProjectRoot { get; init; }
        public required AgoConfig Config { get; init; }

        public IReadOnlyDictionary<string, string> Files { get; init; } = new Dictionary<string, string>();

        public RunScope Scope { get; init; }
        public string? Path { get; init; }

        // public ICodeIndex? Index { get; init; } // TODO: for now, agents can create their own index if they need it. In the future, we can create a shared index and pass it in the context.
    }
}
