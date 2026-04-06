using Ago.Core.Config;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace Ago.Core.LLM
{
    public class OpenRouterClient : IChatClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
        };

        private const int DefaultMaxTokens = 4096;

        private readonly string _model;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private HttpClient _http = SHaredHttpClient.Instance;

        public OpenRouterClient(LlmProviderConfig config)
        {
            _model = config.Model ?? AgoConstants.DefaultsProviderConfigs.OpenRouterConfig.Model;
            _baseUrl = $"{(string.IsNullOrWhiteSpace(config.BaseUrl) ? "https://openrouter.ai/" : config.BaseUrl)}/chat/completions";
            _apiKey = config.ApiKey ?? throw new Exception($"The api key for {this.GetType().Name} must be provided in the configuration");
        }

        public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
        {
            try
            {
                // Probe with minimal tokens
                var probe = new OpenRouterRequest
                {
                    Model = _model,
                    MaxTokens = 1,
                    Messages = [new OpenRouterMessage("user", "ping")],
                };

                var request = BuildHttpRequest(probe);
                var response = await _http.SendAsync(request, ct);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<ChatResponse> SendAsync(IReadOnlyList<ChatMessage> messages, CancellationToken ct = default)
        {
            var body = BuildRequest(messages);
            var request = BuildHttpRequest(body);

            var response = await _http.SendAsync(request, ct);

            // Временно — посмотреть что именно отвечает API
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(
                    $"OpenRouter error {(int)response.StatusCode}: {errorBody}");
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OpenRouterResponse>(JsonOptions, ct)
                ?? throw new InvalidOperationException("Empty response from OpenRouter");

            return MapResponse(result);
        }

        private OpenRouterRequest BuildRequest(IReadOnlyList<ChatMessage> messages) => new()
        {
            Model = _model,
            MaxTokens = DefaultMaxTokens,
            Messages = messages
            .Select(m => new OpenRouterMessage(m.Role, m.Content))
            .ToList(),
        };

        private HttpRequestMessage BuildHttpRequest(object body)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl)
            {
                Content = JsonContent.Create(body, options: JsonOptions),
            };
            request.Headers.Add("Authorization", $"Bearer {_apiKey}");

            // OpenRouter requires HTTP-Referer — identifies your app
            // Any URL works, used for analytics on their side
            request.Headers.Add("HTTP-Referer", "https://github.com/ago-cli");
            request.Headers.Add("X-Title", "AGO");

            return request;
        }

        private static ChatResponse MapResponse(OpenRouterResponse result)
        {
            var choice = result.Choices.FirstOrDefault()
                ?? throw new InvalidOperationException("No choices in OpenRouter response");

            return new ChatResponse
            {
                Content = choice.Message.Content,
                Model = result.Model,
                Usage = result.Usage is not null
                    ? new TokenUsage(result.Usage.PromptTokens, result.Usage.CompletionTokens)
                    : null,
            };
        }

        private class OpenRouterRequest
        {
            public string Model { get; set; } = string.Empty;
            public int MaxTokens { get; set; } = DefaultMaxTokens;
            public List<OpenRouterMessage> Messages { get; set; } = [];
        }

        private record OpenRouterMessage(string Role, string Content);

        private class OpenRouterResponse
        {
            public string Model { get; set; } = string.Empty;
            public List<OpenRouterChoice> Choices { get; set; } = [];
            public OpenRouterUsage? Usage { get; set; }
        }

        private class OpenRouterChoice
        {
            public OpenRouterMessage Message { get; set; } = new("assistant", string.Empty);
        }

        private class OpenRouterUsage
        {
            public int PromptTokens { get; set; }
            public int CompletionTokens { get; set; }
        }
    }
}
