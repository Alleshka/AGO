namespace Ago.Core.Git.Diff
{
    public record FileDiff
    {
        public string Path { get; init; } = string.Empty;
        public string? OldPath { get; init; }
        public FileChangeType ChangeType { get; init; }
        public IReadOnlyList<DiffHunk> Hunks { get; init; } = [];

        public IEnumerable<DiffLine> AddedLines => Hunks.SelectMany(h => h.Lines).Where(l => l.Type == DiffLineType.Added);
        public IEnumerable<DiffLine> RemovedLines => Hunks.SelectMany(h => h.Lines).Where(l => l.Type == DiffLineType.Removed);
    }
}
