using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ago.Core.Git.Diff
{
    public record DiffLine
    {
        public DiffLineType Type { get; init; }
        public string Content { get; init; } = string.Empty;
        public int? OldLineNumber { get; init; }
        public int? NewLineNumber { get; init; }
    }
}
