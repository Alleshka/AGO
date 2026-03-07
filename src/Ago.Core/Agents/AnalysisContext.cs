using Ago.Core.Config;
using Ago.Core.Git.Diff;

namespace Ago.Core.Agents
{
    /// <summary>
    /// Context — what every agent receives
    /// </summary>
    public record AnalysisContext
    {
        public required string ProjectRoot { get; init; }
        public required AgoConfig Config { get; init; }

        public DiffResult? Diff { get; init; }
        public string? FilePath { get; init; }
        public string? ClassName { get; init; }
        public string? RawCode { get; init; }

        // public ICodeIndex? Index { get; init; } // TODO: for now, agents can create their own index if they need it. In the future, we can create a shared index and pass it in the context.
    }
}
