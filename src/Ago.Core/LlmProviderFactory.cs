using Ago.Core.Config;
using Ago.Core.LLM;

namespace Ago.Core
{
    public class LlmProviderFactory
    {
        private readonly AgoConfig _config;
        private readonly Dictionary<string, IChatClient> cache = new();

        public LlmProviderFactory(AgoConfig config)
        {
            _config = config;
        }

        public IChatClient GetForAgent(string agentId)
        {
            var agent = _config.Agents.TryGetValue(agentId, out var a) ? a : null;
            var agentProvider = a?.Provider;
            var providerName = agentProvider ?? _config.Llm.Default;

            if (!cache.TryGetValue(providerName, out var client))
            {
                client = Create(providerName);
                cache[providerName] = client;
            }

            return client;
        }

        private IChatClient Create(string providerName)
        {
            var cfg = _config.Llm.Providers[providerName];

            return providerName switch
            {
                AgoConstants.ModelNames.Ollama => new OllamaClient(cfg.Model, cfg.BaseUrl),
                AgoConstants.ModelNames.Anthropic => new AnthropicClient(cfg),
                _ => throw new InvalidOperationException($"Unknown provider: {providerName}")
            };
        }
    }
}
