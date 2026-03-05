namespace Ago.Core.Config
{
    public class LlmProviderConfig
    {
        public string Model { get; set; } = string.Empty;
        public string? ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
    }
}
