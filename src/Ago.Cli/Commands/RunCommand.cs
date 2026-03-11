using Ago.Core;
using Ago.Core.Config;
using Ago.Core.Orchestrator;
using System.CommandLine;

namespace Ago.Cli.Commands
{
    internal class RunCommand
    {
        public static Command Build()
        {
            var agentsOption = new Option<string[]>(name: "--agents")
            {
                Description = "Agents to run",
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.OneOrMore,
            };

            var scopeOption = new Option<RunScope>(name: "--scope")
            {
                Description = "Scope to run"
            };

            var classOption = new Option<string?>("--class", "-c")
            {
                Description = "class name",
            };

            var pathOption = new Option<string?>("--path", "-p")
            {
                Description = "file path"
            };

            var styleReviewOption = new Option<bool>(name: $"--review")
            {
                Description = "alias for run with style-review"
            };

            var explainerOption = new Option<bool>(name: $"--explain", "-e")
            {
                Description = "alias for run with explainer"
            };

            var command = new Command("run", "Run with parameters")
            {
                agentsOption,
                scopeOption,
                classOption,
                pathOption,
                styleReviewOption,
                explainerOption
            };

            command.SetAction(async (result, ct) =>
            {
                var agents = result.GetValue(agentsOption).ToHashSet() ?? [];
                var scope = result.GetValue(scopeOption);
                var className = result.GetValue(classOption);
                var path = result.GetValue(pathOption);

                var isReview = result.GetValue(styleReviewOption);
                var isExplainer = result.GetValue(explainerOption);

                var runOptions = new RunOptions();

                if (isReview)
                {
                    runOptions.AddAgent(AgoConstants.AgentIds.StyleReview);
                    runOptions.AddAgent(AgoConstants.AgentIds.SecurityReview);
                    runOptions.AddAgent(AgoConstants.AgentIds.PerformanceReview);
                }

                if (isExplainer)
                {
                    runOptions.AddAgent(AgoConstants.AgentIds.Explainer);
                }

                runOptions.Scope = scope;

                if (!string.IsNullOrEmpty(path))
                {
                    runOptions.Path = path;
                    runOptions.Scope = RunScope.Path;
                }

                if (!string.IsNullOrEmpty(className))
                {
                    runOptions.ClassName = className;
                    runOptions.Scope = RunScope.Class;
                }

                runOptions.AddAgents(agents.ToArray());
                runOptions.ProjectRoot = ProjectResolver.ResolveProjectRoot();

                var orchestrator = OrchestratorFactory.Create();
                var orcResult = await orchestrator.RunAsync(runOptions);
            });

            return command;
        }
    }
}
