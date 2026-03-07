namespace Ago.Core.Git.Diff
{
    public record DiffHunk
    {
        public int OldStart { get; init; }
        public int OldCount { get; init; }
        public int NewStart { get; init; }
        public int NewCount { get; init; }
        public string Header { get; init; } = string.Empty;
        public IReadOnlyList<DiffLine> Lines { get; init; } = [];
    }
}
