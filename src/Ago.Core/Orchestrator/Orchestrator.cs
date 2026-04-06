using Ago.Core.Agents;
using Ago.Core.Config;
using Ago.Core.Git;
using Ago.Core.Git.Diff;
using System.Diagnostics;
using System.Text;

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
            var selected = SelectAgents(options, config);

            if (selected.Count == 0)
            {
                Console.WriteLine("No agents selected. Use --review, --explain, --tests, or --docs.");
                return new OrchestratorResult { Success = true, Elapsed = sw.Elapsed };
            }

            var files = await ResolveInputAsync(options, projectRoot, ct);
            var sharedContext = new AnalysisContext
            {
                ProjectRoot = projectRoot,
                Config = config,
                Path = options.Path,
                Scope = options.Scope,
                Files = files,
            };

            var pipeline = new PipelineContext { Analysis = sharedContext };
            var results = await RunAnalysePhaseAsync(selected, pipeline, ct);
            pipeline.AddResults(results);

            var result = BuildResult(pipeline, sw.Elapsed);
            PrintReport(result, options);

            return result;
        }

        private async Task<IReadOnlyDictionary<string, string>> ResolveInputAsync(
            RunOptions options,
            string projectRoot,
            CancellationToken ct)
        {
            if (options.Scope == RunScope.Diff)
            {
                var diff = await _git.GetDiffAsync(projectRoot, ct);
                var files = diff.Files
                    .Where(f => f.ChangeType != FileChangeType.Deleted)
                    .ToDictionary(
                    f => Path.GetFullPath(Path.Combine(projectRoot, f.Path)),
                    f => FormatFileDiff(f));
                return files;
            }

            if (options.Path is not null)
            {
                var files = ResolveFilesFromPath(options.Path).ToDictionary(
                    f => f,
                    f => File.ReadAllText(f));
            }

            return new Dictionary<string, string>();
        }

        private static string FormatFileDiff(FileDiff file)
        {
            var sb = new StringBuilder();
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

            return sb.ToString();
        }

        private static IReadOnlyList<string> ResolveFilesFromPath(string path)
        {
            if (Directory.Exists(path))
            {
                return
                    Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                    .Select(Path.GetFullPath)
                    .ToList();
            }

            if (File.Exists(path))
            {
                return [Path.GetFullPath(path)];
            }

            return [];
        }

        private List<IAgent> SelectAgents(RunOptions options, AgoConfig config)
        {
            return options.Agents
                .Select(id => _agents.GetValueOrDefault(id))
                .Where(a => a is not null && IsEnabled(a!, config))
                .ToList()!;
        }

        private static bool IsEnabled(IAgent agent, AgoConfig config) =>
            !config.Agents.TryGetValue(agent.Id, out var cfg) || cfg.Enabled;

        private async Task<IReadOnlyList<AgentResult>> RunAnalysePhaseAsync(
            List<IAgent> agents,
            PipelineContext pipeline,
            CancellationToken ct)
        {
            var results = new AgentResult[agents.Count];

            await Parallel.ForEachAsync(
                agents.Select((agent, i) => (agent, i)),
                new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
                async (item, token) =>
                {
                    var (agent, i) = item;
                    Console.WriteLine($"  → Running {agent.Id}...");
                    results[i] = await RunAgentAsync(agent, pipeline.Analysis, token);
                });

            return results;
        }

        private async Task<AgentResult> RunAgentAsync(
            IAgent agent,
            AnalysisContext context,
            CancellationToken ct)
        {
            // Whole agents receive all files at once — ExplainerAgent, PlannerAgent
            if (agent.AgentScope == AgentScope.FileSet)
                return await agent.AnalyseAsync(context, ct);

            // PerFile: single file — pass directly
            if (context.Files.Count <= 1)
                return await agent.AnalyseAsync(context, ct);

            // PerFile: multiple files — run per file, aggregate findings
            var allFindings = new List<Finding>();
            var errors = new List<string>();

            foreach (var (path, content) in context.Files)
            {
                var fileContext = context with
                {
                    Files = new Dictionary<string, string> { [path] = content },
                };

                var result = await agent.AnalyseAsync(fileContext, ct);

                if (result.Success)
                    allFindings.AddRange(result.Findings);
                else
                    errors.Add($"{Path.GetFileName(path)}: {result.Error}");
            }

            return new AgentResult
            {
                AgentId = agent.Id,
                Success = errors.Count == 0,
                Findings = allFindings,
                Error = errors.Count > 0 ? string.Join("\n", errors) : null,
            };
        }

        private static OrchestratorResult BuildResult(PipelineContext pipeline, TimeSpan elapsed)
        {
            var sorted = pipeline.Findings
                .OrderBy(f => f.Priority)
                .ThenBy(f => f.FilePath)
                .ThenBy(f => f.LineStart)
                .ToList();

            return new OrchestratorResult
            {
                Success = pipeline.AgentResults.All(r => r.Success),
                Findings = sorted,
                Explanations = pipeline.AgentResults
                    .Where(r => r.Explanation is not null)
                    .Select(r => new AgentExplanation(r.AgentId, r.Explanation!))
                    .ToList(),
                AgentResults = pipeline.AgentResults,
                Elapsed = elapsed,
            };
        }

        private static void PrintReport(OrchestratorResult result, RunOptions options)
        {
            Console.WriteLine();

            foreach (var explanation in result.Explanations)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"── Explanation ({explanation.AgentId}) ──");
                Console.ResetColor();
                Console.WriteLine(explanation.Text);
                Console.WriteLine();
            }

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
                    Console.WriteLine($"{f.FilePath}:{f.LineStart}  {f.Description}");
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  ✓ No findings.");
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"Done in {result.Elapsed.TotalSeconds:F1}s" +
                              $"  |  {result.FindingCount} finding(s)" +
                              $"  |  {result.AgentResults.Count} agent(s) ran");
            Console.ResetColor();
        }

    }
}
