using Ago.Core.Agents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ago.Core.Orchestrator
{
    public class OrchestratorResult
    {
        public bool Success { get; init; }
        public IReadOnlyList<Finding> Findings { get; init; } = [];
        public IReadOnlyList<AgentExplanation> Explanations { get; init; } = [];
        public IReadOnlyList<AgentResult> AgentResults { get; init; } = [];
        public TimeSpan Elapsed { get; init; }
        public int FindingCount => Findings.Count;
        public bool HasFindings => Findings.Count > 0;
        public IEnumerable<Finding> HighPriority =>
            Findings.Where(f => f.Priority == Priority.High);
    }

    public record AgentExplanation(string AgentId, string Text);
}
