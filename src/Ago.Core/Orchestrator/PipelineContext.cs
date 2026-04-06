using Ago.Core.Agents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ago.Core.Orchestrator
{
    /// <summary>
    /// Lives for the duration of one RunAsync call.
    /// Accumulates agent results between pipeline phases so that
    /// later agents (MergeAgent, PlannerAgent) can read findings
    /// produced by earlier agents.
    /// </summary>
    public class PipelineContext
    {
        public required AnalysisContext Analysis { get; init; }

        public IReadOnlyList<Finding> Findings { get; private set; } = [];
        public IReadOnlyList<AgentResult> AgentResults { get; private set; } = [];

        public void AddResults(IEnumerable<AgentResult> results)
        {
            var newResults = AgentResults.Concat(results).ToList();
            AgentResults = newResults;
            Findings = newResults
                .Where(r => r.Success)
                .SelectMany(r => r.Findings)
                .ToList();
        }
    }
}
