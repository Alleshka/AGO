using Ago.Core.Git.Diff;

namespace Ago.Core.Git
{
    public class GitService(IGitRunner runner)
    {
        /// <summary>
        /// Returns diff of staged + unstaged changes against HEAD.
        /// Equivalent to: git diff HEAD
        /// </summary>
        public async Task<DiffResult> GetDiffAsync(string projectRoot, CancellationToken ct = default)
        {
            var raw = await runner.RunAsync("diff HEAD", projectRoot, ct);
            return GitDiffParser.Parse(raw);
        }

        /// <summary>
        /// Returns diff of a specific commit against its parent.
        /// Equivalent to: git diff {commitSha}^ {commitSha}
        /// </summary>
        public async Task<DiffResult> GetCommitDiffAsync(
            string projectRoot, 
            string commitSha, 
            CancellationToken ct = default)
        {
            var raw = await runner.RunAsync($"diff {commitSha}^ {commitSha}", projectRoot, ct);
            return GitDiffParser.Parse(raw);
        }

        /// <summary>
        /// Returns diff between current branch and a base branch (e.g. main).
        /// Equivalent to: git diff {baseBranch}...HEAD
        /// Used by GitHub bot to get PR diff.
        /// </summary>
        public async Task<DiffResult> GetBranchDiffAsync(
            string projectRoot, string baseBranch = "main", CancellationToken ct = default)
        {
            var raw = await runner.RunAsync($"diff {baseBranch}...HEAD", projectRoot, ct);
            return GitDiffParser.Parse(raw);
        }

        public async Task<string> GetCurrentBranchAsync(string projectRoot, CancellationToken ct = default)
        {
            var result = await runner.RunAsync("rev-parse --abbrev-ref HEAD", projectRoot, ct);
            return result.Trim();
        }

        public async Task<string> GetCurrentCommitAsync(string projectRoot, CancellationToken ct = default)
        {
            var result = await runner.RunAsync("rev-parse --short HEAD", projectRoot, ct);
            return result.Trim();
        }

        /// <summary>
        /// Returns list of files changed compared to base branch.
        /// </summary>
        public async Task<IReadOnlyList<string>> GetChangedFilesAsync(
            string projectRoot, string baseBranch = "main", CancellationToken ct = default)
        {
            var result = await runner.RunAsync(
                $"diff --name-only {baseBranch}...HEAD", projectRoot, ct);

            return result
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .ToList();
        }
    }
}
