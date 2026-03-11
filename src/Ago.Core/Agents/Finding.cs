namespace Ago.Core.Agents
{
    public record Finding
    {
        public required string AgentId { get; init; }
        public required string FilePath { get; init; }

        public int LineStart { get; init; }
        public int LineEnd { get; init; }

        public required FindingType Type { get; init; }
        public required string Description { get; init; }

        //public string? ProposedChange { get; init; }
        public Priority Priority { get; init; } = Priority.Medium;
    }
}
