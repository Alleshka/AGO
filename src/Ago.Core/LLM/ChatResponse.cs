namespace Ago.Core.LLM
{
    public record ChatResponse
    {
        public required string Content { get; init; }
        public string? Model { get; init; }
        public TokenUsage? Usage { get; init; }
    }
}
