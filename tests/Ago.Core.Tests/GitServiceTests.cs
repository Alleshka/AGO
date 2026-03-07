using Ago.Core.Git;

namespace Ago.Core.Tests
{
    public class GitServiceTests
    {
        // -------------------------------------------------------------------------
        // Fake runner
        // -------------------------------------------------------------------------

        private class FakeGitRunner(string output) : IGitRunner
        {
            public string? LastArguments { get; private set; }
            public string? LastWorkingDirectory { get; private set; }

            public Task<string> RunAsync(string arguments, string workingDirectory, CancellationToken ct = default)
            {
                LastArguments = arguments;
                LastWorkingDirectory = workingDirectory;
                return Task.FromResult(output);
            }
        }

        private class ThrowingGitRunner(Exception ex) : IGitRunner
        {
            public Task<string> RunAsync(string arguments, string workingDirectory, CancellationToken ct = default) =>
                Task.FromException<string>(ex);
        }

        // -------------------------------------------------------------------------
        // GetDiffAsync
        // -------------------------------------------------------------------------

        [Fact]
        public async Task GetDiffAsync_PassesCorrectArguments()
        {
            var runner = new FakeGitRunner(string.Empty);
            var service = new GitService(runner);

            await service.GetDiffAsync("/project");

            Assert.Equal("diff HEAD", runner.LastArguments);
            Assert.Equal("/project", runner.LastWorkingDirectory);
        }

        [Fact]
        public async Task GetDiffAsync_ReturnsEmptyDiff_WhenNoChanges()
        {
            var service = new GitService(new FakeGitRunner(string.Empty));

            var result = await service.GetDiffAsync("/project");

            Assert.True(result.IsEmpty);
        }

        // -------------------------------------------------------------------------
        // GetBranchDiffAsync
        // -------------------------------------------------------------------------

        [Fact]
        public async Task GetBranchDiffAsync_UsesMainAsDefaultBase()
        {
            var runner = new FakeGitRunner(string.Empty);
            var service = new GitService(runner);

            await service.GetBranchDiffAsync("/project");

            Assert.Equal("diff main...HEAD", runner.LastArguments);
        }

        [Fact]
        public async Task GetBranchDiffAsync_UsesProvidedBaseBranch()
        {
            var runner = new FakeGitRunner(string.Empty);
            var service = new GitService(runner);

            await service.GetBranchDiffAsync("/project", "develop");

            Assert.Equal("diff develop...HEAD", runner.LastArguments);
        }

        // -------------------------------------------------------------------------
        // GetCurrentBranchAsync
        // -------------------------------------------------------------------------

        [Fact]
        public async Task GetCurrentBranchAsync_ReturnsTrimmedBranchName()
        {
            var service = new GitService(new FakeGitRunner("feature/my-branch\n"));

            var branch = await service.GetCurrentBranchAsync("/project");

            Assert.Equal("feature/my-branch", branch);
        }

        // -------------------------------------------------------------------------
        // GetChangedFilesAsync
        // -------------------------------------------------------------------------

        [Fact]
        public async Task GetChangedFilesAsync_ReturnsParsedFilePaths()
        {
            var output = "src/FileA.cs\nsrc/FileB.cs\n";
            var service = new GitService(new FakeGitRunner(output));

            var files = await service.GetChangedFilesAsync("/project");

            Assert.Equal(2, files.Count);
            Assert.Contains("src/FileA.cs", files);
            Assert.Contains("src/FileB.cs", files);
        }

        [Fact]
        public async Task GetChangedFilesAsync_ReturnsEmpty_WhenNoFiles()
        {
            var service = new GitService(new FakeGitRunner(string.Empty));

            var files = await service.GetChangedFilesAsync("/project");

            Assert.Empty(files);
        }

        // -------------------------------------------------------------------------
        // Error handling
        // -------------------------------------------------------------------------

        [Fact]
        public async Task GetDiffAsync_Propagates_GitException()
        {
            var service = new GitService(
                new ThrowingGitRunner(new GitException("not a git repository")));

            await Assert.ThrowsAsync<GitException>(() =>
                service.GetDiffAsync("/not-a-repo"));
        }
    }
}
