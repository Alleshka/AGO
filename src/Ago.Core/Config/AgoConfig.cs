namespace Ago.Core.Config
{
    public class AgoConfig
    {
        public string Version { get; set; } = "1.0.0";
        public ProjectConfig Project { get; set; } = new();
        public Dictionary<string, AgentConfig> Agents { get; set; } = new();
        public LlmConfig Llm { get; set; } = new();
        public List<string> Ignore { get; set; } = new();
        public Dictionary<string, List<string>> Presets { get; set; } = new();
    }
}
