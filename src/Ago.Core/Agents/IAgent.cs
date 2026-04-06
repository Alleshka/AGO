namespace Ago.Core.Agents
{
    public interface IAgent
    {
        string Id { get; }
        AgentScope AgentScope { get; }
        Task<AgentResult> AnalyseAsync(AnalysisContext context, CancellationToken ct = default);
    }
}
