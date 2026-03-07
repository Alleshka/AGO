using Ago.Core.Config;

namespace Ago.Core.Tests
{
    public class ProjectResolverTests : IDisposable
    {
        // Each test gets its own temp directory tree — isolated, no shared state
        private readonly string _root;

        public ProjectResolverTests()
        {
            _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_root);
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);

        private string CreateDir(params string[] parts)
        {
            var path = Path.Combine(new[] { _root }.Concat(parts).ToArray());
            Directory.CreateDirectory(path);
            return path;
        }

        private void PlaceConfig(string dir)
        {
            File.WriteAllText(Path.Combine(dir, ".ago.yml"), "version: '1.0'");
        }

        [Fact]
        public void ReturnsRoot_WhenConfigIsInStartDirectory()
        {
            PlaceConfig(_root);

            var result = ProjectResolver.ResolveProjectRoot(startPath: _root);

            Assert.Equal(_root, result);
        }

        [Fact]
        public void ReturnsRoot_WhenStartedFromNestedChildDirectory()
        {
            // .ago.yml is at root, we start from root/src/services
            PlaceConfig(_root);
            var nested = CreateDir("src", "services");

            var result = ProjectResolver.ResolveProjectRoot(startPath: nested);

            Assert.Equal(_root, result);
        }

        [Fact]
        public void ReturnsRoot_WhenStartedFromDeeplyNestedDirectory()
        {
            PlaceConfig(_root);
            var deep = CreateDir("src", "core", "agents", "review");

            var result = ProjectResolver.ResolveProjectRoot(startPath: deep);

            Assert.Equal(_root, result);
        }

        [Fact]
        public void ReturnsNull_WhenNoConfigExistsAnywhere()
        {
            // No .ago.yml created — should return null
            var result = ProjectResolver.ResolveProjectRoot(startPath: _root);

            Assert.Null(result);
        }

        [Fact]
        public void ReturnsNearestConfig_WhenMultipleConfigsInTree()
        {
            // Nested project takes priority over parent project
            PlaceConfig(_root);
            var nested = CreateDir("subproject");
            PlaceConfig(nested);

            var result = ProjectResolver.ResolveProjectRoot(startPath: nested);

            Assert.Equal(nested, result);
        }

        [Fact]
        public void ReturnsExplicitPath_WhenExplicitPathProvided()
        {
            PlaceConfig(_root);

            var result = ProjectResolver.ResolveProjectRoot(explicitPath: _root);

            Assert.Equal(_root, result);
        }

        [Fact]
        public void ReturnsNull_WhenExplicitPathHasNoConfig()
        {
            // Explicit path exists as directory but has no .ago.yml
            var result = ProjectResolver.ResolveProjectRoot(explicitPath: _root);

            Assert.Null(result);
        }

        [Fact]
        public void ExplicitPath_TakesPrecedenceOverStartPath()
        {
            // Even if startPath has a config, explicit path is used
            PlaceConfig(_root);
            var other = CreateDir("other");
            PlaceConfig(other);

            var result = ProjectResolver.ResolveProjectRoot(
                explicitPath: other,
                startPath: _root);

            Assert.Equal(other, result);
        }
    }
}
