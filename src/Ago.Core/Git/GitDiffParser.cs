using Ago.Core.Git.Diff;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ago.Core.Git
{
    public static class GitDiffParser
    {
        private static readonly Regex HunkHeaderRegex =
            new(@"^@@ -(?<oldStart>\d+)(?:,(?<oldCount>\d+))? \+(?<newStart>\d+)(?:,(?<newCount>\d+))? @@(?<header>.*)$",
            RegexOptions.Compiled);

        private static readonly Regex DiffHeaderRegex =
            new(@"^diff --git a/(?<oldPath>.+) b/(?<newPath>.+)$",
            RegexOptions.Compiled);

        public static DiffResult Parse(string rawDiff)
        {
            if (string.IsNullOrWhiteSpace(rawDiff))
                return new DiffResult { RawDiff = rawDiff };

            var files = new List<FileDiff>();
            var lines = rawDiff
                .Split('\n')
                .Select(l => l.TrimEnd('\r'))
                .ToArray();

            var i = 0;
            while (i < lines.Length)
            {
                if (lines[i].StartsWith("diff --git"))
                {
                    var (fileDiff, nextIndex) = ParseFileDiff(lines, i);
                    files.Add(fileDiff);
                    i = nextIndex;
                }
                else
                {
                    i++;
                }
            }

            return new DiffResult { Files = files, RawDiff = rawDiff };
        }

        private static (FileDiff, int nextIndex) ParseFileDiff(string[] lines, int startIndex)
        {
            var headerMatch = DiffHeaderRegex.Match(lines[startIndex]);
            var oldPath = headerMatch.Success ? headerMatch.Groups["oldPath"].Value : string.Empty;
            var newPath = headerMatch.Success ? headerMatch.Groups["newPath"].Value : string.Empty;

            var changeType = FileChangeType.Modified;
            string? renamedFrom = null;

            var i = startIndex + 1;

            while (i < lines.Length && !lines[i].StartsWith("@@") && !lines[i].StartsWith("diff --git"))
            {
                if (lines[i].StartsWith("new file"))
                {
                    changeType = FileChangeType.Added;
                }
                else if (lines[i].StartsWith("deleted file"))
                {
                    changeType = FileChangeType.Deleted;
                }
                else if (lines[i].StartsWith("rename from"))
                {
                    changeType = FileChangeType.Renamed;
                    renamedFrom = lines[i]["rename from ".Length..].Trim();
                }
                i++;
            }

            var hunks = new List<DiffHunk>();
            while (i < lines.Length && !lines[i].StartsWith("diff --git"))
            {
                if (lines[i].StartsWith("@@"))
                {
                    var (hunk, nextI) = ParseHunk(lines, i);
                    hunks.Add(hunk);
                    i = nextI;
                }
                else
                {
                    i++;
                }
            }

            var fileDiff = new FileDiff
            {
                Path = newPath,
                OldPath = changeType == FileChangeType.Renamed ? renamedFrom : null,
                ChangeType = changeType,
                Hunks = hunks,
            };

            return (fileDiff, i);
        }

        private static (DiffHunk, int nextIndex) ParseHunk(string[] lines, int startIndex)
        {
            var match = HunkHeaderRegex.Match(lines[startIndex]);

            var oldStart = int.Parse(match.Groups["oldStart"].Value);
            var oldCount = match.Groups["oldCount"].Success ? int.Parse(match.Groups["oldCount"].Value) : 1;
            var newStart = int.Parse(match.Groups["newStart"].Value);
            var newCount = match.Groups["newCount"].Success ? int.Parse(match.Groups["newCount"].Value) : 1;
            var header = match.Groups["header"].Value.Trim();

            var diffLines = new List<DiffLine>();
            var oldLine = oldStart;
            var newLine = newStart;
            var i = startIndex + 1;

            while (i < lines.Length && !lines[i].StartsWith("@@") && !lines[i].StartsWith("diff --git"))
            {
                var raw = lines[i];

                if (raw.StartsWith('+'))
                {
                    diffLines.Add(new DiffLine
                    {
                        Type = DiffLineType.Added,
                        Content = raw[1..],
                        NewLineNumber = newLine++,
                    });
                }
                else if (raw.StartsWith('-'))
                {
                    diffLines.Add(new DiffLine
                    {
                        Type = DiffLineType.Removed,
                        Content = raw[1..],
                        OldLineNumber = oldLine++,
                    });
                }
                else if (raw.StartsWith(' ') || raw == string.Empty)
                {
                    diffLines.Add(new DiffLine
                    {
                        Type = DiffLineType.Context,
                        Content = raw.Length > 0 ? raw[1..] : string.Empty,
                        OldLineNumber = oldLine++,
                        NewLineNumber = newLine++,
                    });
                }

                i++;
            }

            var hunk = new DiffHunk
            {
                OldStart = oldStart,
                OldCount = oldCount,
                NewStart = newStart,
                NewCount = newCount,
                Header = header,
                Lines = diffLines,
            };

            return (hunk, i);
        }
    }
}
