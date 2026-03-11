using Ago.Core.Git.Diff;
using Ago.Core.LLM;
using System.Text;

namespace Ago.Core.Agents
{
    /// <summary>
    /// Base class for agents that use LLM.
    /// Handles prompt sending and common error handling.
    /// </summary>
    public abstract class LlmAgentBase : IAgent
    {
        protected virtual bool UseFileChanking => true;

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
            var files = ResolveFiles(context);

            if (UseFileChanking && files.Count > 1)
            {
                var allFindings = new List<Finding>();
                var errors = new List<string>();

                foreach (var file in files)
                {
                    var fileContext = context with { Path = file, Diff = null };
                    var result = await AnalyseAsync(fileContext, ct);

                    if (result.Success)
                    {
                        allFindings.AddRange(result.Findings);
                    }
                    else
                    {
                        errors.Add($"{file}: {result.Error}");
                    }
                }

                return new AgentResult
                {
                    AgentId = Id,
                    Success = errors.Count == 0,
                    Findings = allFindings,
                    Error = errors.Count > 0 ? string.Join(Environment.NewLine, errors) : null
                };
            }
            else
            {
                return await AnalyseSingleAsync(context, ct);
            }
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

        protected abstract IReadOnlyList<ChatMessage> BuildPrompt(AnalysisContext context, PromptResolver promptResolver);

        protected abstract AgentResult ParseResponse(string rawResponse, AnalysisContext context);

        protected static string FormatDiffForPrompt(DiffResult diff)
        {
            var sb = new StringBuilder();

            foreach (var file in diff.Files)
            {
                sb.AppendLine($"### {file.Path} ({file.ChangeType})");

                foreach (var hunk in file.Hunks)
                {
                    sb.AppendLine(hunk.Header);

                    foreach (var line in hunk.Lines)
                    {
                        var prefix = line.Type switch
                        {
                            DiffLineType.Added => "+",
                            DiffLineType.Removed => "-",
                            _ => " ",
                        };
                        sb.AppendLine($"{prefix}{line.Content}");
                    }
                }

                sb.AppendLine();
            }

            return sb.ToString();
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

        protected static IReadOnlyList<string> ResolveFiles(AnalysisContext context)
        {
            if (context.Diff is not null)
            {
                return context.Diff.Files.Select(f => f.Path).ToList();
            }

            if (context.Path is not null)
            {
                return [];
            }

            if (Directory.Exists(context.Path))
            {
                return Directory.GetFiles(context.Path, "*", SearchOption.AllDirectories).ToList();
            }

            if (File.Exists(context.Path))
            {
                return [context.Path];
            }

            return [];
        }
    }
}
