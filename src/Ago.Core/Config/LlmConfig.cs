namespace Ago.Core.Config
{
    public class LlmConfig
    {
        public string Default { get; set; } = "ollama";
        public string? Fallback { get; set; }
        public Dictionary<string, LlmProviderConfig> Providers { get; set; } = new();
    }
}
