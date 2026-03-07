namespace Ago.Core.Agents
{
    internal interface IAgent
    {
        string Id { get; }
        Task<AgentResult> AnalyseAsync(AnalysisContext context, CancellationToken ct = default);
    }
}
