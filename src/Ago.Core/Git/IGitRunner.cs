namespace Ago.Core.Git
{
    public interface IGitRunner
    {
        Task<string> RunAsync(string arguments, string workingDirectory, CancellationToken ct = default);
    }
}
