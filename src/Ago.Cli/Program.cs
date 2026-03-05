using Ago.Cli.Commands;
using System.CommandLine;

var root = new RootCommand("ago — multi-agent code orchestrator");
root.Add(InitCommand.Build());
return await root.Parse(args).InvokeAsync();