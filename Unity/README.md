# Unity front end

The Unity project for 5D Sudoku. The rules engine is not here — it lives in
`Core/` at the repository root and arrives as a compiled assembly. See
`UNITY-PHASES.md` for the milestone plan; this file is only what you need to
open the project.

## Opening it

The project folder is **`Unity/FiveDSudoku`**, not the repository root. Unity
opens a project, and the repository holds more than one thing — pointing Unity at
the root will not work.

The built core assembly is committed, so a fresh clone opens without running
anything first.

1. **Unity Hub → Add → Add project from disk →** pick `Unity/FiveDSudoku`.

2. **Version.** The project asks for **6000.5.9f1**. To move it to a different
   editor:

   ```
   Unity/set-unity-version.sh <version>
   ```

   Opening with a different editor works too — Unity offers to upgrade and
   rewrites the file itself, which is safe here because there is no scene, no
   prefab and no serialized asset to migrate yet.

3. **Check API Compatibility Level is .NET Standard 2.1**
   (*Project Settings → Player → Other Settings*). Modern Unity defaults to it,
   so this is usually already right; the core will not load otherwise. An
   EditMode test fails if it is wrong, so it is checked rather than remembered.

4. **Run the tests.** *Window → General → Test Runner → EditMode → Run All.*

If Package Manager cannot resolve the `com.unity.test-framework` version in
`Packages/manifest.json`, take the newest available one. Nothing here depends on
the exact version.

### Command line, if you prefer

```
<editor> -projectPath Unity/FiveDSudoku \
         -runTests -testPlatform EditMode \
         -testResults /tmp/editmode.xml -batchmode -logFile -
```

## Keeping the core up to date

Any change under `Core/` needs the assembly rebuilt:

```
Unity/build-core-dll.sh            # rebuild and copy
Unity/build-core-dll.sh --check    # fail if the copy is out of date
```

`--check` compares a fingerprint of the `Core/` sources against the one recorded
next to the assembly. Run it in CI. It exists because a stale assembly does not
announce itself — the project keeps compiling and quietly plays by the old rules.

Fingerprinting the sources rather than the assembly is deliberate: two builds of
identical sources are not byte-identical, so comparing assemblies would report a
difference every time and teach everyone to ignore it.

## Layout

```
Unity/
  build-core-dll.sh               builds Core and copies it into Assets/Plugins
  FiveDSudoku/                    the Unity project
    Assets/Plugins/FiveDSudoku/   the built core — generated, never hand-edited
    Assets/FiveDSudoku/
      Runtime/                    game and presentation code (empty until U1)
      Editor/                     editor tooling (empty until U6)
      Tests/EditMode/             the smoke suite
  Verification/                   see below
```

## Why there are two extra csproj files

`Unity/Verification/` holds two small projects that compile the EditMode test
sources outside Unity. They exist because the thing most likely to break this
integration — using a language or API feature Unity does not have — is otherwise
only discoverable on a machine with the editor installed.

- `CompileCheck` builds the same files as `netstandard2.1` with `LangVersion 9`,
  which is exactly what Unity has. A failure here means the tests use something
  Unity's compiler or API surface does not.
- `RunCheck` runs them against NUnit 3, the version Unity's Test Framework
  ships, on a runtime that can execute them.

Both are in `FiveDSudoku.sln`, so `dotnet build` and `dotnet test` cover them.
Neither replaces running the suite in Unity's own Test Runner; they narrow what
that run can still surprise you with.

## What the smoke suite is for

It is not a copy of the real test suite. The 337 tests under `Tests/` prove the
rules, and they run through `dotnet test`. The smoke suite answers a different
question: does that same assembly load and behave identically once Unity is
hosting it. Keep it small.

## Open decisions

These were listed in `UNITY-PHASES.md` as U0 decisions and still need making,
because they depend on what is installed and on design intent:

- **Render pipeline** (URP recommended) — add the package when U1 needs it.
- **Input** (Input System recommended) — add the package when U2 needs it.
- **UI approach** (UI Toolkit for menus, sprites or mesh for the board).

Once settled, write them into `CLAUDE.md` next to the Phase 0 decisions and stop
re-opening them.
