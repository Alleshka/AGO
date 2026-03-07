using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Ago.Core.LLM
{
    public class OllamaClient : IChatClient
    {
        private class OllamaChatRequest
        {
            public string Model { get; set; } = string.Empty;
            public List<OllamaMessage> Messages { get; set; } = new();
            public bool Stream { get; set; }
        }

        private record OllamaMessage(string Role, string Content);

        private class OllamaChatResponse
        {
            public string Model { get; set; } = string.Empty;
            public OllamaMessage Message { get; set; } = new("assistant", string.Empty);
            public int? PromptEvalCount { get; set; }
            public int? EvalCount { get; set; }
        }

        private readonly HttpClient _http;
        private readonly string _model;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
        };

        public OllamaClient(string model = AgoConstants.Defaults.OllamaModel, string baseUrl = AgoConstants.Defaults.OllamaBaseUrl)
        {
            _model = model;
            _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        }

        // Allow injecting HttpClient for tests
        internal OllamaClient(HttpClient httpClient, string model)
        {
            _http = httpClient;
            _model = model;
        }

        public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
        {
            try
            {
                var response = await _http.GetAsync("/api/tags", ct);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<ChatResponse> SendAsync(IReadOnlyList<ChatMessage> messages, CancellationToken ct = default)
        {
            var request = new OllamaChatRequest
            {
                Model = _model,
                Messages = messages.Select(m => new OllamaMessage(m.Role, m.Content)).ToList(),
                Stream = false,
            };

            var response = await _http.PostAsJsonAsync("/api/chat", request, JsonOptions, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions, ct)
                ?? throw new InvalidOperationException("Empty response from Ollama");

            return new ChatResponse
            {
                Content = result.Message.Content,
                Model = result.Model,
                Usage = result.PromptEvalCount is not null
                    ? new TokenUsage(result.PromptEvalCount.Value, result.EvalCount ?? 0)
                    : null,
            };
        }
    }
}
