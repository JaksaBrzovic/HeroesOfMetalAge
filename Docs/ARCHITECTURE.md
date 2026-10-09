# Architecture — Heroes of Metal Age

Status: **target design with an incremental migration plan**. This document does not claim that the prototype already follows these boundaries. Read together with `../AGENTS.md`, `PROTOTYPE.md` and `AUDIO_DIRECTION.md`.

## 1. Goals and constraints

Build a maintainable sound-first turn-based strategy game with reproducible gameplay, replaceable presentation/audio and automated Unity setup/builds. Optimize for a small team and fast playable iterations. Preserve the existing first mission during migration.

The prototype targets Unity 6000.3.26f1 on Windows. The architecture must accommodate future creatures, artifacts, town ownership and missions without putting their rules into UI callbacks. Multiplayer, ECS, distributed services, network determinism and a generic modding platform are not current requirements.

## 2. Current implementation

| File | Current responsibilities | Architectural debt |
|---|---|---|
| `Assets/Scripts/MetalGame.cs` | Map/pathfinding, campaign flow, combat/AI, IMGUI views, saves, procedural art and smoke simulation | Too many responsibilities; string states and direct dependencies; rendering and actions intertwined |
| `Assets/Scripts/MetalRules.cs` | Campaign/troop data, creature catalog, economy, equipment, stat and damage rules | Unity-dependent calculations, publicly mutable state, index-based content IDs and static catalog |
| `Assets/Scripts/MetalAudio.cs` | Synthesized audio, layers, cue playback and resource loading | Cue strings, loading during calls and limited scheduling/priorities |
| `Assets/Editor/PrototypePipeline.cs` | Scene generation, rule assertions and build | Useful automation; tests/setup/build responsibilities should eventually separate |

These shortcuts enabled the first playable slice. Preserve behavior while replacing them feature by feature. New features should not add unrelated responsibilities to `MetalGame`.

## 3. Dependency direction

Use a lightweight layered architecture organized by gameplay feature:

```text
Unity Presentation ----> Application ----> Domain
Unity Infrastructure -------------------> Application ports / Domain contracts
Bootstrap ------------> all concrete implementations, for composition only
Editor tools ---------> bootstrap/content validation, never the other way around
Tests ----------------> the layer under test
```

- **Domain:** plain C# entities, value types and game rules. No UnityEngine, GameObjects, UI, audio, filesystem or scene dependencies once extracted.
- **Application:** use cases, state transitions and orchestration. Depends on Domain; owns ports for side effects such as save storage and presentation feedback.
- **Presentation:** Unity input, views, animation and view models. Dispatches commands; displays results. Does not calculate prices, damage or rewards.
- **Infrastructure:** Unity audio, asset/configuration loading, disk persistence and platform adapters. Implements narrowly scoped ports.
- **Bootstrap:** composition root that constructs and connects the concrete objects. Start with constructor/manual injection; introduce a DI package only if it removes a demonstrated burden.

Add `.asmdef` boundaries when the first modules are extracted. Domain and Application should use `noEngineReferences` when compatible with their dependencies. Presentation and Infrastructure must not reference each other for ordinary gameplay; Bootstrap wires them. Avoid circular assembly references.

## 4. Suggested organization

This layout is a destination, not an instruction to create empty scaffolding now:

```text
Assets/HeroesOfMetalAge/
  Runtime/
    Domain/          Campaign, World, Combat, Heroes, Armies, Towns
    Application/     use cases, state flow, ports
    Presentation/    World, Combat, Town, Hero, Shared UI
    Infrastructure/  Audio, Persistence, Content
    Bootstrap/
  Content/           creature, artifact, town, mission and audio definitions
  Editor/            setup, validation and build tools
  Tests/             EditMode and PlayMode tests
  Generated/         disposable generated assets, with an explicit ownership boundary
```

Prefer feature namespaces such as `HeroesOfMetalAge.Domain.Combat`. Keep helpers next to their owning feature. Introduce shared modules only for concepts genuinely used across boundaries.

## 5. State and data ownership

Separate three kinds of data:

| Data | Ownership | Examples |
|---|---|---|
| Authored definitions | Content catalog, read-only during play | Creature stats, artifact bonuses, mission map, reward tables, audio cue definitions |
| Runtime state | Domain/session instances | Gold, army stacks, day, hero position, town owner, battle HP |
| Save DTOs | Persistence boundary | Schema version, stable definition IDs, campaign snapshot |

Unity ScriptableObjects are suitable for authored definitions. Convert them to validated plain definitions at startup; Domain must not consume ScriptableObjects directly. Never mutate shared definition assets to represent battle damage or a player's inventory.

Replace array-index IDs with stable IDs during a deliberate content/save migration. Provide a catalog lookup with explicit missing-ID handling. Do not serialize scene instance IDs or asset object references into campaign saves.

One `GameSession` owns the current campaign and at most one battle. Domain operations own writes. Views receive read-only snapshots/view models; audio receives event data. Do not expose mutable campaign lists to arbitrary subscribers.

## 6. Commands, results and events

Use explicit application commands such as `MoveHero`, `ResolveEncounter`, `BuyCreatures`, `TransferStack`, `EquipArtifact`, `EndDay` and `PerformBattleAction`.

Each command:

1. Validates state, ownership, range, resources and capacity.
2. Computes the complete outcome without partial mutation.
3. Applies the outcome atomically.
4. Returns a structured success/rejection result.
5. Publishes immutable facts for presentation/audio and schedules persistence where appropriate.

For example, `BuyCreatures` must validate visiting-hero availability, town access, stock, price and army capacity before changing gold/stock/army. UI shows the price from the same policy used by the command. A disabled button is convenience, not authorization or validation.

Use events for facts that multiple consumers need, such as `DamageResolved` or `BattleWon`. Use direct calls for straightforward command responses. Avoid a global string-topic event bus; use typed, scoped subscriptions with clear ordering and lifetime ownership. Publish before/after values or a result snapshot so subscribers need not query mutable state later. Listener failure must not roll back already committed gameplay.

## 7. World and campaign flow

- Keep adventure-grid coordinates separate from hex-combat coordinates. Use explicit value types and one tested conversion policy per grid.
- Pathfinding operates on a traversal model, not rendered tiles. Costs, blocking sites and diagonal corner rules belong to Domain. Use deterministic tie-breaking; optimize with A*/a priority queue only when map size warrants it.
- Keep planned route separate from executed movement. Revalidate each step against current state. Stop before unresolved encounters or when movement is exhausted.
- `EndDay` owns movement restoration and weekly growth. Weekly behavior must be tested at days 7/8 and 14/15; neither UI refresh nor loading a save may apply growth twice.
- Model campaign states with an enum/state machine: Adventure, Encounter, Battle, Town, Victory, Defeat. Treat hero/options screens as overlays where suitable. A transition owns enter/exit work, input permissions and cancellation.
- Use a single transition authority to prevent simultaneous travel, town interaction and combat commands. Do not distribute competing `busy`/modal flags across unrelated views.

## 8. Tactical combat

`BattleState` owns combatants, positions, total HP, initiative, round flags and terminal outcome. `CombatResolver` applies rules; `BattleTurnController` orchestrates legal actions. `BattleAi` proposes commands through the same validation path used by the player.

- Damage, retaliation, casualties, movement reach and ranged penalties are independent of animation timing.
- Inject an `IRandomSource` where combat needs randomness. Tests use fixed seeds/scripted values. Define tie-breaking for equal speed and equally good AI destinations.
- Produce an ordered resolution result: movement, primary hit, casualties, retaliation, deaths and battle outcome. Presentation can animate this sequence without re-running rules.
- Apply victory casualties/rewards once, using an outcome identifier or an equivalent transition guard. Loading/reopening a victory panel must not grant duplicate rewards.
- Keep pre-battle snapshots and document retry policy. Current prototype retries without persistent defeat casualties; preserve this until deliberately changed.
- Runtime animation may temporarily block new commands, but it must not become the source of truth for whose turn it is.

## 9. Town, armies and heroes

Represent town owner/faction explicitly, rather than deriving permissions from which row is visible. Keep town defense, visiting hero and home hero as distinct concepts. A town can have a garrison without a hero; a faction-held town must protect its army from visitor commands.

Recruitment uses a price policy derived from ownership and definition data. Stocks are runtime state; growth is authored data. Transfer and hero-stationing commands must validate the full destination before committing any stack movement. Preserve counts exactly when merging equal types, including capacity failures.

Equipment owns slot compatibility and replacement. Derived hero stats come from base stats, levels and equipped bonuses through one calculator. UI previews must use the same calculation. Do not permanently add an artifact bonus to base stats and then add it again on load/equip.

## 10. Audio architecture

Audio is a first-class presentation subsystem, independent of gameplay decisions:

```text
Successful game command -> immutable gameplay facts -> audio director
                                                    -> visual presentation
```

The director maps travel state to persistent stems and events to short cues. Use authored cue definitions for clips, variants, volume, priority, concurrency and musical-transition policy. Missing optional cues should be logged once and handled gracefully.

- Preload/cache required clips before they are needed. Do not synthesize clips or synchronously load resources during attacks.
- Schedule musical stems using Unity DSP time. Distinguish immediate hit/input feedback from beat/bar-aligned music changes.
- Define behavior for mute, pause, scene/state exits and rapid transitions. Cue priorities and bounded voices prevent a sound pileup.
- Use music/effects/voice buses and deliberate ducking where needed. Keep user settings independent of campaign saves.
- Scope ownership of AudioSources, runtime clips and subscriptions. The director must not leak audio across a new campaign or duplicate itself on scene reload.
- Keep the audio audition panel, allowing sound review without replaying the mission. Listening tests must cover repeated gameplay, transitions and fatigue.

The current synthesized clips/offline voices are replaceable placeholders; a new production audio backend should preserve event contracts.

## 11. Persistence, lifecycle and automation

`SaveCampaign` creates a consistent snapshot outside in-progress battle mutations. `ISaveStore` implements IO; serialization/migration maps DTOs to validated runtime state. Validate IDs, counts, ownership, positions and schema before swapping the active session. Save/load failures must leave the active session and previous valid save intact.

Use temporary-file writes and platform-appropriate replacement/backup. Test corrupt, missing and unsupported versions, plus interrupted writes. Define what is saved on victory, travel, recruitment and equipment; do not save as a side effect of rendering.

Every long-lived service/subscription/task has an owner. Scene/state exit cancels its work. Unity-facing work stays on the main thread. File IO may use detached snapshots off-thread if profiling justifies it; never allow background code to mutate live campaign collections.

Preserve the CLI workflow. Separate authored and generated assets. Generator reruns should update only owned output, retain stable GUIDs and report missing dependencies clearly. Editor code must remain excluded from player builds. Do not rely on manual Inspector repairs after generation.

## 12. Tests and performance

- **Domain tests:** economy atomicity, ownership, weekly boundaries, level/equipment calculations, transfer conservation, hex reach, damage/retaliation and terminal outcomes.
- **Application tests:** command rejection, legal state transitions, no duplicate rewards, cancellation and save/event ordering.
- **Unity integration tests:** configuration import, bootstrap wiring, scene lifecycle and audio routing. Use fakes for IO/randomness where appropriate.
- **Built-player smoke:** complete the mission, conquest/home/visiting, save/load and defeat/retry using isolated save files. Capture relevant screens and inspect logs.
- **Human review:** usability, visual clarity, musical coherence, perceived impact and fatigue.

Measure before optimizing. Avoid known per-frame allocations, repeated asset searches and unnecessary updates, but do not add pooling/jobs/ECS without evidence. Turn-based rules need reliability and readability more than a speculative high-throughput framework.

## 13. Incremental migration plan

1. Preserve the existing build and gameplay checks as a baseline. Characterize rules whose behavior is unclear before extracting them.
2. Extract plain campaign/combat data and rule calculators; replace Unity math/coordinates at this boundary. Add Domain assembly/tests.
3. Extract recruitment, equipment, day progression and army-transfer use cases. Make views dispatch commands rather than mutate state.
4. Extract combat resolution and AI, retaining battle outcomes and seeded tests.
5. Extract persistence and the typed audio-event boundary; add explicit session/state lifetimes.
6. Split views and introduce the Bootstrap composition root. Replace IMGUI with a production UI solution only when visual/workflow requirements justify the choice.
7. Migrate hardcoded content into authored definitions with stable IDs and a save migration. Remove superseded paths and update this document.

Each step must remain playable and buildable. Do not maintain two authoritative rule implementations or migrate every layer in one unreviewable change.

## 14. Architecture decisions and change policy

Record substantial decisions here using **context, decision, tradeoff, validation and revisit trigger**. Current decisions: lightweight layers; feature ownership; plain gameplay core; manual composition; configuration/runtime/save separation; typed sound feedback; CLI automation. These are project choices, not universal requirements.

Update this document when dependency direction, state ownership, persistence contracts or content/audio boundaries change. Clearly label completed migrations and remaining debt. A new abstraction should earn its place through a real consumer, testing boundary or variation.

## Official background

- [Unity architectural guidance](https://unity.com/how-to/how-architect-code-your-project-scales)
- [Unity configuration/data separation](https://unity.com/how-to/separate-game-data-logic-scriptable-objects)
- [Unity 6.3 assembly definitions](https://docs.unity.com/en-us/engine/6000.3/manual/programming-environment/script-compilation/assembly-definition-files)

The design above is tailored to this project; it is not a claim that Unity prescribes one mandatory architecture.
