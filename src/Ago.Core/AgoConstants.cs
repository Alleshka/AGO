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

        public static class Defaults
        {
            public const string OllamaBaseUrl = "http://localhost:11434";
            public const string OllamaModel = "qwen2.5-coder:7b";
            public const string LlmProvider = "ollama";
        }
    }
}
