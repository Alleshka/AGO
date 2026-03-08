using Ago.Core.Agents;
using Ago.Core.Agents.CodeReview;
using Ago.Core.Agents.Explainer;
using Ago.Core.Config;
using Ago.Core.Git;

namespace Ago.Core.Orchestrator
{
    /// <summary>
    /// Wires up the Orchestrator with all dependencies.
    /// No DI container needed — just a factory method.
    /// Replace with proper DI registration when adding Ago.Api.
    /// </summary>
    public static class OrchestratorFactory
    {
        public static Orchestrator Create(string? projectRoot = null)
        {
            var root = projectRoot ?? ProjectResolver.ResolveProjectRoot()
                ?? throw new InvalidOperationException("Not an ago project. Run 'ago init'.");

            var configService = new ConfigService();
            var config = configService.Load(root);
            var factory = new LlmProviderFactory(config);
            var gitRunner = new ProcessGitRunner();
            var git = new GitService(gitRunner);
            var resolver = new PromptResolver();

            var agents = new Dictionary<string, IAgent>
            {
                [AgoConstants.AgentIds.StyleReview] = new StyleReviewAgent(factory, resolver),
                [AgoConstants.AgentIds.Explainer] = new ExplainerAgent(factory, resolver),
                // Add more agents here as they are implemented:
                // [AgoConstants.AgentIds.PerformanceReview] = new PerformanceReviewAgent(factory, resolver),
                // [AgoConstants.AgentIds.SecurityReview]    = new SecurityReviewAgent(factory, resolver),
                // [AgoConstants.AgentIds.TestGeneration]    = new TestGeneratorAgent(factory, resolver),
            };

            return new Orchestrator(agents, git, configService);
        }
    }
}
