namespace Ago.Core.Orchestrator
{
    public class RunOptions
    {
        public ICollection<string> Agents { get; set; }

        public string? Preset { get; set; }

        public bool Fix { get; set; }
        public bool DryRun { get; set; }

        public RunScope Scope { get; set; } = RunScope.Diff;
        public string? FilePath { get; set; }
        public string? ClassName { get; set; }

        // --- Optional explicit project root (from -C flag) ---
        public string? ProjectRoot { get; set; }

        public RunOptions()
        {
            Agents = new HashSet<string>();
        }

        public void AddAgent(string agentId) => Agents.Add(agentId);
        public void AddAgents(params string[] agentsId)
        {
            foreach (var agent in agentsId)
            {
                AddAgent(agent);
            }
        }
        public void HasAgent(string agentId) => Agents.Contains(agentId);
    }

    public enum RunScope { Diff, File, Class, All }
}
