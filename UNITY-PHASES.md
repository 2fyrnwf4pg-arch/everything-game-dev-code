# 5D Sudoku — Unity Integration Plan

The core-prototype milestone is finished: `Core/` is a deterministic, fully
tested rules engine with no engine dependency, and `PHASES.md` is closed. This
file is the plan for the next milestone — putting that engine behind a Unity
front end for Steam/Windows, iOS and Android.

**How to use this file:** the same way as `PHASES.md`. Work one phase at a time,
in order. Each phase has its own scope, its own tests, and an explicit stop
point. Do not start a phase that has not been approved.

The rules in `PHASES.md` and the constraints in `CLAUDE.md` still hold. Nothing
in this milestone changes a game rule. If the UI turns out to need a rule that
does not exist, that is a finding for `PHASES.md`, not something to implement in
a MonoBehaviour.

---

## What the core already gives you

Verified against the code, not assumed. These are the properties the integration
gets to build on.

| Property | Why it matters here |
|---|---|
| `netstandard2.1`, no package or project references | Drops into Unity as-is; nothing to reconcile |
| No reflection anywhere in `Core` | IL2CPP/AOT safe; no `link.xml` needed for Core |
| No `System.IO`, no threading, no `UnityEngine` | Saving goes through Unity's own file APIs; Core can run on any thread |
| Integer arithmetic only, no floating point, no randomness | Bit-identical results on every platform |
| Every state immutable, every action returns a new state | The view can re-render from state and never has to diff |
| Saves are plain text via `GameSaveFormat` | Straight to `Application.persistentDataPath` |
| 337 tests, ~0.5 s, seed-swept | A regression during integration is caught by an existing suite |

Two things to know before designing around them:

- **`Core/Compatibility/IsExternalInit.cs` is currently unused.** No `record` and
  no `init` accessor exists in `Core` — immutability is hand-written. `CLAUDE.md`
  pins the shim as a fixed decision and it stays, but it is inert today, so it is
  not a Unity risk unless records are introduced later.
- **Memory grows with the length of a run, not the size of the board.** Every
  state of every timeline is kept, deliberately. A 9×9 run is 51 boards of 81
  ints per timeline; that is small, but it is unbounded in run length.

---

## Two blocking incompatibilities, found before planning around them

These would have derailed the naive "just drop the source in `Assets/`" approach.

### 1. Unity's compiler is C# 9; `Core` is written in C# 10

23 of `Core`'s 24 files use file-scoped namespaces (`namespace X;`), which is a
C# 10 feature. Unity's Roslyn is pinned to C# 9. **`Core`'s source does not
compile inside Unity as written.**

Three ways out, in order of preference:

1. **Ship `Core` as a pre-built DLL** (`dotnet build -c Release`, drop
   `FiveDSudoku.Core.dll` plus its portable `.pdb` into
   `Assets/Plugins/FiveDSudoku/`). A `netstandard2.1` assembly is exactly what
   Unity consumes. The language level becomes a Core-internal concern that Unity
   never sees, and the `IsExternalInit` shim disappears into the binary. Costs a
   build step, which must be scripted and reproducible; the DLL is generated and
   never hand-edited, so there is still one source of truth.
2. **Convert `Core` to block-scoped namespaces.** Mechanical, 23 files, one
   commit. Keeps source-level stepping inside Unity. Adds a standing rule that
   `Core` must stay C# 9-compatible, which is a rule someone will eventually
   break by accident.
3. Wait for Unity to move past C# 9. Not a plan.

**Recommendation: option 1, with option 2 as the fallback** if stepping through
Core inside Unity turns out to matter more than expected.

### 2. The test suite is nearly portable to Unity — but not quite

Unity's Test Framework is NUnit 3; the repo is on NUnit 4. That is a smaller
problem than it sounds: the suite uses `Assert.That` 918 times and almost nothing
else (`Assert.Pass` once, `Assert.Fail` once, `TestContext.Out` three times), all
of which NUnit 3 has. The real blockers are language and API level, not the test
framework:

- 36 test files use file-scoped namespaces (C# 10).
- 4 test files use raw string literals (C# 11) for board fixtures.
- One line uses `Enum.GetValues<T>()` (.NET 5+).

**Do not try to run all 337 tests inside Unity.** The plan is:

- `dotnet test` from the repo root stays the authoritative suite, in CI and
  locally. It is what proves the rules.
- A **smoke subset** — perhaps 20 tests covering a solve, a branch, a dead
  classification, present movement and a save round trip — is ported to Unity
  EditMode tests. Its job is different: proving `Core` behaves identically inside
  Unity's runtime, under Mono and under IL2CPP, not re-proving the rules.

---

## Decisions to settle in U0

Settle these once, write them into `CLAUDE.md` the way the Phase 0 decisions
were, and stop re-opening them.

| Decision | Recommendation | Why |
|---|---|---|
| Unity version | Current LTS | Hard requirement: API Compatibility Level **.NET Standard 2.1** |
| How Core gets in | Pre-built DLL in `Assets/Plugins/` | See blocker 1 |
| Render pipeline | URP (2D renderer) | One pipeline across desktop and mobile |
| Input | Input System package | Touch, mouse and gamepad without three code paths |
| UI | UI Toolkit for menus and HUD; sprites/mesh for the board | The board wants per-cell control over feel and effects; the menus do not |
| Assembly layout | `FiveDSudoku.Unity.Game`, `.UI`, `.Editor` asmdefs, all referencing Core | Keeps the "UI holds no rules" boundary checkable |
| Repo | Unity project in this repo under `Unity/` | One history for engine and front end |

---

## Phase U0 — Unity project, with the core inside it

**Goal:** a Unity project that compiles against `Core` and runs a smoke subset of
its tests. No game yet.

**Depends on:** nothing.

**Scope:**
- Create the Unity project under `Unity/`, on the LTS chosen above, with API
  Compatibility Level set to .NET Standard 2.1.
- Script the Core build step: `dotnet build Core/Core.csproj -c Release` and copy
  the DLL and PDB into `Assets/Plugins/FiveDSudoku/`. Wire it so a stale DLL is
  obvious — a version stamp asset, or a CI check that rebuilds and diffs.
- Create the assembly definitions and their references.
- Port the smoke subset of tests to EditMode.
- `.gitignore` for `Library/`, `Temp/`, `Logs/`, `Build/`, `obj/`.

**Out of scope:** any game code, any art, any input.

**Tests required:**
- The smoke subset passes in Unity's Test Runner in EditMode.
- `dotnet test` from the repo root still passes, unchanged.

**Definition of done:** both suites green, and a one-command way to refresh the
Core DLL. Report both commands. **Stop and wait for approval before U1.**

---

## Phase U1 — A board on screen, read-only

**Goal:** a scene that renders a real `GameState`. Nothing is clickable.

**Depends on:** U0.

**Scope:**
- A `GameSession` component holding the immutable `GameState` and raising a
  change event. **The view re-renders from state; it never mutates state.**
- A board renderer for any `BoardSize` — 4×4 and 9×9 from the same code, box
  borders derived from `BoxWidth`/`BoxHeight`, never hard-coded.
- Placeholder art only.

**Out of scope:** input, timelines, animation.

**Tests required:** an EditMode test that the renderer builds the right number of
cells for 4×4 and 9×9 and reads its values from the state; a PlayMode test that a
state change re-renders.

**Definition of done:** a scene shows a real level's starting board at both
sizes. **Stop.**

---

## Phase U2 — The normal move

**Goal:** a human can play and win a 4×4 with a mouse or a finger.

**Depends on:** U1.

**Scope:**
- Cell selection and value entry.
- Every `MoveRejection` value mapped to feedback a player can act on. The reasons
  already exist and are distinct on purpose — do not collapse them into "invalid".
- Win and game-over presentation.

**Out of scope:** timelines, Temporal Moves, slots.

**Tests required:** an EditMode test that every `MoveRejection` value has a
message (drive it off the enum so a new reason fails the test); a PlayMode test
that drives input through a full 4×4 solve and reaches `Won`.

**Definition of done:** Scenario A, played by hand, in the editor. **Stop.**

---

## Phase U3 — The multiverse, read-only

**Goal:** the player can see that there is more than one timeline, and look at
any of them.

**Depends on:** U2.

**Scope:**
- Timeline list with id, status and frontier.
- The present, shown as a position, not just a number — this is the mechanic the
  whole game hangs on.
- Switching timelines (free, changes nothing else — the rules already guarantee
  that; the UI must not add a cost).
- Scrubbing a timeline's history, read-only.

**Out of scope:** creating branches.

**Tests required:** EditMode tests that the list reflects a state built in code,
including a `DEAD` and an `INACTIVE` timeline; a test that switching leaves
budget, present and every board untouched.

**Definition of done:** a state built in code with three timelines is fully
readable in the UI. **Stop.**

---

## Phase U4 — The Temporal Move

**Goal:** a human can branch. This is the phase the game lives or dies by, and
the one most worth prototyping roughly before committing to a layout.

**Depends on:** U3.

**Scope:**
- Choosing a source timeline, a source time, a cell and a value.
- Showing what the rules already compute: remaining budget, which historical
  states are inside the temporal window, and which are not.
- `ValidateTemporalMove` used to preview legality **before** the player commits —
  it exists for exactly this and costs nothing.
- Presenting immediate `DEAD` classification honestly: the branch was legal, and
  it is finished.

**Out of scope:** slot pressure.

**Tests required:** a PlayMode test performing Scenario C through the UI; EditMode
tests that every `TemporalMoveRejection` value has a message and that the window
indicator agrees with `ValidateTemporalMove` for every historical state.

**Definition of done:** Scenario C, played by hand. **Stop.**

---

## Phase U5 — Slots

**Goal:** the player can manage which timelines are in play.

**Depends on:** U4.

**Scope:**
- Slot count and occupancy shown.
- Parking and bringing back (rule D4), including that bringing a timeline back
  classifies it and can reveal it was doomed. That is a designed consequence —
  present it, do not hide it.
- Branches created `INACTIVE` because no slot was free, shown as parked rather
  than as failures.

**Tests required:** a PlayMode test playing the full multi-timeline session from
Phase 4 — a wrong value, a parked branch, the root dying, the branch activated,
the level won on it — entirely through the UI.

**Definition of done:** Scenarios B and E, played by hand. **Stop.**

---

## Phase U6 — Levels, saving, run flow

**Goal:** a run survives quitting the app, and levels are content rather than
code.

**Depends on:** U5.

**Scope:**
- A `ScriptableObject` wrapping `LevelDefinition`. It carries data only; it does
  not re-implement validation.
- Level select and run flow.
- Save and resume through `GameSaveFormat` and `Application.persistentDataPath`.
  Core produces the text; Unity writes the file.
- **An editor test that runs `LevelValidator` over every level asset in the
  project and fails the build on any level that is not a release candidate.**
  Phase 5 built this; this is where it earns its keep.

**Tests required:** the level-asset validation gate; a PlayMode test that saves
mid-run, reloads and continues to the same result — the engine-level version of
this already passes, so a failure here is a Unity-layer bug.

**Definition of done:** quit and resume mid-run restores exactly; every shipped
level passes validation automatically. **Stop.**

---

## Phase U7 — Performance and platform

**Goal:** it holds up on the slowest device you intend to ship on.

**Depends on:** U6.

**Scope:**
- **Measure the solver first, then decide.** `PerformTemporalMove` and
  `ActivateTimeline` call the solver synchronously. On desktop a 9×9
  classification measured 0.1–0.9 ms during Phase 8; on a low-end phone it will
  be worse, and a contradictory sparse grid is worse again. If it exceeds the
  frame budget, move it off the main thread — `Core` touches no Unity API and no
  IO, so a plain background task is safe. Do not do this before measuring.
- Allocation and memory: profile a long run with many timelines. Every state is
  retained by design; confirm what that costs in practice.
- IL2CPP build and run. `Core` uses no reflection, so no `link.xml` entry should
  be needed — verify rather than assume.
- Touch input, safe areas, aspect ratios, and 9×9 legibility on a phone.

**Tests required:** a stated frame-time and memory budget, and a measurement
against it on the lowest target device. The smoke subset passing under IL2CPP.

**Definition of done:** budgets stated and met, with numbers. **Stop.**

---

## Phase U8 — Builds and acceptance

**Goal:** signed-off builds for the three targets, and a check that the boundary
held.

**Depends on:** U7.

**Scope — verify, do not implement:**
- Windows/Steam, iOS and Android builds that run.
- **The UI layer contains no game rules.** Automate what can be automated: an
  editor test asserting no type in the UI assemblies declares a member whose name
  matches the rule vocabulary (`IsLegal`, `Validate`, `Classify`, `Solve`,
  `Present`…). It is a heuristic, exactly like the Phase 8 audit, and like that
  audit it should be checked for being vacuous by deliberately adding a violation.
- `Core` is unmodified by the integration, or every modification is a deliberate
  entry in the `PHASES.md` decision log.
- The `PHASES.md` not-yet list still holds — this milestone adds art, sound and
  animation, so that list needs revisiting rather than blind re-running.

**Definition of done:** three builds, the boundary check green and proven
non-vacuous, and a short report with build sizes and device results.

---

## Risk register

| Risk | Likelihood | Mitigation |
|---|---|---|
| Core's C# 10 source will not compile in Unity | **Certain** | Ship as a DLL (U0) |
| Solver call spikes a frame on mobile | Likely | Measure in U7; Core is thread-safe and IO-free, so moving it off-thread is cheap |
| `Core.Timelines` collides with the `UnityEngine.Timeline` package in `using` directives | Likely if that package is installed | Never import both in one file; alias if needed |
| Stale Core DLL in `Assets/` silently diverging from source | Likely | Scripted build step plus a CI check that rebuilds and diffs |
| `bin/`/`obj/` landing inside a Unity-scanned folder | Likely with the source-copy fallback | Redirect `BaseOutputPath`/`BaseIntermediateOutputPath`, or use the DLL path |
| Memory growth over a long run with many timelines | Possible | Profile in U7; the retention is deliberate, so the answer is a budget, not a fix |
| Game rules leaking into MonoBehaviours | Possible | The U8 boundary check, plus reviewing every `if` in a presenter |
| Unity Test Framework's NUnit 3 vs the repo's NUnit 4 | Low | Smoke subset only; `dotnet test` stays authoritative |

---

## Explicitly NOT part of this milestone

Carried forward from `CLAUDE.md`, minus the three this milestone is allowed to
add. Still out: multiplayer, online services, achievements, monetization, ads,
Steam features beyond shipping a build, cross-timeline magic constraints, causal
echoes, timeline merging, artificial paradox rules, and any gameplay Undo.

Now in scope, because a front end needs them: placeholder and then final art,
sound, and animation. The Phase 8 audit forbids all three by name and will need
its word list narrowed to the Core assembly when U8 revisits it — which is the
correct outcome, since the audit's real subject was always `Core`.
