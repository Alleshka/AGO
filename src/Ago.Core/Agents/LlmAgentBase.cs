using Ago.Core.Git.Diff;
using Ago.Core.LLM;
using Ago.Core.Orchestrator;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace Ago.Core.Agents
{
    /// <summary>
    /// Base class for agents that use LLM.
    /// Handles prompt sending and common error handling.
    /// </summary>
    public abstract class LlmAgentBase : IAgent
    {
        public virtual AgentScope AgentScope => AgentScope.SingleFile;

        protected record AgentFinding(
            int LineStart,
            int LineEnd,
            string Description,
            string Priority);

        protected JsonNode schema = JsonSchemaExporter.GetJsonSchemaAsNode(
            JsonSerializerOptions.Default,
            typeof(AgentFinding));

        protected readonly LlmProviderFactory _factory;
        private readonly PromptResolver _promptResolver;

        public abstract string Id { get; }

        protected LlmAgentBase(LlmProviderFactory factory, PromptResolver promptResolver)
        {
            _factory = factory;
            _promptResolver = promptResolver;
        }

        public virtual async Task<AgentResult> AnalyseAsync(AnalysisContext context, CancellationToken ct = default)
        {
            var result = await AnalyseSingleAsync(context, ct);
            return result;
        }

        public async Task<AgentResult> AnalyseSingleAsync(AnalysisContext context, CancellationToken ct = default)
        {
            try
            {
                var messages = BuildPrompt(context, _promptResolver);
                var llm = _factory.GetForAgent(Id);
                var response = await llm.SendAsync(messages, ct);

                return ParseResponse(response.Content, context);
            }
            catch (Exception ex)
            {
                return new AgentResult
                {
                    AgentId = Id,
                    Success = false,
                    Error = ex.Message,
                };
            }
        }

        protected virtual IReadOnlyList<ChatMessage> BuildPrompt(AnalysisContext context, PromptResolver promptResolver)
        {
            var system = promptResolver.Resolve(Id, context.ProjectRoot) ?? BuildSystemPrompt(context);
            var userContent = BuildUserPrompt(context);
            return [
                ChatMessage.System(system),
                ChatMessage.User(userContent),
            ];
        }

        protected abstract string BuildSystemPrompt(AnalysisContext context);
        protected virtual string BuildUserPrompt(AnalysisContext context) => BuildUserContent(context);

        protected virtual string BuildUserContent(AnalysisContext context)
        {
            if (context.Files.Count == 0)
                throw new InvalidOperationException($"{Id}: context.Files is empty.");

            var(path, content) = context.Files.First();
            var label = context.Scope == RunScope.Diff
                ? $"diff of {Path.GetFileName(path)}"
                : $"file {Path.GetFileName(path)}";

            return $"Review this {label}:\n\n{content}";
        }

        protected virtual AgentResult ParseResponse(string rawResponse, AnalysisContext context)
        {
            var filePath = context.Path
                        ?? "unknown";

            try
            {
                var clean = StripMarkdownFences(rawResponse);
                var items = JsonSerializer.Deserialize<List<AgentFinding>>(clean.Trim(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? [];

                var findings = items.Select(f => new Finding
                {
                    AgentId = Id,
                    FilePath = filePath,
                    LineStart = f.LineStart,
                    LineEnd = f.LineEnd,
                    Type = FindingType.Suggestion,
                    Description = f.Description,
                    //ProposedChange = f.ProposedChange,
                    Priority = Enum.TryParse<Priority>(f.Priority, out var p) ? p : Priority.Medium,
                }).ToList();

                return new AgentResult
                {
                    AgentId = Id,
                    Success = true,
                    Findings = findings,
                };
            }
            catch (JsonException ex)
            {
                return new AgentResult
                {
                    AgentId = Id,
                    Success = false,
                    Error = $"Failed to parse LLM response as JSON: {ex.Message}\nRaw: {rawResponse}",
                };
            }
        }

        protected static string StripMarkdownFences(string raw)
        {
            var text = raw.Trim();

            // Remove ```json ... ``` or ``` ... ```
            if (text.StartsWith("```"))
            {
                var firstNewline = text.IndexOf('\n');
                if (firstNewline >= 0)
                    text = text[(firstNewline + 1)..];

                if (text.EndsWith("```"))
                    text = text[..^3].Trim();
            }

            return text.Trim();
        }
    }
}
