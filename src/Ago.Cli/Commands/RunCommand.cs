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

            var fileOption = new Option<string?>("--file", "-f")
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
                fileOption,
                styleReviewOption,
                explainerOption
            };

            command.SetAction(async (result, ct) =>
            {
                var agents = result.GetValue(agentsOption).ToHashSet() ?? [];
                var scope = result.GetValue(scopeOption);
                var className = result.GetValue(classOption);
                var filePath = result.GetValue(fileOption);

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

                runOptions.AddAgents(agents.ToArray());

                runOptions.ProjectRoot = ProjectResolver.ResolveProjectRoot();
                runOptions.FilePath = filePath;
                runOptions.ClassName = className;
                runOptions.Scope = scope;

                var orchestrator = OrchestratorFactory.Create();
                var orcResult = await orchestrator.RunAsync(runOptions);
            });

            return command;
        }
    }
}
