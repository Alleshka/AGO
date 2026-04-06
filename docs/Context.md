# AGO — Project Context File
> Use this file as context when asking an LLM about specific implementation questions.
> How to use: provide the entire file + ask a specific question.

---

## What is AGO

A CLI tool and multi-agent orchestrator for code analysis and improvement.
Named `ago` (working title).
Implementation language: **C#**
Target audience: developers using the tool locally and via GitHub PRs.

---

## Core Architecture Principles

1. **Core is the single source of business logic.** CLI, GitHub Bot, and GUI are thin wrappers over Core.
2. **Agents only read, never write files directly.** They return `Finding[]`.
3. **Phases are strictly separated:** Analyse (parallel) → Plan/Merge (single agent) → Apply (single thread).
4. **RunOptions is the single entry point into the Orchestrator** regardless of source (flags, command, preset, bot).
5. **Upward search for `.ago.yml`** — same as `.git` in git. No "open project" command needed. `.ago/` folder lives alongside it and holds all runtime data (history, cache, index).

---
## LLM Cache

AGO uses a transparent caching layer to reduce repeated LLM calls and cost.

### Goals
- Avoid repeated identical LLM calls
- Reduce latency and cost
- Keep behaviour deterministic per configuration

### Cache Key

Cache key is a hash of the full request context:

- provider
- model
- temperature (and other sampling params)
- system prompt
- messages (full content)
- agentId (optional but recommended)

Example:

```csharp
var cachePayload = new
{
    Provider = provider,
    Model = model,
    Temperature = temperature,
    Messages = messages,
    Agent = agentId
};
```


Serialized and hashed via SHA256. 

### Storage

Default: local filesystem

```
.ago/cache/llm/
  ├── ab12cd34.json
  ├── ef56gh78.json
```

### Cache Entry
```
{  
"content": "...",  
"model": "claude-sonnet-4",  
"createdAt": "2026-03-07T12:00:00Z"  
}
```

### Rules
- Only successful responses are cached
- Errors are never cached
- Cache is automatically invalidated when:
	- model changes
	- provider changes
	- prompt changes

### TTL
Configurable (default: 24h)

### Future Extensions
- Semantic cache (embedding similarity)
- Partial cache reuse for large files

---

## Solution Structure

```
ago/
├── .git/
├── .ago/                            # gitignore entire folder
│   ├── history.jsonl                # append-only run history
│   ├── index.json                   # class/file cache (Phase 2)
│   ├── cache/                       # LLM cache (hashed requests)
│   └── prompts/                     # personal prompt overrides (gitignore)
│       ├── style-review.md          # overrides team + built-in prompt
│       └── explainer.md
├── .ago-prompts/                    # team prompt overrides — committed to git
│   ├── style-review.md
│   └── explainer.md
├── .ago.yml                         # project config — committed to git
├── ago.sln
│
├── src/
│   ├── Ago.Core.Abstractions/       # Lightweight plugin SDK — interfaces only
│   │   ├── IAgent.cs
│   │   ├── ICodeIndex.cs
│   │   ├── Finding.cs
│   │   ├── AnalysisContext.cs
│   │   ├── AgentResult.cs
│   │   └── ChatMessage.cs
│   │
│   ├── Ago.Core/                    # All business logic
│   │   ├── Agents/
│   │   │   ├── IAgent.cs
│   │   │   ├── LlmAgentBase.cs
│   │   │   ├── AgentResult.cs
│   │   │   ├── Finding.cs
│   │   │   ├── AnalysisContext.cs
│   │   │   ├── Prompts/
│   │   │   │   └── PromptResolver.cs    # personal → team → built-in
│   │   │   ├── Plugins/
│   │   │   │   ├── PluginLoader.cs      # scans .ago-plugins/, loads DLLs + prompt-only
│   │   │   │   ├── PluginLoadContext.cs # AssemblyLoadContext for isolation
│   │   │   │   └── PromptOnlyAgent.cs  # generic IAgent driven by .md files
│   │   │   ├── CodeReview/
│   │   │   │   ├── StyleReviewAgent.cs
│   │   │   │   ├── PerformanceReviewAgent.cs
│   │   │   │   └── SecurityReviewAgent.cs
│   │   │   ├── Explainer/
│   │   │   │   └── ExplainerAgent.cs
│   │   │   ├── TestGeneration/
│   │   │   │   └── TestGeneratorAgent.cs
│   │   │   ├── DocWriter/
│   │   │   │   └── DocWriterAgent.cs
│   │   │   ├── Merge/
│   │   │   │   └── MergeAgent.cs
│   │   │   ├── Planner/
│   │   │   │   └── PlannerAgent.cs
│   │   │   └── Dispatcher/
│   │   │       └── DispatcherAgent.cs
│   │   ├── Orchestrator/
│   │   │   ├── Orchestrator.cs
│   │   │   ├── RunOptions.cs
│   │   │   └── ExecutionPlan.cs
│   │   ├── LLM/
│   │   │   ├── Cache/  
│   │   │   │   ├── ILlmCache.cs  
│   │   │   │   ├── FileLlmCache.cs  
│   │   │   │   └── CacheKeyBuilder.cs
│   │   │   ├── IChatClient.cs
│   │   │   ├── ChatMessage.cs
│   │   │   ├── ChatResponse.cs
│   │   │   ├── LlmProviderFactory.cs
│   │   │   ├── AnthropicClient.cs
│   │   │   ├── OpenAiClient.cs
│   │   │   ├── OllamaClient.cs
│   │   │   └── OpenRouterClient.cs
│   │   ├── Config/
│   │   │   ├── AgoConfig.cs
│   │   │   └── ConfigService.cs
│   │   ├── Git/
│   │   │   ├── GitService.cs
│   │   │   ├── GitDiffParser.cs
│   │   │   └── DiffResult.cs
│   │   ├── Parsing/
│   │   │   └── CSharpParser.cs      # Roslyn
│   │   ├── Index/
│   │   │   ├── ICodeIndex.cs
│   │   │   └── RoslynCodeIndex.cs   # Phase 1-2: traversal
│   │   │   # SemanticCodeIndex.cs   # Phase 3+: embeddings (add later)
│   │   ├── Writer/
│   │   │   └── CodeWriter.cs
│   │   └── History/
│   │       └── HistoryService.cs
│   │
│   ├── Ago.Cli/                     # CLI wrapper
│   │   ├── Program.cs
│   │   └── Commands/
│   │       ├── InitCommand.cs
│   │       ├── RunCommand.cs
│   │       ├── StatusCommand.cs
│   │       └── UiCommand.cs
│   │
│   ├── Ago.Bot/                     # GitHub Bot wrapper
│   │   ├── Program.cs
│   │   ├── Webhooks/
│   │   │   └── PullRequestHandler.cs
│   │   ├── GitHub/
│   │   │   ├── GitHubClientWrapper.cs
│   │   │   └── PrCommentService.cs
│   │   └── appsettings.json
│   │
│   └── Ago.Api/                     # Web API for GUI
│       ├── Program.cs
│       └── Endpoints/
│           ├── ReviewEndpoints.cs
│           └── TestEndpoints.cs
│
├── tests/
│   ├── Ago.Core.Tests/
│   └── Ago.Bot.Tests/
│
├── frontend/                        # React GUI (Phase 4)
│   └── src/
│
└── README.md
```

---

## Key Models

### AgoConfig (.ago.yml — committed to git)
```yaml
version: "1.0"
project:
  name: "MyProject"
  language: "csharp"
  testFramework: "xunit"
agents:
  codeReview:
    enabled: true
    style: true
    performance: true
    security: true
  testGeneration:
    enabled: true
  docWriter:
    enabled: false
ignore:
  - "**/*.generated.cs"
  - "**/Migrations/**"
llm:
  provider: "anthropic"
  model: "claude-sonnet-4"
presets:
  check:
    - review
  full:
    - review
    - tests
    - docs
  ci:
    - review
    - security
```

### LLM Strategy Resolution  
The provider used for a request is resolved as:  
1. Agent.provider (if set)  
2. Agent.strategy (if set)  
3. Global llm.strategy  
4. Global llm.default

### Finding (agent output)
```csharp
public record Finding
{
    public string AgentId { get; init; }
    public string FilePath { get; init; }
    public int LineStart { get; init; }
    public int LineEnd { get; init; }
    public FindingType Type { get; init; }   // Fix | Suggestion | Warning | Info
    public string Description { get; init; }
    public string? ProposedChange { get; init; }
    public Priority Priority { get; init; }  // High | Medium | Low
}
```

### RunOptions (single entry point into Orchestrator)
```csharp
public class RunOptions
{
    public bool Review { get; set; }
    public bool Tests { get; set; }
    public bool Docs { get; set; }
    public bool Fix { get; set; }
    public bool DryRun { get; set; }
    public Scope Scope { get; set; }         // Diff | File | Class | All
    public string? FilePath { get; set; }
    public string? ClassName { get; set; }
    public string? ProjectRoot { get; set; }
}

public enum Scope { Diff, File, Class, All }
```

### IAgent
```csharp
public interface IAgent
{
    string Id { get; }
    Task<AgentResult> AnalyseAsync(AnalysisContext context, CancellationToken ct);
}

public record AnalysisContext
{
    public string ProjectRoot { get; init; }
    public AgoConfig Config { get; init; }
    public DiffResult? Diff { get; init; }
    public string? FilePath { get; init; }
    public string? ClassName { get; init; }
}

public record AgentResult
{
    public string AgentId { get; init; }
    public Finding[] Findings { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
}
```

---

## How the Orchestrator Works

```
RunOptions → Orchestrator
  1. ResolveProjectRoot()
  2. Load AgoConfig
  3. Build agent list from RunOptions flags
  4. ANALYSE PHASE: 
     For each agent:  
		- Build prompt  
		- Check LLM cache  
		- If hit → return cached result  
		- Else → call LLM → store in cache
  5. Aggregate all Findings
  6. If RunOptions.Fix:
       PLAN PHASE: MergeAgent.ResolveConflicts(findings) → UnifiedPatch
       APPLY PHASE: CodeWriter.Apply(patch)
  7. If RunOptions.DryRun: show what would change without applying
  8. Save to history
  9. Output report
```

---

## Conflict Resolution (MergeAgent)

Two agents want to modify the same line → conflict:
```
Finding A: line 42, StyleAgent,  Priority.Low
Finding B: line 42, PerfAgent,   Priority.High
```

Strategies (configurable in .ago):
- `priority-wins` — apply the finding with higher Priority
- `llm-merge` — send both findings to LLM, which applies both changes together
- `sequential` — apply A first, then apply B to the already-modified code

---

## Project Resolution (upward search)

```csharp
public static string? ResolveProjectRoot(string? explicitPath = null)
{
    if (explicitPath != null) return explicitPath;

    var current = Directory.GetCurrentDirectory();
    while (current != null)
    {
        if (File.Exists(Path.Combine(current, ".ago.yml")))
            return current;
        current = Directory.GetParent(current)?.FullName;
    }
    return null; // → error: "not an ago project. Run ago init"
}
```

---

## CLI Command Syntax

```bash
# Initialization
ago init                                    # create .ago.yml and .ago/ in current directory

# Running via flags (Stage 1)
ago run --review --diff                     # review the diff
ago run --review --file src/UserService.cs  # review a specific file
ago run --tests --diff                      # generate tests for diff
ago run --tests --class UserService         # generate tests for a class
ago run --docs --diff                       # generate docs for diff
ago run --review --tests --fix --diff       # everything + apply fixes
ago run --review --dry-run --diff           # show changes without applying

# Built-in command aliases (Stage 2)
ago check                                   # = --review --diff
ago fix                                     # = --review --fix --diff
ago full                                    # = --review --tests --docs --fix --diff

# Presets from .ago config (Stage 2)
ago run --preset ci --diff

# Semi-auto — PlannerAgent suggests a plan (Stage 3)
ago run --auto

# Explicit project path
ago -C ~/other-project run --review --diff

# Utilities
ago status                                  # show project config
ago report --last                           # last report
ago report --all                            # full history
ago ui                                      # start local web server with GUI
```

---

## Input → RunOptions Mapping

All input sources (flags, built-in commands, presets) map to the same RunOptions object.
The Orchestrator never knows where the request came from.

```
CLI flags        ──┐
Built-in commands ──┼──→  RunOptions  ──→  Orchestrator
User presets     ──┘
GitHub Bot       ──┘
```

```csharp
// Built-in command alias example
"check" → new RunOptions { Review = true, Scope = Scope.Diff }
"fix"   → new RunOptions { Review = true, Fix = true, Scope = Scope.Diff }
"full"  → new RunOptions { Review = true, Tests = true, Docs = true, Fix = true, Scope = Scope.Diff }
```

---

## PlannerAgent Evolution

| Stage         | Behaviour                                                   |
| ------------- | ----------------------------------------------------------- |
| 1 — MVP       | Reads RunOptions flags, converts to agent list              |
| 2 — Semi-auto | LLM analyses diff/description, suggests plan, user confirms |
| 3 — Autopilot | LLM makes decisions without confirmation                    |

The Orchestrator does not change between stages. Only PlannerAgent changes.

---

## GitHub Bot (Phase 3)

### Architecture
```
GitHub Event (PR open/update/comment)
  → Webhook Handler (ASP.NET Core Minimal API)
  → same Core (RunOptions → Orchestrator)
  → result → GitHub API
```

### What runs automatically
On any PR open/update: all review agents (style, performance, security).

### What runs on command
User writes in a PR comment:
```
/ago tests
/ago fix
/ago full
```

### DispatcherAgent
Reads the PR description and automatically decides which additional agents to run.

### Where the bot writes changes

| Action type                   | Destination                                                     |
| ----------------------------- | --------------------------------------------------------------- |
| Review findings               | Inline comments on PR lines                                     |
| Minor auto-fixes (formatting) | Commit to the same PR tagged `[ago-bot]`                        |
| Test generation / new code    | Separate PR targeting the same branch (`ago/tests-{pr-number}`) |

---

## GUI (Phase 4)

- `ago ui` — starts a local ASP.NET Core server and opens the browser
- React frontend calls REST API implemented via the same Core
- Features: project list, run commands, history, config editor

---

## Implementation Order

### Phase 1 — Core + CLI MVP
```
1.1  Solution structure                          ✅
1.2  AgoConfig + ConfigService (YAML)            ✅
1.3  ResolveProjectRoot                          ✅
1.4  ago init                                    ✅
1.5  IChatClient + OllamaClient                  ✅
1.6  LlmProviderFactory
1.7  GitService + GitDiffParser                  ✅
1.8  IAgent + Finding + AnalysisContext models
1.9  LlmAgentBase (shared base class)
1.10 PromptResolver (personal → team → built-in)
1.11 StyleReviewAgent
1.12 PerformanceReviewAgent
1.13 SecurityReviewAgent
1.14 ExplainerAgent
1.15 Orchestrator (Analyse phase only, parallel)
1.16 ago run --review --diff / --file
1.17 ago run --explain --diff / --file / --class
1.18 MergeAgent + CodeWriter (Write phase)
1.19 ago run --fix / --dry-run flags
1.20 CSharpParser (Roslyn)
1.21 TestGeneratorAgent
1.22 ago run --tests --diff / --class
```

### Phase 2 — Extensibility
```
2.1  Built-in command aliases (ago check, ago fix, ago full)
2.2  User presets from .ago
2.3  DocWriterAgent
2.4  PlannerAgent (semi-auto — suggests a plan)
2.5  HistoryService + ago report
```

### Phase 3 — GitHub Bot
```
3.1  GitHub App registration
3.2  Webhook handler (ASP.NET Core Minimal API)
3.3  Auto-run review agents on PR
3.4  Post findings as PR review comments
3.5  Parse /ago commands from comments
3.6  Test generation → separate PR
3.7  DispatcherAgent (analyse PR description)
3.8  Dockerfile + deployment
```

### Phase 4 — GUI
```
4.1  ASP.NET Core Minimal API (ago ui command)
4.2  React frontend
4.3  Project list, run commands, history, config editor
```

---

## Technology Stack

| Component          | Technology                                    |
| ------------------ | --------------------------------------------- |
| Language           | C# / .NET 8+                                  |
| CLI framework      | System.CommandLine                            |
| YAML serialization | YamlDotNet                                    |
| C# parsing         | Microsoft.CodeAnalysis (Roslyn)               |
| LLM                | Anthropic Claude (primary), OpenAI (optional) |
| GitHub API         | Octokit.net                                   |
| Web API            | ASP.NET Core Minimal API                      |
| Frontend           | React + TypeScript                            |
| Tests              | xUnit                                         |

---

## Key Decisions and Rationale

**Why git-style CLI instead of REPL**
Easy to integrate into CI/CD pipelines, same code works in the bot, familiar to developers.

**Why agents never write files directly**
Parallel writes to the same file cause conflicts. The Analyse/Plan/Apply separation solves this cleanly.

**Why RunOptions is the single entry point**
CLI, bot, and GUI simply convert their inputs into RunOptions. The Orchestrator is source-agnostic.

**Why `.ago.yml` is committed to git but `.ago/` is not**
`.ago.yml` holds project settings (language, test framework, ignore patterns, presets) that are useful to the whole team — same pattern as `.editorconfig`, `.eslintrc`, `global.json`. `.ago/` holds runtime data (history, cache, index) that is machine-specific and can be regenerated — it belongs in `.gitignore`.

**Why a `.ago/` folder instead of flat files**
All runtime data lives in one place that can be gitignored with a single line. Mirrors the `.git/` convention and makes it easy to wipe local state with `rm -rf .ago/`.

**Why the bot creates a separate PR for code generation**
The author can review, edit, or reject the changes without affecting the main PR.

**Why prompt overrides use .md files instead of YAML config**
Prompts are long, multiline text — editing them inside YAML is awkward and error-prone. Separate `.md` files are easy to edit, diff, and review. The `{{placeholder}}` syntax keeps them readable without a templating engine.

---

## PromptResolver

Every agent has a built-in default prompt hardcoded in the class. `PromptResolver` allows overriding it at two levels without changing any agent code.

### Priority order (first match wins)

```
1. .ago/prompts/{agentId}.md        personal override  — gitignored, per-developer
2. .ago-prompts/{agentId}.md        team override      — committed to git, shared
3. built-in default                 hardcoded in agent — always available
```

### Prompt file format

Plain Markdown with named sections and placeholders:

```markdown
# system
You are an expert C# reviewer for a fintech project.
We use Result<T> pattern — flag any throws in business logic.

# user
Review this {{scope}}:

{{code}}
```

Supported placeholders:

| Placeholder    | Replaced with                                  |
| -------------- | ---------------------------------------------- |
| `{{scope}}`    | `diff` / `file src/X.cs` / `class UserService` |
| `{{code}}`     | the actual code or formatted diff              |
| `{{language}}` | value from `AgoConfig.Project.Language`        |

### Implementation

```csharp
public class PromptResolver
{
    // Priority: personal → team → built-in
    public PromptTemplate Resolve(string agentId, string projectRoot)
    {
        var personal = Path.Combine(projectRoot, ".ago", "prompts", $"{agentId}.md");
        if (File.Exists(personal))
            return PromptTemplate.FromFile(personal);

        var team = Path.Combine(projectRoot, ".ago-prompts", $"{agentId}.md");
        if (File.Exists(team))
            return PromptTemplate.FromFile(team);

        return PromptTemplate.BuiltIn(agentId);
    }
}

public class PromptTemplate
{
    public string System { get; private set; } = string.Empty;
    public string User   { get; private set; } = string.Empty;

    public IReadOnlyList<ChatMessage> Render(Dictionary<string, string> vars)
    {
        return
        [
            ChatMessage.System(Replace(System, vars)),
            ChatMessage.User(Replace(User, vars)),
        ];
    }

    private static string Replace(string template, Dictionary<string, string> vars) =>
        vars.Aggregate(template, (t, kv) => t.Replace($"{{{{{kv.Key}}}}}", kv.Value));
}
```

`LlmAgentBase` calls `PromptResolver` before sending — concrete agents no longer need `BuildPrompt()` unless they want to bypass the resolver entirely.

---

## Planned Extension: Multi-Model Debate Pattern

> This is a Phase 2+ extension. The current architecture already supports it without changes to the Orchestrator.

### What it is

Multiple LLM models solve the same task independently, then a Judge agent synthesises the best result. Based on the "Society of Mind / Debate" pattern — research shows that models acting as judges catch errors that each model missed individually.

### How it fits the current architecture

The `IAgent` abstraction already supports this. The Orchestrator does not change at all — a DebateAgent is just another `IAgent` from its perspective.

```
Current:
  StyleReviewAgent (1 model) → Finding[]

With Debate:
  StyleReviewAgent
    ├── StyleReviewAgent_Claude  → Finding[]  ──┐
    ├── StyleReviewAgent_GPT4o   → Finding[]  ──┼──→ JudgeAgent → Finding[]
    └── StyleReviewAgent_Gemini  → Finding[]  ──┘
```

### Implementation

```csharp
// Simple agent — single LLM call (current behaviour)
public class StyleReviewAgent : IAgent { ... }

// Debate agent — multiple models + judge (Phase 2+ extension)
public class DebateAgent : IAgent
{
    private readonly IAgent[] _participants;  // different models
    private readonly IAgent _judge;

    public async Task<AgentResult> AnalyseAsync(AnalysisContext context, CancellationToken ct)
    {
        // Run all participants in parallel
        var results = await Task.WhenAll(
            _participants.Select(a => a.AnalyseAsync(context, ct))
        );

        // Judge receives all results and synthesises the best one
        return await _judge.JudgeAsync(context, results, ct);
    }
}
```

### Three multi-model modes

| Mode | Description | Cost |
|------|-------------|------|
| `debate` | All models solve the same task, judge picks the best | High — best quality |
| `specialization` | Different models for different tasks (Claude for architecture, GPT-4o for tests, local model for formatting) | Low — efficient |
| `validate` | One model generates, another reviews | Medium — good second opinion |

### Config

```yaml
agents:
  codeReview:
    mode: "debate"           # single | debate | validate | specialization
    participants:
      - provider: anthropic
        model: claude-sonnet-4
      - provider: openai
        model: gpt-4o
    judge:
      provider: anthropic
      model: claude-opus-4
```

### What "orchestration" means in this context

Orchestration is defined by coordinating multiple agents with flow control, dependency management, and result aggregation — not by the number of LLMs used. The current single-LLM structure with parallel agents, phased execution (Analyse → Plan → Apply), and a MergeAgent is already full orchestration. Multiple LLMs are an enhancement, not a requirement.

---

## Planned Extension: Plugin System (Phase 5)

> User-authored agents. The current architecture already supports this — plugins register as `IAgent` entries in the same dictionary as built-in agents. The Orchestrator does not change.

### Two levels of plugins

| Level | How | Audience |
|---|---|---|
| **Prompt-only** | Folder with `.md` files, no code | Any user |
| **Code plugin** | DLL loaded via `AssemblyLoadContext` | Developers |

### Level 1 — Prompt-only plugins

User creates a folder under `.ago-plugins/` — no C# required:

```
.ago-plugins/
  my-naming-checker/
    agent.json       ← metadata
    system.md        ← system prompt
    user.md          ← user prompt with {{placeholders}}
```

```json
// agent.json
{
  "id": "my-naming-checker",
  "description": "Checks that all async methods end with Async",
  "scope": ["diff", "file"]
}
```

The plugin loader scans `.ago-plugins/`, reads each folder, and registers a `PromptOnlyAgent` — a generic `IAgent` implementation driven entirely by the prompt files. From the Orchestrator's perspective it is identical to a built-in agent.

### Level 2 — Code plugins (DLL)

Developer builds a class library referencing `Ago.Core.Abstractions` (a lightweight NuGet with only interfaces — no heavy dependencies):

```csharp
// Developer's project — references Ago.Core.Abstractions NuGet
public class MyCustomAgent : IAgent
{
    public string Id => "my-custom-agent";

    public async Task<AgentResult> AnalyseAsync(AnalysisContext context, CancellationToken ct)
    {
        // full control — custom logic, external APIs, anything
    }
}
```

Places the compiled DLL in `.ago-plugins/`. The plugin loader uses `AssemblyLoadContext` for isolation:

```csharp
public class PluginLoader
{
    public IReadOnlyList<IAgent> LoadFromFolder(string pluginsFolder)
    {
        var agents = new List<IAgent>();

        foreach (var dll in Directory.GetFiles(pluginsFolder, "*.dll"))
        {
            // Isolated context prevents version conflicts with Core dependencies
            var context  = new PluginLoadContext(dll);
            var assembly = context.LoadFromAssemblyPath(dll);

            var agentTypes = assembly.GetTypes()
                .Where(t => typeof(IAgent).IsAssignableFrom(t) && !t.IsAbstract);

            foreach (var type in agentTypes)
            {
                if (Activator.CreateInstance(type) is IAgent agent)
                    agents.Add(agent);
            }
        }

        return agents;
    }
}
```

### Ago.Core.Abstractions — the plugin SDK

To avoid forcing plugin developers to reference all of `Ago.Core` (which pulls in LLM clients, Roslyn, YamlDotNet, etc.), the public contract is extracted into a minimal NuGet package:

```
Ago.Core.Abstractions  ← IAgent, ICodeIndex, Finding, AnalysisContext,
                          ChatMessage, AgentResult — no heavy dependencies

Ago.Core               ← references Abstractions, adds all implementations
Ago.Cli                ← references Core
```

Plugin developers only reference `Ago.Core.Abstractions`. This keeps their project lightweight and decoupled from internal implementation details.

### Where plugins are discovered

```
Priority order (same as PromptResolver):
  1. .ago/plugins/          personal plugins — gitignored
  2. .ago-plugins/          team plugins     — committed to git
```

### How it fits the current architecture

The Orchestrator holds a `Dictionary<string, IAgent>`. Built-in agents are registered at startup. Plugin agents are loaded by `PluginLoader` and added to the same dictionary. No other code changes.

```csharp
// Startup — same dictionary, different sources
var agents = new Dictionary<string, IAgent>();

// Built-in
agents[AgoConstants.AgentIds.StyleReview] = new StyleReviewAgent(factory);

// Plugins — added transparently
foreach (var plugin in pluginLoader.LoadAll(projectRoot))
    agents[plugin.Id] = plugin;
```


--- 

## LLM Provider Strategies  
  
AGO supports multiple strategies for selecting an LLM provider.  
  
The goal is to balance cost, speed, and quality without requiring complex configuration.  
  
### Strategy Types  
  
| Strategy        | Description                               |
| --------------- | ----------------------------------------- |
| `explicit`      | Use the provider specified directly       |
| `cheap-first`   | Try local model first, fallback to remote |
| `quality-first` | Prefer best model, fallback to local      |
| `balanced`      | Heuristic based on input size             |
| `local-only`    | Only local models (no API usage)          |
| `failover`      | Primary provider with fallback            |
  
### Example Config  
  
```yaml  
llm:  
default: ollama  
```


Agent override:

```
agents:  
style-review:  
provider: ollama  
  
security-review:  
strategy: quality-first
```

### Strategy Resolution
1. Agent-level `provider`
2. Agent-level `strategy`
3. Global `llm.strategy`
4. Global `llm.default`

### Behaviour
Strategies are implemented in code (not YAML DSL)
No user-defined expressions (to avoid complexity)
Deterministic and loggable

### Example: balanced
- small input → local model
- large input → remote model

### Example: cheap-first
- try ollama
- fallback → claude-cli
- fallback → API

## LLM Execution Logging

Each LLM call should log:

- agentId
- selected provider
- strategy used
- cache hit/miss

Example:

[style-review] strategy=cheap-first → ollama (cache miss)
[security-review] provider=anthropic (cache hit)