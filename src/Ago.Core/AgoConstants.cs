namespace Ago.Core
{
    public static class AgoConstants
    {
        public const string ConfigFileName = ".ago.yml";
        public const string AgoFolderName = ".ago";
        public const string HistoryFileName = "history.jsonl";
        public const string IndexFileName = "index.json";

        public static class AgentIds
        {
            public const string StyleReview = "style-review";
            public const string PerformanceReview = "performance-review";
            public const string SecurityReview = "security-review";
            public const string TestGeneration = "test-generation";
            public const string DocWriter = "doc-writer";
            public const string Explainer = "explainer";
        }

        public static class DefaultsProviderConfigs
        {
            public static class OllamaProviderConfig
            {
                public const string BaseUrl = "http://localhost:11434";
                public const string Model = "qwen2.5-coder:7b";
            }

            public static class AnthropicProviderConfig
            {
                public const string Model = "claude-sonnet-4-5";
            }

            public static class OpenRouterConfig
            {
                public const string Model = "qwen/qwen3-coder-480b:free";
            }
        }

        public static class ModelNames
        {
            public const string Ollama = "ollama";
            public const string Anthropic = "anthropic";
            public const string OpenRouter = "openrouter";
        }
    }
}
