using Ago.Core.Config;
using Ago.Core.LLM;

namespace Ago.Core
{
    public class LlmProviderFactory
    {
        private readonly AgoConfig _config;

        public LlmProviderFactory(AgoConfig config)
        {
            _config = config;
        }

        public IChatClient GetForAgent(string agentId)
        {
            var agent = _config.Agents.TryGetValue(agentId, out var a) ? a : null;
            var agentProvider = a?.Provider;
            var providerName = agentProvider ?? _config.Llm.Default;
            return Create(providerName);
        }

        private IChatClient Create(string providerName)
        {
            var cfg = _config.Llm.Providers[providerName];

            return providerName switch
            {
                "ollama" => new OllamaClient(cfg.Model, cfg.BaseUrl),
                _ => throw new InvalidOperationException($"Unknown provider: {providerName}")
            };
        }
    }
}
