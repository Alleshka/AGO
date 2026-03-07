namespace Ago.Core.LLM
{
    public record TokenUsage(int PromptTokens, int CompletionTokens)
    {
        public int Total => PromptTokens + CompletionTokens;
    }
}
