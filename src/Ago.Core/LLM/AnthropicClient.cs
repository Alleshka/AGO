using Ago.Core.Config;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ago.Core.LLM
{
    public class AnthropicClient : IChatClient
    {
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly string _model;
        private const int DefaultMaxTokens = 4096;

        private record AnthropicMessage(string Role, string Content);

        private class AnthropicRequest
        {
            public string Model { get; set; } = string.Empty;
            public int MaxTokens { get; set; }
            public string? System { get; set; }
            public List<AnthropicMessage> Messages { get; set; } = new();
        }

        private class ContentBlock
        {
            public string Type { get; set; } = string.Empty;
            public string? Text { get; set; }
        }

        private class UsageInfo
        {
            public int InputTokens { get; set; }
            public int OutputTokens { get; set; }
        }


        private class AnthropicResponse
        {
            public List<ContentBlock> Content { get; set; } = new();
            public string Model { get; set; } = string.Empty;
            public UsageInfo? Usage { get; set; }
        }


        private readonly HttpClient _http = SHaredHttpClient.Instance;


        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
        };

        public AnthropicClient(LlmProviderConfig config)
        {
            _model = config.Model ?? AgoConstants.DefaultsProviderConfigs.AnthropicProviderConfig.Model;
            _baseUrl = $"{(string.IsNullOrWhiteSpace(config.BaseUrl) ? "https://api.anthropic.com" : config.BaseUrl)}/v1/messages";
            _apiKey = config.ApiKey ?? throw new Exception($"The api key for {this.GetType().Name} must be provided in the configuration");
        }

        // Allow injecting HttpClient for tests
        internal AnthropicClient(HttpClient httpClient, string model)
        {
            _http = httpClient;
            _model = model;
        }

        public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
        {
            try
            {
                var probe = new AnthropicRequest
                {
                    Model = _model,
                    MaxTokens = 1,
                    Messages = [new AnthropicMessage("user", "ping")]
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
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AnthropicResponse>(JsonOptions, ct)
                ?? throw new InvalidOperationException("Empty response from Anthropic");

            return MapResponse(result);
        }

        private AnthropicRequest BuildRequest(IReadOnlyList<ChatMessage> messages) => new()
        {
            Model = _model,
            MaxTokens = DefaultMaxTokens,
            System = messages.FirstOrDefault(m => m.Role == "system")?.Content,
            Messages = messages
            .Where(m => m.Role != "system")
            .Select(m => new AnthropicMessage(m.Role, m.Content))
            .ToList(),
        };

        private HttpRequestMessage BuildHttpRequest(object body)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl)
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            };

            request.Headers.Add("x-api-key", _apiKey);
            _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01"); // TODO: Make configurable??

            return request;
        }

        private static ChatResponse MapResponse(AnthropicResponse result) => new()
        {
            Content = result.Content.FirstOrDefault(b => b.Type == "text")?.Text
            ?? throw new InvalidOperationException("No text block in Anthropic response"),
            Model = result.Model,
            Usage = result.Usage is not null
            ? new TokenUsage(result.Usage.InputTokens, result.Usage.OutputTokens)
            : null,
        };
    }
}

