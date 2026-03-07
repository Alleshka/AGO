using Ago.Core.LLM;
using System.Net;
using System.Text;
using System.Text.Json;
namespace Ago.Core.Tests
{
    public class OllamaClientTests
    {
        private static OllamaClient BuildClient(
            string responseJson,
            HttpStatusCode statusCode = HttpStatusCode.OK,
            string model = "qwen2.5-coder:7b")
        {
            var handler = new FakeHttpHandler(responseJson, statusCode);
            var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
            return new OllamaClient(http, model);
        }

        private static string OllamaJson(
            string content,
            string model = "qwen2.5-coder:7b",
            int promptTokens = 10,
            int completionTokens = 20)
        {
            return JsonSerializer.Serialize(new
            {
                model,
                message = new { role = "assistant", content },
                prompt_eval_count = promptTokens,
                eval_count = completionTokens,
            });
        }

        private static string AvailableTagsJson() => JsonSerializer.Serialize(new { models = new[] { new { name = "qwen2.5-coder:7b" } } });

        [Fact]
        public async Task SendAsync_ReturnsContent_OnSuccessResponse()
        {
            var client = BuildClient(OllamaJson("Looks good!"));

            var response = await client.SendAsync(new[]
            {
                ChatMessage.User("Review this code"),
            });

            Assert.Equal("Looks good!", response.Content);
        }

        [Fact]
        public async Task SendAsync_ReturnsModel_FromResponse()
        {
            var client = BuildClient(OllamaJson("ok", model: "qwen2.5-coder:7b"));

            var response = await client.SendAsync(new[] { ChatMessage.User("hi") });

            Assert.Equal("qwen2.5-coder:7b", response.Model);
        }

        [Fact]
        public async Task SendAsync_PopulatesTokenUsage_WhenPresent()
        {
            var client = BuildClient(OllamaJson("ok", promptTokens: 15, completionTokens: 30));

            var response = await client.SendAsync(new[] { ChatMessage.User("hi") });

            Assert.NotNull(response.Usage);
            Assert.Equal(15, response.Usage!.PromptTokens);
            Assert.Equal(30, response.Usage.CompletionTokens);
            Assert.Equal(45, response.Usage.Total);
        }

        [Fact]
        public async Task SendAsync_Throws_OnNonSuccessStatusCode()
        {
            var client = BuildClient("{}", HttpStatusCode.InternalServerError);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                client.SendAsync(new[] { ChatMessage.User("hi") }));
        }

        [Fact]
        public async Task SendAsync_SendsAllMessages_InCorrectOrder()
        {
            var handler = new CapturingHttpHandler(OllamaJson("ok"));
            var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
            var client = new OllamaClient(http, "qwen2.5-coder:7b");

            await client.SendAsync(new[]
            {
                ChatMessage.System("You are a code reviewer"),
                ChatMessage.User("Review this"),
            });

            var body = JsonDocument.Parse(handler.LastRequestBody!);
            var msgs = body.RootElement.GetProperty("messages").EnumerateArray().ToList();

            Assert.Equal(2, msgs.Count);
            Assert.Equal("system", msgs[0].GetProperty("role").GetString());
            Assert.Equal("user", msgs[1].GetProperty("role").GetString());
        }

        [Fact]
        public async Task IsAvailableAsync_ReturnsTrue_WhenOllamaResponds()
        {
            var client = BuildClient(AvailableTagsJson());

            var available = await client.IsAvailableAsync();

            Assert.True(available);
        }

        [Fact]
        public async Task IsAvailableAsync_ReturnsFalse_OnHttpError()
        {
            var client = BuildClient("{}", HttpStatusCode.ServiceUnavailable);

            var available = await client.IsAvailableAsync();

            Assert.False(available);
        }

        [Fact]
        public async Task IsAvailableAsync_ReturnsFalse_WhenConnectionRefused()
        {
            var handler = new ThrowingHttpHandler(new HttpRequestException("Connection refused"));
            var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
            var client = new OllamaClient(http, "qwen2.5-coder:7b");

            var available = await client.IsAvailableAsync();

            Assert.False(available);
        }

        private class FakeHttpHandler(string responseBody, HttpStatusCode statusCode) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                return Task.FromResult(new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
                });
            }
        }

        private class CapturingHttpHandler(string responseBody) : HttpMessageHandler
        {
            public string? LastRequestBody { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken ct)
            {
                LastRequestBody = await request.Content!.ReadAsStringAsync(ct);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
                };
            }
        }

        private class ThrowingHttpHandler(Exception exception) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken ct) =>
                Task.FromException<HttpResponseMessage>(exception);
        }
    }
}
