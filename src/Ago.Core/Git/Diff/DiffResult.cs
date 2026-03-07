namespace Ago.Core.Git.Diff
{
    /// <summary>
    /// Full diff result returned by GitService.
    /// </summary>
    public record DiffResult
    {
        public IReadOnlyList<FileDiff> Files { get; init; } = [];
        public string RawDiff { get; init; } = string.Empty;

        public bool IsEmpty => Files.Count == 0;
    }
}
