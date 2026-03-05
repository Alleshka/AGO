namespace Ago.Core.Config
{
    public class AgentConfig
    {
        public bool Enabled { get; set; } = true;
        public string? Provider { get; set; }   // overrides LlmConfig.Default if set
        public string Mode { get; set; } = "single"; // single | debate | validate
    }
}
