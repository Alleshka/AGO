using System.Runtime.CompilerServices;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Ago.Core.Config
{
    public class ConfigService
    {
        private const string ConfigFileName = AgoConstants.ConfigFileName;
        private const string AgoFolderName = AgoConstants.AgoFolderName;

        private readonly ISerializer _serializer;
        private readonly IDeserializer _deserializer;

        public ConfigService()
        {
            _serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
        }

        public AgoConfig Load(string projectRoot)
        {
            var path = ConfigFilePath(projectRoot);

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Config not found: {path}. Run 'ago init'.");
            }

            var yaml = File.ReadAllText(path);
            var config = _deserializer.Deserialize<AgoConfig>(yaml);

            var variables = LoadEnvVariables(projectRoot);
            ResolveEnvVariables(config, variables);

            return config;
        }

        public void Save(AgoConfig config, string projectRoot)
        {
            EnsureAgoFolder(projectRoot);

            var path = ConfigFilePath(projectRoot);
            var yaml = _serializer.Serialize(config);
            File.WriteAllText(path, yaml);
        }

        private IReadOnlyDictionary<string, string> LoadEnvVariables(string projectRoot)
        {
            var path = Path.Combine(projectRoot, ".env");
            if (!File.Exists(path)) return new Dictionary<string, string>();

            return File.ReadAllLines(path, Encoding.UTF8)
                .Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("#"))
                .Select(line => line.Split("=", 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());
        }

        /// <summary>
        /// Creates .ago.yml and .ago/ folder. Throws if config already exists.
        /// </summary>
        public AgoConfig Init(string projectRoot, string projectName, string language = "csharp")
        {
            var path = ConfigFilePath(projectRoot);

            if (File.Exists(path))
            {
                throw new InvalidOperationException(
                    $"Project already initialised: {path}");
            }

            var config = CreateDefault(projectName, language);
            Save(config, projectRoot);
            EnsureAgoFolder(projectRoot);

            return config;
        }

        public static string ConfigFilePath(string projectRoot) =>
            Path.Combine(projectRoot, ConfigFileName);

        public static string AgoFolderPath(string projectRoot) =>
            Path.Combine(projectRoot, AgoFolderName);

        private static void EnsureAgoFolder(string projectRoot)
        {
            var folder = AgoFolderPath(projectRoot);
            Directory.CreateDirectory(folder);

            AddToGitIgnore(projectRoot);
        }

        private static AgoConfig CreateDefault(string projectName, string language) => new()
        {
            Project = new ProjectConfig
            {
                Name = projectName,
                Language = language,
                TestFramework = "xunit",
            },
            Agents = new Dictionary<string, AgentConfig>()
            {
                [AgoConstants.AgentIds.StyleReview] = new()
                {
                    Enabled = true,
                    Provider = null,
                },
                [AgoConstants.AgentIds.PerformanceReview] = new()
                {
                    Enabled = true,
                    Provider = null,
                },
                [AgoConstants.AgentIds.SecurityReview] = new()
                {
                    Enabled = true,
                    Provider = null,
                },
                [AgoConstants.AgentIds.TestGeneration] = new()
                {
                    Enabled = false,
                    Provider = null,
                },
                [AgoConstants.AgentIds.DocWriter] = new()
                {
                    Enabled = false,
                    Provider = null,
                },
            },
            Llm = new LlmConfig
            {
                Default = "ollama",
                Fallback = null,
                Providers = new Dictionary<string, LlmProviderConfig>
                {
                    [AgoConstants.ModelNames.Ollama] = new()
                    {
                        Model = AgoConstants.DefaultsProviderConfigs.OllamaProviderConfig.Model, //"qwen2.5-coder:7b",
                        BaseUrl = AgoConstants.DefaultsProviderConfigs.OllamaProviderConfig.BaseUrl // "http://localhost:11434",
                    },
                    [AgoConstants.ModelNames.Anthropic] = new()
                    {
                        Model = AgoConstants.DefaultsProviderConfigs.OllamaProviderConfig.Model, // "claude-sonnet-4",
                        ApiKey = "${AGO_ANTHROPIC_KEY}",
                    },
                    ["openai"] = new()
                    {
                        Model = "gpt-4o",
                        ApiKey = "${AGO_OPENAI_KEY}",
                    },
                    ["openrouter"] = new()
                    {
                        Model = "meta-llama/llama-3.1-8b-instruct:free",
                        ApiKey = "${AGO_OPENROUTER_KEY}",
                        BaseUrl = "https://openrouter.ai/api/v1",
                    },
                },
            },
            Ignore = new List<string>
        {
            "**/*.generated.cs",
            "**/Migrations/**",
            "**/bin/**",
            "**/obj/**",
        },
            Presets = new Dictionary<string, List<string>>
            {
                ["check"] = new() { "review" },
                ["full"] = new() { "review", "tests", "docs" },
                ["ci"] = new() { "review", "security" },
            },
        };

        /// <summary>
        /// Replaces ${ENV_VAR} placeholders with actual environment variable values.
        /// </summary>
        private static void ResolveEnvVariables(AgoConfig config, IReadOnlyDictionary<string, string> env)
        {
            foreach (var provider in config.Llm.Providers.Values)
            {
                if (provider.ApiKey is not null)
                    provider.ApiKey = ResolveEnvValue(provider.ApiKey, env);

                if (!string.IsNullOrEmpty(provider.BaseUrl))
                    provider.BaseUrl = ResolveEnvValue(provider.BaseUrl, env);
            }
        }

        private static string ResolveEnvValue(string value, IReadOnlyDictionary<string, string> env)
        {
            if (!value.StartsWith("${") || !value.EndsWith("}"))
                return value;

            var envName = value[2..^1];

            return env.TryGetValue(envName, out var envValue)
                ? envValue
                : Environment.GetEnvironmentVariable(envName) ?? value;
        }

        private static void AddToGitIgnore(string projectRoot)
        {
            var gitignorePath = Path.Combine(projectRoot, ".gitignore");
            const string entry = ".ago/";

            // File doesn't exist yet — create it
            if (!File.Exists(gitignorePath))
            {
                File.WriteAllText(gitignorePath, $"{entry}\n");
                return;
            }

            // Already has the entry — do nothing
            var lines = File.ReadAllLines(gitignorePath);
            if (lines.Any(l => l.Trim() == entry))
                return;

            // Append the entry
            File.AppendAllText(gitignorePath, $"\n{entry}\n");
        }
    }
}
