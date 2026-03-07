using Ago.Core.Git.Diff;
using Ago.Core.LLM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ago.Core.Agents
{
    /// <summary>
    /// Base class for agents that use LLM.
    /// Handles prompt sending and common error handling.
    /// </summary>
    public abstract class LlmAgentBase : IAgent
    {
        private readonly LlmProviderFactory _factory;

        public abstract string Id { get; }

        protected LlmAgentBase(LlmProviderFactory factory)
        {
            _factory = factory;
        }

        public async Task<AgentResult> AnalyseAsync(AnalysisContext context, CancellationToken ct = default)
        {
            try
            {
                var messages = BuildPrompt(context);
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

        protected abstract IReadOnlyList<ChatMessage> BuildPrompt(AnalysisContext context);

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
    }
}
