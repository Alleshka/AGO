using Ago.Core.Git;
using Ago.Core.Git.Diff;

namespace Ago.Core.Tests
{
    public class GitDiffParserTests
    {
        private const string SingleFileModifiedDiff = """
        diff --git a/src/UserService.cs b/src/UserService.cs
        index abc1234..def5678 100644
        --- a/src/UserService.cs
        +++ b/src/UserService.cs
        @@ -10,7 +10,9 @@ namespace MyApp
             public class UserService
             {
        -        public User GetUser(int id)
        +        public async Task<User> GetUserAsync(int id)
                 {
        +            await Task.Delay(1);
                     return new User(id);
                 }
             }
        """;

        private const string NewFileDiff = """
        diff --git a/src/NewClass.cs b/src/NewClass.cs
        new file mode 100644
        index 0000000..abc1234
        --- /dev/null
        +++ b/src/NewClass.cs
        @@ -0,0 +1,5 @@
        +namespace MyApp;
        +
        +public class NewClass
        +{
        +}
        """;

        private const string DeletedFileDiff = """
        diff --git a/src/OldClass.cs b/src/OldClass.cs
        deleted file mode 100644
        index abc1234..0000000
        --- a/src/OldClass.cs
        +++ /dev/null
        @@ -1,5 +0,0 @@
        -namespace MyApp;
        -
        -public class OldClass
        -{
        -}
        """;

        private const string RenamedFileDiff = """
        diff --git a/src/OldName.cs b/src/NewName.cs
        rename from src/OldName.cs
        rename to src/NewName.cs
        """;

        private const string MultipleFilesDiff = """
        diff --git a/src/FileA.cs b/src/FileA.cs
        index abc..def 100644
        --- a/src/FileA.cs
        +++ b/src/FileA.cs
        @@ -1,3 +1,4 @@
         namespace MyApp;
        +// added comment
         public class FileA { }
        diff --git a/src/FileB.cs b/src/FileB.cs
        index abc..def 100644
        --- a/src/FileB.cs
        +++ b/src/FileB.cs
        @@ -1,3 +1,3 @@
         namespace MyApp;
        -public class FileB { }
        +public class FileBRenamed { }
        """;

        [Fact]
        public void Parse_ReturnsEmpty_WhenDiffIsEmpty()
        {
            var result = GitDiffParser.Parse(string.Empty);

            Assert.True(result.IsEmpty);
            Assert.Empty(result.Files);
        }

        [Fact]
        public void Parse_ReturnsEmpty_WhenDiffIsWhitespace()
        {
            var result = GitDiffParser.Parse("   \n  ");

            Assert.True(result.IsEmpty);
        }

        [Fact]
        public void Parse_ReturnsSingleFile_ForModifiedDiff()
        {
            var result = GitDiffParser.Parse(SingleFileModifiedDiff);

            Assert.Single(result.Files);
            Assert.Equal("src/UserService.cs", result.Files[0].Path);
            Assert.Equal(FileChangeType.Modified, result.Files[0].ChangeType);
        }

        [Fact]
        public void Parse_ParsesHunk_WithCorrectLineNumbers()
        {
            var result = GitDiffParser.Parse(SingleFileModifiedDiff);
            var hunk = result.Files[0].Hunks[0];

            Assert.Equal(10, hunk.OldStart);
            Assert.Equal(7, hunk.OldCount);
            Assert.Equal(10, hunk.NewStart);
            Assert.Equal(9, hunk.NewCount);
        }

        [Fact]
        public void Parse_CorrectlyClassifiesAddedAndRemovedLines()
        {
            var result = GitDiffParser.Parse(SingleFileModifiedDiff);
            var file = result.Files[0];

            Assert.Equal(2, file.AddedLines.Count());
            Assert.Equal(1, file.RemovedLines.Count());
        }

        [Fact]
        public void Parse_AddedLine_HasCorrectContent()
        {
            var result = GitDiffParser.Parse(SingleFileModifiedDiff);
            var added = result.Files[0].AddedLines.First();

            Assert.Contains("async Task<User> GetUserAsync", added.Content);
        }

        [Fact]
        public void Parse_RemovedLine_HasCorrectContent()
        {
            var result = GitDiffParser.Parse(SingleFileModifiedDiff);
            var removed = result.Files[0].RemovedLines.First();

            Assert.Contains("public User GetUser(int id)", removed.Content);
        }

        // -------------------------------------------------------------------------
        // File change types
        // -------------------------------------------------------------------------

        [Fact]
        public void Parse_DetectsNewFile()
        {
            var result = GitDiffParser.Parse(NewFileDiff);

            Assert.Equal(FileChangeType.Added, result.Files[0].ChangeType);
        }

        [Fact]
        public void Parse_NewFile_AllLinesAreAdded()
        {
            var result = GitDiffParser.Parse(NewFileDiff);
            var file = result.Files[0];

            Assert.All(file.Hunks.SelectMany(h => h.Lines),
                line => Assert.Equal(DiffLineType.Added, line.Type));
        }

        [Fact]
        public void Parse_DetectsDeletedFile()
        {
            var result = GitDiffParser.Parse(DeletedFileDiff);

            Assert.Equal(FileChangeType.Deleted, result.Files[0].ChangeType);
        }

        [Fact]
        public void Parse_DeletedFile_AllLinesAreRemoved()
        {
            var result = GitDiffParser.Parse(DeletedFileDiff);
            var file = result.Files[0];

            Assert.All(file.Hunks.SelectMany(h => h.Lines),
                line => Assert.Equal(DiffLineType.Removed, line.Type));
        }

        [Fact]
        public void Parse_DetectsRenamedFile()
        {
            var result = GitDiffParser.Parse(RenamedFileDiff);

            Assert.Equal(FileChangeType.Renamed, result.Files[0].ChangeType);
            Assert.Equal("src/OldName.cs", result.Files[0].OldPath);
            Assert.Equal("src/NewName.cs", result.Files[0].Path);
        }

        // -------------------------------------------------------------------------
        // Multiple files
        // -------------------------------------------------------------------------

        [Fact]
        public void Parse_ReturnsAllFiles_ForMultiFileDiff()
        {
            var result = GitDiffParser.Parse(MultipleFilesDiff);

            Assert.Equal(2, result.Files.Count);
        }

        [Fact]
        public void Parse_EachFile_HasCorrectPath()
        {
            var result = GitDiffParser.Parse(MultipleFilesDiff);

            Assert.Equal("src/FileA.cs", result.Files[0].Path);
            Assert.Equal("src/FileB.cs", result.Files[1].Path);
        }

        [Fact]
        public void Parse_PreservesRawDiff()
        {
            var result = GitDiffParser.Parse(SingleFileModifiedDiff);

            Assert.Equal(SingleFileModifiedDiff, result.RawDiff);
        }
    }
}
