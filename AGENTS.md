# Engineering instructions — Heroes of Metal Age

These are standing instructions for coding agents working in this repository. Apply them to new and modified code. Existing prototype shortcuts are migration work, not examples to copy. Human instructions take precedence; resolve routine implementation choices autonomously within the requested scope.

## Project context and required reading

- Unity **6000.3.26f1**, Windows standalone, Mono, built-in rendering. Preserve the sound-first turn-based strategy experience.
- Read `Docs/ARCHITECTURE.md` before implementation or structural changes. Read `Docs/PROTOTYPE.md` for gameplay rules and `Docs/AUDIO_DIRECTION.md` for audio work. Consult `Docs/STATUS.md` for current limitations.
- `MetalGame.cs` currently combines presentation, campaign orchestration, combat, persistence and diagnostics. `MetalRules.cs` contains rules and data; `MetalAudio.cs` handles sound. The target architecture is documented separately and is not implemented yet.
- Preserve CLI-driven setup, validation and builds. No feature should require the user to manually wire Inspector references.

## Design principles

Use SOLID as a guide to maintainable boundaries, not a requirement to create an interface for every class.

| Principle | Application in this project |
|---|---|
| Single responsibility | Separate combat rules, recruitment, saving, audio and view rendering. A class should have one coherent reason to change. |
| Open/closed | Extend creatures, artifacts and sound cues through validated content definitions. Introduce a strategy when an actual algorithm varies; do not build speculative plugin frameworks. |
| Liskov substitution | Alternative implementations must preserve documented contracts and invariants. Do not require callers to special-case subtypes or throw unsupported-operation exceptions for promised behavior. |
| Interface segregation | Use small consumer-focused ports, such as save storage or randomness. Avoid a universal game service interface. |
| Dependency inversion | Gameplay policy must not depend on disk, UI or audio implementations. Inject relevant external dependencies at the composition root. |

- Prefer composition over inheritance. Use inheritance only for a genuine substitutable relationship.
- Apply KISS and YAGNI: solve the concrete requirement with the smallest understandable design.
- Apply DRY to duplicated knowledge and rules; do not merge unrelated code merely because it looks similar.
- Keep behavior local to the feature that owns it. Avoid god objects, generic `Utils` classes, service locators and mutable global state.
- Add a pattern only when it solves an identifiable problem. Do not introduce ECS, a DI framework, repositories for every entity or a global event bus by default.
- Refactor incrementally with behavior checks. Do not rewrite the entire prototype as a side effect of a small feature request.

## C# and clean code

- Use meaningful domain names, PascalCase for types/public members and camelCase for parameters/local variables. Use `_camelCase` for new private fields; do not churn existing files solely for style.
- Use explicit access modifiers and braces. Prefer focused methods, guard clauses and readable control flow over compressed one-line statements.
- Choose names that express intent; avoid ambiguous booleans and multi-purpose string flags. Prefer enums for finite internal states and stable IDs for content.
- Encapsulate mutable collections and state. Public fields are acceptable on serialization DTOs; gameplay entities should expose controlled operations or read-only views.
- Keep commands separate from queries. A query must not consume gold, advance turns or alter random state.
- Separate validation from mutation. A rejected command must leave state unchanged; multi-step transfers/purchases must be atomic.
- Do not swallow exceptions. Return an explicit rejection for expected gameplay failures; log and surface unexpected failures at a boundary. Preserve diagnostic context without logging secrets or full save payloads.
- Use named constants or configuration for tunable values. Avoid duplicating prices, growth, movement costs or stat formulas across UI and gameplay.
- Document non-obvious invariants, tradeoffs and lifecycle ownership. Comments should explain why, not restate the next line.
- Use Unity-supported C#/.NET features. Do not add runtime packages or language features that the pinned Editor cannot compile.

## Unity-specific practices

- Keep MonoBehaviours thin: Unity lifecycle, input, references and presentation. Prefer plain C# for rules that do not need Unity.
- Treat ScriptableObjects as authored configuration, not mutable campaign saves. Create separate runtime instances/state and validate configuration before play.
- Prefer explicit serialized references or bootstrap injection. Avoid repeated `Find*`, `GetComponent` and `Resources.Load` calls in frame/turn hot paths; cache validated dependencies.
- Initialize dependencies explicitly. Do not rely on accidental `Awake`/`Start` order across unrelated objects or use constructors for MonoBehaviour setup.
- Pair subscriptions with unsubscriptions in matching lifetimes. Stop owned coroutines/tasks on exit, disable or destruction as appropriate. Reset static state when using reload-disabled editor modes.
- Keep Unity object/API access on the main thread. Background work may operate only on detached plain data; marshal results back before interacting with Unity.
- Distinguish Unity object lifetime checks from ordinary C# null checks. Destroy owned runtime textures, clips and other native resources; never destroy shared authored assets.
- Keep UI render callbacks free of game-state changes except explicit input dispatch. Avoid allocating/rebuilding domain data during `OnGUI`, `Update` or frequent UI refreshes.
- Never reference `UnityEditor` from player assemblies. When extracting assemblies, enforce the documented dependency direction with `.asmdef` references.
- Preserve `.meta` files/GUIDs. Move assets together with their metadata. Never edit `Library` or generated solution/project files.
- Validate scenes, prefabs and required content automatically. Configuration failures should be actionable, not deferred null-reference exceptions.

## Rules, saves and sound

- Inject or seed randomness for reproducible combat tests. Define deterministic tie-breaking for initiative and pathfinding. Do not use wall-clock time as gameplay policy.
- Protect gold, inventory, army ownership and turn invariants. Use checked/wide arithmetic where counts and prices can overflow. Handle empty armies, full capacity, duplicate types and invalid content IDs explicitly.
- Save versioned plain data with stable IDs, not live Unity object references. Validate before replacing the active campaign. Evolve saves through explicit migrations or explain incompatibility.
- A failed write must preserve the last valid save. Use a temporary file and safe replacement appropriate to the platform. Test missing, malformed and incompatible saves.
- Publish sound feedback only after a command succeeds. Audio must not decide damage, rewards or movement, and an absent cue must not break gameplay.
- Prepare audio clips before playback. Keep latency-sensitive hits/input responsive; synchronize musical changes separately. Apply cue priorities, bounded concurrency and independent music/effects/voice controls.

## Workflow and completion criteria

1. Inspect relevant code and instructions; identify the owning feature and acceptance criteria.
2. Implement the smallest coherent change, including automatic setup/reference wiring when needed.
3. Check edge cases and dependency/lifecycle ownership. Add meaningful tests for changed rules or integration boundaries.
4. Run checks appropriate to the change. Report actual results and any unverified behavior honestly.
5. Update architecture/status documentation when responsibilities, dependencies or user-visible rules change. Leave a reviewable diff and respect unrelated user changes.

- `Tools/Build.ps1` runs generated-scene setup, rule validation and the Windows build.
- `Tools/SmokeTest.ps1` runs the built-player scenario and captures screens. Use it when gameplay, setup, views or integration change; inspect relevant screenshots and player logs.
- Documentation-only edits need reference/diff checks, not a Unity rebuild.
- Current assertions live in `PrototypePipeline.Validate`; migrate them to feature tests as test infrastructure is extracted. Test outcomes and invariants, not private methods or implementation shape.
- Smoke tests must use isolated saves and must never overwrite the player's campaign. Visual checks and human listening/playtesting remain necessary.
- `PrototypePipeline.Setup` regenerates only `Assets/Scenes/AshenPass.unity`. Keep authored content outside that generated scene. Setup must be repeatable, preserve authored assets and fail clearly rather than silently overwrite them.
- Keep Unity version/package changes deliberate and justified. Retain the existing `.gitignore` protection for cache, build output and local user files.

## Official references

- [Unity: architectural boundaries as a project grows](https://unity.com/how-to/how-architect-code-your-project-scales)
- [Unity: authored data and ScriptableObjects](https://unity.com/how-to/separate-game-data-logic-scriptable-objects)
- [Unity: assembly definitions](https://docs.unity.com/en-us/engine/6000.3/manual/programming-environment/script-compilation/assembly-definition-files)

These references provide background. Repository requirements and the pinned Unity version determine implementation choices.
