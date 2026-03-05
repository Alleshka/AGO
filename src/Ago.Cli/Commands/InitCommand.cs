using Ago.Core.Config;
using System.CommandLine;

namespace Ago.Cli.Commands
{
    public static class InitCommand
    {
        public static Command Build()
        {
            var nameOption = new Option<string?>(name: "--name")
            {
                Description = "Project name (defaults to current folder name)"
            };

            var languageOption = new Option<string>(name: "--language")
            {
                Description = "Project language",
                DefaultValueFactory = (r) => "csharp"
            };

            var forceOption = new Option<bool>(name: "--force")
            {
                Description = "Overwrite existing config"
            };

            var pathOption = new Option<string>(name: "--path")
            {
                Description = "Path to initialise project in (defaults to current directory)",
                DefaultValueFactory = (r) => Environment.CurrentDirectory
            };

            var command = new Command("init", "Initialise a new ago project in the current directory")
            {
                nameOption,
                languageOption,
                forceOption,
                pathOption
            };

            command.SetAction(async (result, ct) =>
            {
                var name = result.GetValue(nameOption);
                var language = result.GetValue(languageOption);
                var force = result.GetValue(forceOption);
                var path = result.GetValue(pathOption);
                await ExecuteAsync(name, language, force, path);
            });

            return command;
        }

        private static async Task ExecuteAsync(string? name, string language, bool force, string? path)
        {
            var projectRoot = path ?? Directory.GetCurrentDirectory();
            var projectName = name ?? new DirectoryInfo(projectRoot).Name;
            var configPath = ConfigService.ConfigFilePath(projectRoot);

            // Guard: already initialised
            if (File.Exists(configPath) && !force)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Project already initialised: {configPath}");
                Console.WriteLine("Use --force to overwrite.");
                Console.ResetColor();
                return;
            }

            try
            {
                var service = new ConfigService();

                // If --force, remove existing config first
                if (File.Exists(configPath) && force)
                    File.Delete(configPath);

                var config = service.Init(projectRoot, projectName, language);

                PrintSuccess(projectRoot, projectName, config);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
                Environment.Exit(1);
            }
        }

        private static void PrintSuccess(string projectRoot, string projectName, AgoConfig config)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Initialised ago project: {projectName}");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine($"  Config : {ConfigService.ConfigFilePath(projectRoot)}");
            Console.WriteLine($"  Folder : {ConfigService.AgoFolderPath(projectRoot)}");
            Console.WriteLine($"  LLM    : {config.Llm.Default}");
            Console.WriteLine();
            Console.WriteLine("Next steps:");
            Console.WriteLine("  ago run --review --diff   review the current diff");
            Console.WriteLine("  ago run --tests --diff    generate tests for the diff");
        }
    }

}
