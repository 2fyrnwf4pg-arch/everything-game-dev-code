# 5D Sudoku — Phased Implementation Plan for Claude Code

This file breaks the original core-prototype specification into ordered
phases. Each phase is a self-contained unit of work with its own scope, its
own tests, and an explicit stop point. Rules are carried over from the
original spec essentially unchanged — only the *order* and *grouping*
changed, not the content, since these rules were already validated against
a Python prototype.

**How to use this file:**

- Tell Claude Code which phase to work on, e.g. *"Read PHASES.md and
  implement Phase 1 only."*
- Claude Code should not start a later phase in the same session unless
  explicitly told to continue.
- If something in an earlier phase turns out to be wrong once a later phase
  needs it, that's a real finding — stop and say so rather than quietly
  patching it while nominally "in" a different phase.

General constraints that apply to every phase (architecture, determinism,
immutability, the "do not implement" list) live in `CLAUDE.md` and are not
repeated in full here.

---

## Phase 0 — Solution Skeleton

**Goal:** an empty, buildable, testable solution with the right shape. No
game rules yet.

**Depends on:** nothing.

**Scope:**

- Create the solution/project layout:
  ```
  Core/
    Sudoku/
    Timeline/
    Game/
    Level/
    Solver/
    Validation/

  Tests/

  Tools/
    ScenarioRunner/
  ```
- `Core` must not reference UnityEngine or any Unity package.
- Wire up a test project (`Tests/`) with one placeholder test that passes,
  just to prove the pipeline works end to end.
- Set up whatever project/solution files are needed so `dotnet build` and
  `dotnet test` both succeed.

**Out of scope:** any actual Sudoku or timeline logic.

**Definition of done:** `dotnet build` and `dotnet test` both succeed.
Report the exact commands you ran. **Stop and wait for approval before
Phase 1.**

---

## Phase 1 — Generic Sudoku Engine (no timeline yet)

**Goal:** a standalone, engine-agnostic Sudoku core that knows nothing
about timelines, present, or time travel. This is "just" correct, generic,
tested Sudoku.

**Depends on:** Phase 0.

**Scope (from original spec §1):**

- Support at least two board sizes: 4×4 (2×2 boxes) and 9×9 (3×3 boxes). Do
  not hard-code the engine to 4×4 — size must be a parameter/generic
  concept, not a magic constant.
- A board type representing cell values, with row/column/box legality
  checks.
- A solver (`Core/Solver/`) able to:
  - determine whether a board is valid (no constraint violations),
  - determine whether a placement is legal,
  - count/find solutions for a board (needed later to distinguish
    zero-solution / one-solution / multi-solution boards, and to classify
    branches in Phase 3).

**Out of scope:** `T`/`L` coordinates, timelines, present, branching,
budget/window, victory/game-over.

**Tests required (original "Sudoku" test category):**

- valid/invalid boards
- legal/illegal placements
- solver correctness
- a known zero-solution board
- a known one-solution board
- a known multi-solution board (for diagnostics later)

**Definition of done:** all of the above tests pass, for both 4×4 and 9×9.
**Stop and wait for approval before Phase 2.**

---

## Phase 2 — Immutable States & Single-Timeline Game Loop

**Goal:** a playable, single-timeline Sudoku game with immutable history,
present/frontier, and victory/game-over — but still with zero time travel.
This phase's acceptance test is **Scenario A** from the original spec.

**Depends on:** Phase 1.

**Scope:**

*Immutable states (original §2):*
- Every played Sudoku state is immutable — a move never mutates an old
  board, it produces a new one.
- A normal placement on state `Tn` creates `Tn+1` on the same timeline
  (`T0 -> T1 -> T2 -> T3`, …).
- Enough history must be kept to inspect all previous states. Use immutable
  value objects/records where practical.

*Timeline structure (original §4), even though only one timeline exists
yet:*
- Each timeline has: id, parent timeline id (nullable for the root
  timeline), branch time, historical states, frontier time, status, and
  whether it currently occupies an active timeline slot.
- The frontier is the only directly playable state of a timeline.
  Historical states remain viewable but not directly editable.

*Timeline switching (original §5) — implement even with one timeline, so
the API shape is ready:*
- Switching timelines is a view/selection operation, not a game move: it
  consumes no temporal budget, advances no time, changes no board, changes
  no history, and does not by itself change the present.

*Normal move (original §3):*
- `PlaceValue(row, column, value)` is legal only when: (1) the selected
  timeline is ACTIVE; (2) its frontier equals the current global PRESENT;
  (3) the target cell is empty; (4) the value is within the Sudoku range;
  (5) the value does not violate row/column/box constraints.
- A legal placement produces the next immutable state on that timeline.
- After every state-producing action, recompute PRESENT (see below).

*Present (original §8), single-timeline case:*
- `Present = min(frontier of every ACTIVE + active-slot timeline)`. With
  one timeline this just tracks that timeline's frontier, but implement the
  real formula now, not a special case — Phase 4 needs it to already be
  correct.

*Active/inactive slots (original §9) — structure only, since there's
nothing yet to overflow:*
- Each level has a maximum number of simultaneously ACTIVE timeline slots.
  The root timeline occupies one.

*No normal Undo (original §12):*
- Do not implement a gameplay Undo anywhere in the core rules.

*Victory (original §14):*
- A level is won when any timeline reaches a valid, complete Sudoku
  solution. (With one timeline, this is just "the root solves it.")
- `FinalDepth` is a pacing/balance parameter, not a requirement about move
  count — a complete valid solution is always sufficient for victory.

*Game over (original §15):*
- Game over occurs when the game is not already won and there are no
  ACTIVE timelines capable of further play.

**Out of scope:** branching/Temporal Moves, temporal budget, temporal
window, multi-timeline present behavior, active-slot overflow, level
validation layer.

**Tests required:**
- From "Timeline": immutable history, frontier advancement.
- From "Present": minimum active frontier (degenerate single-timeline
  case).
- From "Victory": solved root wins; `FinalDepth` does not block an
  already-complete valid solution from winning.
- **Scenario A** (original §19): start a solvable 4×4 puzzle, solve it
  entirely without Temporal Moves, assert victory, assert temporal budget
  is unchanged (budget can just be a fixed configured number that nothing
  has touched yet).

**Definition of done:** Scenario A passes end to end, on a real 4×4 puzzle,
through the actual public API (not a test-only shortcut). **Stop and wait
for approval before Phase 3.**

---

## Phase 3 — Temporal Moves: Branching, Budget, Window, Immediate Classification

**Goal:** the actual time-travel mechanic. This is the core "5D" feature
and the phase most worth reviewing carefully.

**Depends on:** Phase 2.

**Scope:**

*Temporal Move / branching (original §6):*
- A Temporal Move creates a new timeline from a historical state of an
  existing timeline (e.g. branching from historical `T1` of
  `L0: T0 -> T1 -> T2 -> T3` creates `L1: T1' -> ...`). The parent timeline
  is never modified. The new timeline starts from an immutable copy of the
  selected historical board, plus one alternative legal Sudoku placement.
- A branch is valid only if **all** of the following hold:
  1. temporal budget > 0;
  2. source timeline exists;
  3. source timeline contains the requested historical state;
  4. source state is strictly before the source frontier;
  5. source time is strictly before the global PRESENT;
  6. source time is within the configured temporal window;
  7. the branch placement is locally legal Sudoku;
  8. the branch actually changes the source board.
- A branch is therefore never an edit to parent history.

*Immediate classification (original §7) — validated against the Python
prototype, keep it exact:*
- A branch can be locally legal but mathematically impossible to complete.
  Immediately after creating a branch:
  - if the new board has at least one complete Sudoku solution → status
    `ACTIVE`;
  - if the new board is complete and valid → status `SOLVED`;
  - if the new board has zero Sudoku solutions → status `DEAD`.
- Never leave an impossible branch in `ACTIVE` state. (Example: locally
  legal, zero completions → `DEAD` immediately.)

*Temporal budget (original §10):*
- Every level has a finite `TemporalBudget`. Each Temporal Move consumes
  exactly 1 unit. Normal placements and timeline switching consume none.
  When budget reaches zero, further branching is illegal.

*Temporal window (original §11):*
- Every level has a `TemporalWindow`. If PRESENT is `T`, a branch may only
  target historical states where `0 <= T - sourceTime <= TemporalWindow`,
  and also `sourceTime < PRESENT`, and `sourceTime < source.frontier`.
  Window size is level-configurable, but the core engine itself must never
  silently allow arbitrary historical jumps.

**Out of scope:** multi-timeline present/slot enforcement beyond what's
needed to run one branch (that's Phase 4 — for this phase it's fine if the
"second" timeline just always gets an active slot; don't build
slot-overflow handling yet, just don't block it structurally).

**Tests required (original "Temporal move" test category):**
- budget decreases exactly once per branch;
- budget exhaustion rejects further branches;
- temporal window enforced;
- future-state branching rejected;
- current-frontier branching rejected;
- locally illegal branch rejected;
- locally legal but globally impossible branch becomes `DEAD` immediately.
- **Scenario C** (original §19): a puzzle with a locally legal but globally
  impossible alternative value; create the branch; assert the child becomes
  `DEAD` immediately; assert the parent remains `ACTIVE` and solvable.

**Definition of done:** Scenario C passes; all budget/window edge cases
above are covered by tests. **Stop and wait for approval before Phase 4.**

---

## Phase 4 — Multi-Timeline Present, Active/Inactive Slot Enforcement, Full Victory/Game-Over

**Goal:** make Present, slots, victory, and game-over correct once
*multiple* timelines genuinely exist and compete for attention. This is
where the "time management" feel of the game actually shows up.

**Depends on:** Phase 3.

**Scope:**

*Present, full form (original §8):*
- `Present = min(frontier of every ACTIVE + active-slot timeline)`. If
  there are no ACTIVE timelines, use the level's configured terminal value
  only as an internal sentinel — victory/game-over logic must be evaluated
  separately, never inferred from that sentinel.
- Consequence to verify: after branching into a historical state, the
  child timeline can pull PRESENT backwards; the child must then be
  advanced through normal play before later timelines can become the
  PRESENT again. This is intentional.

*Active/inactive slots, full enforcement (original §9):*
- Each level has a maximum number of simultaneously ACTIVE timeline slots.
- When a branch is created: if a slot is available, the new timeline may
  become `ACTIVE` (subject to the immediate `DEAD`/`SOLVED` classification
  from Phase 3); if no slot is available, create it as `INACTIVE`.
- An `INACTIVE` timeline still exists in history and can be inspected,
  does not influence PRESENT, and cannot be played until explicitly
  activated. Activation is only allowed when a slot frees up. Treat this as
  a real game-state rule, not a UI-only concept.

*Victory, full form (original §14):*
- Any timeline reaching a valid, complete solution wins — not necessarily
  the root, and not necessarily every timeline.

*Game over, full form (original §15):*
- Game over occurs when the game is not already won and there are no
  `ACTIVE` timelines capable of further play. A `DEAD` or `INACTIVE`
  historical timeline alone must never count as playable, and the game
  must never end just because *one* branch died while another `ACTIVE`
  timeline is still solvable.

**Out of scope:** level validation layer, formal "time travel is optional"
proof (Phase 5).

**Tests required:**
- From "Present": present moves backward after a historical branch;
  present advances when the earliest active frontier advances; inactive
  timelines do not influence present.
- From "Timeline": parent/child relationship, branch metadata, correct
  branch copying, no parent mutation.
- From "Victory": solved child wins; a `SOLVED` timeline wins only if it
  currently occupies an `ACTIVE` slot — an `INACTIVE` solved timeline
  cannot end the level until activated.
- **Scenario B** (original §19): progress on the root timeline; branch
  from a historical state; assert parent history is unchanged; assert
  PRESENT can move backward; play the child timeline; assert both
  timelines remain distinct objects with immutable history.
- **Scenario E** (original §19): create more branches than there are
  active slots; assert the extras become `INACTIVE`; assert `INACTIVE`
  timelines do not change PRESENT.

**Definition of done:** Scenarios B and E both pass; a full multi-timeline
play session (branch, switch, let one die, win on a child) works end to
end through the public API. **Stop and wait for approval before Phase 5.**

---

## Phase 5 — Level Validation Layer & "Time Travel Is Optional"

**Goal:** a validation layer that can certify a level, and a concrete,
automated proof that Temporal Moves are always optional, never required.

**Depends on:** Phase 4.

**Scope:**

*Hard design requirement (original §13):* every level intended for release
must be solvable without using any Temporal Move. The core engine must
never require a Temporal Move merely to make ordinary progress in a valid
level. Temporal Moves may only ever *help* (explore an alternative without
destroying the original, isolate a risky hypothesis, recover from a bad
historical choice without a conventional Undo, or let advanced players
reason about alternative futures) — never gate progress.

*Level validation layer (original §16), implement at least these checks:*
1. Starting puzzle is valid.
2. Starting puzzle has at least one solution.
3. Release candidate puzzles should normally have exactly one standard
   Sudoku solution.
4. The puzzle has at least one complete solve path with **zero Temporal
   Moves**.
5. Optional Temporal Moves are legal and do not corrupt the original
   timeline.
6. Any branch that has zero Sudoku solutions becomes `DEAD` immediately.
7. At least one scenario demonstrates a useful but optional branch.
8. No allowed action can violate immutable-history invariants.
9. No action sequence can produce negative temporal budget.
10. PRESENT always equals the minimum frontier among `ACTIVE` timelines
    with active slots.

Do not invent cross-timeline Sudoku constraints just to make temporal play
"necessary" — that would violate the hard requirement above.

**Tests required:**
- **Scenario D** (original §19): demonstrate that a given level has a
  complete zero-time-travel solution; use a branch only as an optional
  alternate exploration on top of that.
- A level-validation tool/test that explicitly asserts "this level is
  solvable with zero Temporal Moves" for every release-candidate level
  fixture you test with.
- The 10 required checks above, each with at least one passing and one
  failing example where that makes sense (e.g. a puzzle rejected for
  having zero solutions).

**Definition of done:** Scenario D passes; the validation layer correctly
accepts a genuinely valid level and correctly rejects at least one
deliberately broken one per check. **Stop and wait for approval before
Phase 6.**

---

## Phase 6 — Randomized Stress Tests & 9×9 Validation

**Goal:** confidence that the rules hold under many random action
sequences, and that the engine isn't secretly a 4×4-only engine.

**Depends on:** Phase 5.

**Scope:**

*Randomized stress tests (original §18):*
- Deterministic randomized tests with a fixed default seed and a
  configurable seed override.
- For each random test: start from a known solvable 4×4 puzzle; perform
  legal placements; optionally branch when legal; switch timelines
  randomly; activate/deactivate timelines when legal; assert all
  invariants after every state-changing action.
- Also build a smaller, bounded 9×9 stress suite, since solver cost grows
  substantially at 9×9.
- Do not use uncontrolled brute force that makes the suite unreasonably
  slow. Prefer candidate-based generation, bounded random walks, cached
  solver results where appropriate, and deterministic seeds.

*9×9 validation (original §20):*
- Don't treat the 4×4 prototype as proof of 9×9 scalability. Run the same
  generic engine at 9×9 and validate at least one known unique-solution
  9×9 puzzle.
- Run a small bounded scenario demonstrating: ordinary 9×9 solving; a
  legal historical branch; immediate `DEAD` classification for a
  contradiction branch where applicable; correct PRESENT movement;
  immutable history.

**Tests required:**
- The full Scenario A–E suite (original §19), now re-run under randomized
  stress conditions rather than just as fixed hand-written cases.
- The bounded 9×9 scenario above.

**Definition of done:** stress suite runs in reasonable time on both board
sizes and reports zero invariant violations across all assertions; 9×9
scenario passes. **Stop and wait for approval before Phase 7.**

---

## Phase 7 — Persistence, Determinism Audit, Developer Diagnostics

**Goal:** the engine can save/load, is provably deterministic end to end,
and is debuggable from a terminal without any UI.

**Depends on:** Phase 6.

**Scope:**

*Persistence (original §22):*
- Game state must be serializable in a version-tolerant format for later
  save/load. At minimum, preserve: level id; Sudoku dimensions; all
  timeline ids; parent relationships; branch times; every historical board
  state needed to reconstruct the game; frontiers; statuses; active slots;
  PRESENT; remaining temporal budget; temporal window; final depth; move
  history if needed for replay/debugging.
- Use a stable DTO/save-model separate from runtime objects if
  appropriate.

*Determinism (original §23), audited retroactively across everything built
so far:*
- The same level definition + seed + starting state + action sequence must
  produce the same result, every time. This matters for debugging,
  replays, automated tests, future achievements/challenges, and
  cross-platform behavior.

*Developer diagnostics (original §24):*
- A text-based scenario runner (`Tools/ScenarioRunner/`) that can print a
  state summary, e.g.:
  ```
  PRESENT T3

  L0  ACTIVE   frontier=T5
  L1  ACTIVE   frontier=T3
  L2  DEAD     frontier=T2

  Budget: 1/3 used

  L1 board:
  1 . | 3 4
  . 4 | 1 .
  ...
  ```
- A compact timeline-tree view, e.g.:
  ```
  L0
  ├── L1
  │   └── L3
  └── L2 (DEAD)
  ```
- This is for debugging and automated scenario verification, not final UI.

**Tests required:**
- Save → load round-trip test: state before save equals state after load,
  for a game that includes at least one branch and one `DEAD` timeline.
- A determinism/replay test: same seed + same action sequence run twice →
  identical resulting state.
- A snapshot-style test (or manual check) confirming the scenario runner's
  text output matches the expected format for a small fixture scenario.

**Definition of done:** save/load round-trip test passes; determinism
replay test passes; scenario runner and timeline-tree view both produce
correct output for a fixture scenario. **Stop and wait for approval before
Phase 8.**

---

## Phase 8 — Final Acceptance Pass

**Goal:** formal sign-off against the original acceptance criteria — not
"it compiles," but every specific claim checked.

**Depends on:** Phase 7.

**Scope — re-verify, don't re-implement:**

*"Do not implement yet" audit (original §25) — confirm none of these crept
in anywhere:* multiplayer, online services, achievements, monetization,
ads, sound, animations, final art, Steam integration, iOS/Android platform
code, cross-timeline magic constraints, causal echoes, timeline merging,
artificial paradox rules, normal gameplay Undo.

*Acceptance criteria (original §27) — all must be true:*
- all unit tests pass;
- randomized invariant tests pass;
- at least one complete 4×4 level is solved with zero Temporal Moves;
- at least one 4×4 scenario demonstrates an optional useful branch;
- at least one locally legal but globally impossible branch is immediately
  classified `DEAD`;
- 9×9 solving works on a known unique-solution puzzle;
- immutable history is verified automatically;
- PRESENT invariants are verified automatically;
- temporal budget/window rules are verified automatically;
- no normal Undo exists in gameplay logic;
- no speculative cross-timeline mechanics have been added;
- the core has no UnityEngine dependency.

**Final report:** print a concise test report including: number of unit
tests passed; number of randomized scenarios/actions run; 4×4 results;
9×9 results; any known performance limitations; any remaining design
assumptions.

**Definition of done:** the acceptance-criteria list above is entirely
checked off, with the final report printed. This is the end of the
core-prototype phase — Unity integration is a new, separate effort and
starts a new plan.

---

## Rule decisions made during implementation

The phase texts above are the original specification and are left as written.
This log records the points where implementation forced a decision the spec did
not settle, so later phases work from the resolved rule instead of re-deriving
the old one. Each entry is covered by a named test.

### D1 — A lost run still allows Temporal Moves (decided during Phase 3)

Only a **won** run is terminal. §15 game over means ordinary placements are
exhausted, not that the run is finished: while temporal budget and a reachable
historical state remain, a Temporal Move may branch off history, and a playable
branch puts the run back in progress.

*Why:* §13 lists "recover from a bad historical choice without a conventional
Undo" as a purpose of Temporal Moves. Refusing them once ordinary play dead-ends
would make that purpose unreachable in exactly the situation it exists for.

*Tests:* `ALostRunStillAllowsTemporalMoves`, `ATemporalMoveCanBringALostRunBack`,
`AWonRunRefusesTemporalMoves`.

### D2 — A timeline with no legal placement left is DEAD (decided during Phase 3)

A normal placement classifies the resulting state: complete and valid → `SOLVED`,
no legal placement anywhere → `DEAD`, otherwise `ACTIVE`. This is a *local*
check; unlike branch classification (§7) it does not run the solver, so a
timeline whose board has no completion but still offers placements stays
`ACTIVE` and the player discovers the mistake by playing.

*Why:* without this, a timeline that had run out of moves stayed `ACTIVE`, kept
counting towards PRESENT, and pinned it at its own frontier forever — so every
other timeline was blocked from advancing past it, in a run that could no longer
end. That soft-lock is reachable as soon as D1 allows a rescue branch.

*Tests:* `ATimelineThatCanNoLongerBePlayedStopsHoldingThePresentBack`,
`ATimelineThatIsDoomedButStillPlayableStaysActive`,
`OrdinaryPlayAsksALocalQuestionWhereBranchingAsksAGlobalOne`.

### D3 — With no PRESENT, a branch measures from the source frontier (decided during Phase 3)

§8's "configured terminal value" sentinel is modelled as an absent PRESENT
(`int?`) rather than a magic number, so no code can infer victory or game over
from it. Branch conditions 5 and 6 (§6, §11) then need a stand-in when nothing
is active: the **source timeline's own frontier**, which is where that line of
history actually ended. While a PRESENT exists, neither condition changes.

*Tests:* `WithNoPresentTheWindowIsMeasuredFromTheSourceTimelinesOwnFrontier`,
`AnAbsentPresentDoesNotByItselfSayHowTheRunEnded`.

---

## Reference: original design note

The game is inspired by the *concepts* of 5D Chess With Multiverse Time
Travel (immutable historical states, timelines, branching, a
present/frontier, switching between timelines) applied to Sudoku. It is an
original game: do not copy code, assets, UI, names, or proprietary
implementation details from 5D Chess at any phase.
