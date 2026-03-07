using Ago.Core.LLM;
using Xunit.Sdk;

namespace Ago.Core.IntegrationTests
{
    /// <summary>
    /// Integration tests — require a running Ollama instance on localhost:11434.
    /// Skipped automatically if Ollama is unavailable.
    ///
    /// Run explicitly:
    ///   dotnet test --filter "Category=Integration"
    ///
    /// Excluded from default CI run via:
    ///   dotnet test --filter "Category!=Integration"
    /// </summary>
    [Trait("Category", "Integration")]
    public class OllamaClientIntegrationTests
    {
        private const string BaseUrl = AgoConstants.Defaults.OllamaBaseUrl; // "http://localhost:11434";
        private const string Model = AgoConstants.Defaults.OllamaModel; // "qwen2.5-coder:7b";
        private readonly OllamaClient _client = new(Model, BaseUrl);

        private async Task SkipIfUnavailableAsync()
        {
            var available = await _client.IsAvailableAsync();
            if (!available)
                throw new SkipException("Ollama is not running on localhost:11434. Skipping integration tests.");
        }

        [Fact]
        public async Task IsAvailableAsync_ReturnsTrue_WhenOllamaIsRunning()
        {
            await SkipIfUnavailableAsync();

            var result = await _client.IsAvailableAsync();

            Assert.True(result);
        }

        [Fact]
        public async Task SendAsync_ReturnsNonEmptyContent_ForSimplePrompt()
        {
            await SkipIfUnavailableAsync();

            var messages = new[]
            {
                ChatMessage.System("You are a helpful assistant. Reply briefly."),
                ChatMessage.User("Say the word PONG and nothing else."),
            };

            var response = await _client.SendAsync(messages);

            Assert.NotNull(response);
            Assert.NotEmpty(response.Content);
        }

        [Fact]
        public async Task SendAsync_ReturnsModel_MatchingRequestedModel()
        {
            await SkipIfUnavailableAsync();

            var response = await _client.SendAsync(new[]
            {
                ChatMessage.User("Say hi."),
            });

            Assert.Equal(Model, response.Model);
        }

        [Fact]
        public async Task SendAsync_PopulatesTokenUsage()
        {
            await SkipIfUnavailableAsync();

            var response = await _client.SendAsync(new[]
            {
                ChatMessage.User("Say hi."),
            });

            Assert.NotNull(response.Usage);
            Assert.True(response.Usage!.PromptTokens > 0);
            Assert.True(response.Usage.CompletionTokens > 0);
            Assert.Equal(response.Usage.PromptTokens + response.Usage.CompletionTokens,
                         response.Usage.Total);
        }

        [Fact]
        public async Task SendAsync_WorksWithCancellationToken()
        {
            await SkipIfUnavailableAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var response = await _client.SendAsync(
                new[] { ChatMessage.User("Say hi.") },
                cts.Token);

            Assert.NotEmpty(response.Content);
        }

        [Fact]
        public async Task SendAsync_ThrowsOnCancelledToken()
        {
            await SkipIfUnavailableAsync();

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                _client.SendAsync(
                    new[] { ChatMessage.User("Say hi.") },
                    cts.Token));
        }
    }
}