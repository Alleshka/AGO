namespace Ago.Core.Config
{
    public static class ProjectResolver
    {
        private const string ConfigFileName = AgoConstants.ConfigFileName;

        /// <summary>
        /// Walks up the directory tree from <paramref name="startPath"/> (or
        /// current directory) until it finds a folder containing .ago.yml.
        /// Returns the project root, or null if not found.
        /// </summary>
        public static string? ResolveProjectRoot(string? explicitPath = null, string? startPath = null)
        {
            // Explicit path provided — use it directly
            if (explicitPath is not null)
            {
                return File.Exists(Path.Combine(explicitPath, ConfigFileName))
                    ? explicitPath
                    : null;
            }

            // Walk upward from startPath (injectable for testing) or CWD
            var current = startPath ?? Directory.GetCurrentDirectory();

            while (current is not null)
            {
                if (File.Exists(Path.Combine(current, ConfigFileName)))
                    return current;

                current = Directory.GetParent(current)?.FullName;
            }

            return null;
        }
    }
}
