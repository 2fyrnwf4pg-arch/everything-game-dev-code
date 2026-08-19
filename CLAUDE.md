# CLAUDE.md — 5D Sudoku Core Prototype

## Project

**5D Sudoku** is an original game inspired by the *concepts* of 5D Chess With
Multiverse Time Travel — immutable historical states, timelines, branching,
a present/frontier, and switching between timelines — applied to Sudoku
instead of chess.

Do not copy code, assets, UI text, names, or proprietary implementation
details from 5D Chess. Only the high-level concepts are shared inspiration;
everything else here is original design.

## Current milestone

We are building the **core game-logic prototype**: an engine-independent C#
core library, with automated tests, that will later be embedded in Unity for
Steam/Windows, iOS, and Android.

No graphics, no Unity, no platform code yet. This phase is done when the
deterministic rules engine is correct and fully tested.

The detailed, phase-by-phase implementation plan lives in `PHASES.md`.
Always check which phase is currently open before writing code, and don't
start a phase that hasn't been explicitly approved.

## Coordinates

Every board state conceptually sits at `(X, Y, T, L)`:

- `X`, `Y` — Sudoku column/row (the actual puzzle grid)
- `T` — time/state index within a timeline
- `L` — timeline identifier

`T` and `L` locate a board in the multiverse; they are **not** extra Sudoku
dimensions. Row/column/box constraints are always local to a single
`(X, Y)` board.

## Non-negotiable constraints

- **No UnityEngine dependency** anywhere in `Core/`. Plain C# types,
  records, structs, collections only.
- **Determinism**: the same level definition + seed + starting state +
  action sequence must always produce the same result.
- **Immutability**: every played Sudoku state is a value object that is
  never mutated in place. A move produces a *new* state; history is
  preserved and inspectable.
- **No normal gameplay Undo.** The only way to revisit a past decision is a
  Temporal Move, which costs temporal budget and leaves the original
  timeline untouched.
- **Time travel is always optional**, never required to finish a level.
  Every release-candidate level must have at least one complete solve path
  using zero Temporal Moves.
- Preserve the rules in `PHASES.md` exactly as written — they were already
  stress-tested against a Python prototype. If a test proves an internal
  contradiction, stop, document it, add a regression test, and only then
  make the smallest necessary rule change. Never silently work around a
  contradiction.

## Explicitly NOT part of this phase

Do not implement, even if it seems convenient: multiplayer, online
services, achievements, monetization, ads, sound, animations, final art,
Steam integration, iOS/Android platform code, cross-timeline magic
constraints, causal echoes, timeline merging, artificial paradox rules.
Do not invent speculative mechanics (temporal cells, timeline gates,
cross-timeline Sudoku constraints) that aren't in the spec.

## Architecture

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

The UI layer (future Unity project) must not contain game rules — it only
calls into `Core`.

## Workflow

1. Work through `PHASES.md` **in order**, one phase at a time.
2. At the end of each phase: run the full test suite, print a short
   pass/fail summary, and **stop**. Wait for explicit approval before
   starting the next phase.
3. Don't reach ahead into a later phase's rules "for convenience" — if
   Phase 2 code seems to want something from Phase 4, flag it instead of
   implementing it early.
4. Before declaring a phase done, re-read its "Definition of done" in
   `PHASES.md`. Compiling is not done. Passing tests is not done unless the
   specific tests listed for that phase pass.

## Testing philosophy

- Every phase ships with its own tests; don't defer testing to "later".
- Keep regression tests for every bug found during development — never
  delete them once added.
- Randomized/stress tests use a fixed default seed with a configurable
  override, bounded random walks (not uncontrolled brute force), and should
  stay fast enough to run routinely.
