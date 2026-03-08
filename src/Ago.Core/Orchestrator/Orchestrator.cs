using Ago.Core.Agents;
using Ago.Core.Config;
using Ago.Core.Git;
using Ago.Core.Git.Diff;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ago.Core.Orchestrator
{
    public class Orchestrator
    {
        private readonly Dictionary<string, IAgent> _agents;
        private readonly GitService _git;
        private readonly ConfigService _configService;

        public Orchestrator(
            Dictionary<string, IAgent> agents,
            GitService git,
            ConfigService configService)
        {
            _agents = agents;
            _git = git;
            _configService = configService;
        }

        public async Task<OrchestratorResult> RunAsync(
            RunOptions options,
            CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();

            var projectRoot = options.ProjectRoot
                ?? ProjectResolver.ResolveProjectRoot()
                ?? throw new InvalidOperationException(
                    "Not an ago project. Run 'ago init'.");
            var config = _configService.Load(projectRoot);
            var context = await BuildContextAsync(options, projectRoot, config, ct);
            var selected = SelectAgents(options, config);

            if (selected.Count == 0)
            {
                Console.WriteLine("No agents selected. Use --review, --explain, --tests, or --docs.");
                return new OrchestratorResult { Success = true, Elapsed = sw.Elapsed };
            }

            var agentResults = await RunAgentsAsync(selected, context, ct);
            var result = Aggregate(agentResults, sw.Elapsed);
            PrintReport(result, options);

            return result;
        }

        // -------------------------------------------------------------------------
        // Context
        // -------------------------------------------------------------------------

        private async Task<AnalysisContext> BuildContextAsync(
            RunOptions options,
            string projectRoot,
            AgoConfig config,
            CancellationToken ct)
        {
            DiffResult? diff = null;

            if (options.Scope == RunScope.Diff)
                diff = await _git.GetDiffAsync(projectRoot, ct);

            return new AnalysisContext
            {
                ProjectRoot = projectRoot,
                Config = config,
                Diff = diff,
                FilePath = options.Scope == RunScope.File ? options.FilePath : null,
                ClassName = options.Scope == RunScope.Class ? options.ClassName : null,
            };
        }

        // -------------------------------------------------------------------------
        // Agent selection
        // -------------------------------------------------------------------------

        private List<IAgent> SelectAgents(RunOptions options, AgoConfig config)
        {
            if (options.Preset is not null)
            {
                var presents = config.Presets.GetValueOrDefault(options.Preset) ?? [];
            }

            var selected = new List<IAgent>();

            foreach (var agent in options.Agents)
            {
                TryAdd(selected, config, agent);
            }

            return selected;
        }

        private void TryAdd(List<IAgent> list, AgoConfig config, string agentId)
        {
            // Skip if not registered
            if (!_agents.TryGetValue(agentId, out var agent))
                return;

            // Skip if disabled in config
            if (config.Agents.TryGetValue(agentId, out var agentConfig) && !agentConfig.Enabled)
                return;

            list.Add(agent);
        }

        private static async Task<List<AgentResult>> RunAgentsAsync(
            List<IAgent> agents,
            AnalysisContext context,
            CancellationToken ct)
        {
            var results = new AgentResult[agents.Count];

            await Parallel.ForEachAsync(
                agents.Select((agent, i) => (agent, i)),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = 4,
                    CancellationToken = ct,
                },
                async (item, token) =>
                {
                    var (agent, i) = item;
                    Console.WriteLine($"  → Running {agent.Id}...");
                    results[i] = await agent.AnalyseAsync(context, token);
                });

            return results.ToList();
        }

        private static OrchestratorResult Aggregate(
            List<AgentResult> agentResults,
            TimeSpan elapsed)
        {
            var allFindings = new List<Finding>();
            var allExplanations = new List<AgentExplanation>();

            foreach (var result in agentResults)
            {
                if (!result.Success)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"{result.AgentId} failed: {result.Error}");
                    Console.ResetColor();
                    continue;
                }

                allFindings.AddRange(result.Findings);

                if (result.Explanation is not null)
                {
                    allExplanations.Add(new AgentExplanation(
                        AgentId: result.AgentId,
                        FilePath: result.Findings.FirstOrDefault()?.FilePath ?? "unknown",
                        Text: result.Explanation));
                }
            }

            // Sort findings: High → Medium → Low, then by file and line
            var sorted = allFindings
                .OrderBy(f => f.Priority)
                .ThenBy(f => f.FilePath)
                .ThenBy(f => f.LineStart)
                .ToList();

            return new OrchestratorResult
            {
                Success = agentResults.All(r => r.Success),
                Findings = sorted,
                Explanations = allExplanations,
                AgentResults = agentResults,
                Elapsed = elapsed,
            };
        }

        private static void PrintReport(OrchestratorResult result, RunOptions options)
        {
            Console.WriteLine();

            // Explanations
            foreach (var explanation in result.Explanations)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"── Explanation ({explanation.AgentId}) ──");
                Console.ResetColor();
                Console.WriteLine(explanation.Text);
                Console.WriteLine();
            }

            // Findings
            if (result.HasFindings)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"── Findings ({result.FindingCount}) ──");
                Console.ResetColor();

                foreach (var f in result.Findings)
                {
                    var color = f.Priority switch
                    {
                        Priority.High => ConsoleColor.Red,
                        Priority.Medium => ConsoleColor.Yellow,
                        Priority.Low => ConsoleColor.Gray,
                        _ => ConsoleColor.White,
                    };

                    Console.ForegroundColor = color;
                    Console.Write($"  [{f.Priority,-6}] ");
                    Console.ResetColor();
                    Console.Write($"{f.FilePath}:{f.LineStart}");
                    Console.WriteLine($"  {f.Description}");

                    if (f.ProposedChange is not null && !options.DryRun)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"           → {f.ProposedChange}");
                        Console.ResetColor();
                    }
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("No findings.");
                Console.ResetColor();
            }

            // Summary
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"Done in {result.Elapsed.TotalSeconds:F1}s  " +
                              $"| {result.FindingCount} finding(s)  " +
                              $"| {result.AgentResults.Count} agent(s) ran");
            Console.ResetColor();
        }
    }
}
