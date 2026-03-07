namespace Ago.Core.LLM
{
    public record ChatMessage(string Role, string Content)
    {
        public static ChatMessage System(string content) => new("system", content);
        public static ChatMessage User(string content) => new("user", content);
        public static ChatMessage Assistant(string content) => new("assistant", content);
    }
}
