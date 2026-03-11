using Ago.Core.LLM;
using System.Text.Json;

namespace Ago.Core.Agents.CodeReview
{
    /// <summary>
    /// Reviews code style: naming, formatting, readability, C# conventions.
    /// Returns structured findings with line numbers and proposed fixes.
    /// </summary>
    public class StyleReviewAgent : LlmAgentBase
    {
        public override string Id => AgoConstants.AgentIds.StyleReview;

        public StyleReviewAgent(LlmProviderFactory factory, PromptResolver promptResolver) : base(factory, promptResolver) { }

        protected override IReadOnlyList<ChatMessage> BuildPrompt(AnalysisContext context, PromptResolver promptResolver)
        {

            var system = promptResolver.Resolve(this.Id, context.ProjectRoot) ?? """
            You are an expert C# code reviewer focused on code style.
            Review the provided code for:
            - Naming conventions (PascalCase for types/methods, camelCase for locals)
            - Readability and clarity
            - Unnecessary complexity
            - C# idiomatic patterns (prefer expression bodies, pattern matching, etc.)
            - XML documentation on public members

            Respond ONLY with a JSON array of findings. No prose, no markdown fences.
            Each finding must have this exact shape:
            {
              "lineStart": <int>,
              "lineEnd": <int>,
              "description": "<what is wrong>",
              "proposedChange": "<suggested fix or null>",
              "priority": "<High|Medium|Low>"
            }

            If there are no issues, respond with an empty array: []
            """;

            var userContent = BuildUserContent(context);

            return [
                ChatMessage.System(system),
                ChatMessage.User(userContent),
            ];
        }

        private static string BuildUserContent(AnalysisContext context)
        {
            if (context.Diff is not null)
            {
                return $"Review this diff:\n\n{FormatDiffForPrompt(context.Diff)}";
            }

            if (context.RawCode is not null)
            {
                return $"Review this code:\n\n{context.RawCode}";
            }

            if (context.Path is not null)
            {
                var code = File.ReadAllText(context.Path);
                return $"Review this file ({context.Path}):\n\n{code}";
            }

            throw new InvalidOperationException($"{AgoConstants.AgentIds.StyleReview}: context must have Diff, RawCode, or FilePath.");
        }

        protected override AgentResult ParseResponse(string rawResponse, AnalysisContext context)
        {
            var filePath = context.Path
                        ?? context.Diff?.Files.FirstOrDefault()?.Path
                        ?? "unknown";

            try
            {
                var clean = StripMarkdownFences(rawResponse);
                var items = JsonSerializer.Deserialize<List<StyleFinding>>(clean.Trim(),
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
                    ProposedChange = f.ProposedChange,
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

        private record StyleFinding(
            int LineStart,
            int LineEnd,
            string Description,
            string? ProposedChange,
            string Priority);
    }
}
