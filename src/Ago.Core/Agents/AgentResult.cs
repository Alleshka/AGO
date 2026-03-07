using Ago.Core.Agents;

namespace Ago.Core
{
    public record AgentResult
    {
        public required string AgentId { get; init; }
        public required bool Success { get; init; }

        public IReadOnlyList<Finding> Findings { get; init; } = [];

        // For agents that produce free-form text (ExplainerAgent, DocWriterAgent)
        public string? Explanation { get; init; }

        public string? Error { get; init; }

        public bool HasFindings => Findings.Count > 0;
    }
}
