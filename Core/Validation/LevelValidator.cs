using System;
using System.Collections.Generic;
using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Level;
using FiveDSudoku.Core.Solver;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;

namespace FiveDSudoku.Core.Validation;

/// <summary>
/// Certifies a level by playing it.
///
/// The puzzle checks could be answered by looking at the board, but the rest
/// cannot: "an optional branch leaves the original intact" and "no action
/// corrupts history" are claims about what the rules do, so validation drives a
/// real <see cref="GameState"/> through the public API and watches. Every action
/// it takes is one a player could take.
///
/// It is deterministic — same level in, same report out — and bounded: branch
/// exploration stops as soon as it has seen what it needs, so a 9x9 level costs
/// a handful of solver calls rather than a sweep of every cell and value.
/// </summary>
public static class LevelValidator
{
    /// <summary>
    /// How many candidate branches to try before giving up on finding the witnesses
    /// checks 6 and 7 need. Generous for a 4x4 and enough for a 9x9, where the first
    /// few empty cells already offer both a right value and a wrong one.
    /// </summary>
    private const int MaxBranchAttempts = 24;

    /// <summary>Runs every check against a level and reports what it found.</summary>
    public static LevelValidationReport Validate(LevelDefinition level)
    {
        if (level is null)
        {
            throw new ArgumentNullException(nameof(level));
        }

        List<LevelValidationFinding> findings = new List<LevelValidationFinding>();
        List<InvariantViolation> violations = new List<InvariantViolation>();

        SudokuBoard board = level.StartingBoard;

        // ---- 1..3: the puzzle on its own --------------------------------
        bool isValid = board.IsValid();
        findings.Add(new LevelValidationFinding(
            LevelValidationCheck.StartingPuzzleIsValid,
            isValid,
            isValid ? "No row, column or box is repeated." : "The starting puzzle repeats a value."));

        int solutionCount = isValid ? SudokuSolver.CountSolutions(board, 2) : 0;

        findings.Add(new LevelValidationFinding(
            LevelValidationCheck.StartingPuzzleHasASolution,
            solutionCount >= 1,
            solutionCount >= 1
                ? "The starting puzzle can be completed."
                : "The starting puzzle has no completion at all."));

        findings.Add(new LevelValidationFinding(
            LevelValidationCheck.StartingPuzzleHasExactlyOneSolution,
            solutionCount == 1,
            solutionCount == 1
                ? "Exactly one completion."
                : solutionCount == 0
                    ? "No completion, so it cannot have exactly one."
                    : "More than one completion; a release candidate should have exactly one."));

        // ---- 4: solvable with no time travel ----------------------------
        SudokuBoard? solution = solutionCount >= 1 ? SudokuSolver.FindFirstSolution(board) : null;
        GameState? afterFirstMove = null;

        findings.Add(SolveWithoutTemporalMoves(level, solution, violations, out afterFirstMove));

        // ---- 5..7: what branching does ----------------------------------
        findings.AddRange(ExploreBranches(afterFirstMove, violations));

        // ---- 8..10: invariants seen along the way -----------------------
        findings.Add(InvariantFinding(
            LevelValidationCheck.ImmutableHistoryHolds,
            GameInvariant.ImmutableHistory,
            violations));
        findings.Add(InvariantFinding(
            LevelValidationCheck.TemporalBudgetNeverGoesNegative,
            GameInvariant.TemporalBudgetWithinBounds,
            violations));
        findings.Add(InvariantFinding(
            LevelValidationCheck.PresentMatchesTheMinimumActiveFrontier,
            GameInvariant.PresentMatchesTheMinimumActiveFrontier,
            violations));

        return new LevelValidationReport(level, findings.ToArray());
    }

    /// <summary>
    /// Plays the level to a win with ordinary placements only, and hands back the
    /// state right after the first move so branch exploration has a historical
    /// state to reach for.
    /// </summary>
    private static LevelValidationFinding SolveWithoutTemporalMoves(
        LevelDefinition level,
        SudokuBoard? solution,
        List<InvariantViolation> violations,
        out GameState? afterFirstMove)
    {
        afterFirstMove = null;

        if (solution is null)
        {
            return new LevelValidationFinding(
                LevelValidationCheck.SolvableWithoutTemporalMoves,
                false,
                "There is no solution to play towards.");
        }

        GameState game = GameState.Start(level);
        violations.AddRange(GameInvariants.Check(game));

        SudokuBoard start = level.StartingBoard;
        BoardSize size = level.Size;
        int movesPlayed = 0;

        for (int row = 0; row < size.Side; row++)
        {
            for (int column = 0; column < size.Side; column++)
            {
                if (!start.IsEmpty(row, column))
                {
                    continue;
                }

                MoveResult result = game.PlaceValue(row, column, solution[row, column]);

                if (!result.Succeeded)
                {
                    return new LevelValidationFinding(
                        LevelValidationCheck.SolvableWithoutTemporalMoves,
                        false,
                        $"Placing {solution[row, column]} at r{row}c{column} was refused: {result.Rejection}.");
                }

                violations.AddRange(GameInvariants.CheckTransition(game, result.State));
                game = result.State;
                movesPlayed++;

                if (movesPlayed == 1)
                {
                    afterFirstMove = game;
                }
            }
        }

        if (game.Outcome != GameOutcome.Won)
        {
            return new LevelValidationFinding(
                LevelValidationCheck.SolvableWithoutTemporalMoves,
                false,
                $"Filling every cell left the run {game.Outcome}.");
        }

        if (game.RemainingTemporalBudget != level.TemporalBudget)
        {
            return new LevelValidationFinding(
                LevelValidationCheck.SolvableWithoutTemporalMoves,
                false,
                $"The solve spent budget: {game.RemainingTemporalBudget} of {level.TemporalBudget} left.");
        }

        return new LevelValidationFinding(
            LevelValidationCheck.SolvableWithoutTemporalMoves,
            true,
            $"Won in {movesPlayed} ordinary placements with all {level.TemporalBudget} " +
            "units of temporal budget untouched.");
    }

    /// <summary>
    /// Tries branches off the earliest historical state until it has seen both an
    /// impossible one and a worthwhile one, checking along the way that branching
    /// never disturbs the timeline it came from.
    /// </summary>
    private static IEnumerable<LevelValidationFinding> ExploreBranches(
        GameState? afterFirstMove,
        List<InvariantViolation> violations)
    {
        if (afterFirstMove is null)
        {
            const string reason = "The level never reached a historical state to branch from.";

            return new[]
            {
                new LevelValidationFinding(LevelValidationCheck.OptionalBranchesLeaveTheOriginalIntact, false, reason),
                new LevelValidationFinding(LevelValidationCheck.ImpossibleBranchesDieImmediately, false, reason),
                new LevelValidationFinding(LevelValidationCheck.AUsefulButOptionalBranchExists, false, reason),
            };
        }

        GameState game = afterFirstMove;
        Timeline root = game.GetTimeline(GameState.RootTimelineId);
        SudokuBoard sourceBoard = root.StateAt(0);
        BoardSize size = game.Level.Size;

        int attempted = 0;
        int corrupted = 0;
        string? impossibleWitness = null;
        string? impossibleFailure = null;
        string? usefulWitness = null;
        GameState? withUsefulBranch = null;
        int usefulBranchId = -1;

        for (int row = 0; row < size.Side && attempted < MaxBranchAttempts; row++)
        {
            for (int column = 0; column < size.Side && attempted < MaxBranchAttempts; column++)
            {
                for (int value = size.MinValue; value <= size.MaxValue; value++)
                {
                    if (attempted >= MaxBranchAttempts ||
                        (impossibleWitness is not null && usefulWitness is not null))
                    {
                        break;
                    }

                    if (game.ValidateTemporalMove(GameState.RootTimelineId, 0, row, column, value)
                        != TemporalMoveRejection.None)
                    {
                        continue;
                    }

                    TemporalMoveResult result = game.PerformTemporalMove(
                        GameState.RootTimelineId, 0, row, column, value);

                    attempted++;

                    IReadOnlyList<InvariantViolation> branchViolations =
                        GameInvariants.CheckTransition(game, result.State);
                    violations.AddRange(branchViolations);

                    // "Did the branch corrupt the original" is the immutable-history
                    // guarantee asked over exactly this one action, so it is answered
                    // by comparing histories rather than by demanding that the parent
                    // came back as the very same object.
                    foreach (InvariantViolation violation in branchViolations)
                    {
                        if (violation.Invariant == GameInvariant.ImmutableHistory)
                        {
                            corrupted++;
                            break;
                        }
                    }

                    Timeline branch = result.State.GetTimeline(result.NewTimelineId!.Value);
                    bool completable = SudokuSolver.HasSolution(branch.Frontier);
                    string where = $"{value} at r{row}c{column}";

                    if (!completable)
                    {
                        if (branch.Status == TimelineStatus.Dead)
                        {
                            impossibleWitness ??= where;
                        }
                        else
                        {
                            impossibleFailure ??= $"{where} has no completion but came out {branch.Status}.";
                        }
                    }
                    else if (branch.Status == TimelineStatus.Active && usefulWitness is null)
                    {
                        usefulWitness = where;
                        withUsefulBranch = result.State;
                        usefulBranchId = branch.Id;
                    }
                }
            }
        }

        SeparateTheFrontiers(withUsefulBranch, usefulBranchId, violations);

        return new[]
        {
            OptionalBranchFinding(attempted, corrupted),
            ImpossibleBranchFinding(attempted, impossibleWitness, impossibleFailure),
            UsefulBranchFinding(attempted, usefulWitness),
        };
    }

    /// <summary>
    /// Advances the branch one step so that two active timelines sit at different
    /// frontiers, and checks the invariants there.
    ///
    /// Without this the run never has two active frontiers that disagree, and the
    /// claim that the present is the *earliest* of them says nothing — a present
    /// taken from the latest instead would look exactly the same.
    /// </summary>
    private static void SeparateTheFrontiers(
        GameState? withUsefulBranch,
        int branchId,
        List<InvariantViolation> violations)
    {
        if (withUsefulBranch is null)
        {
            return;
        }

        GameState onBranch = withUsefulBranch.SelectTimeline(branchId);
        BoardSize size = onBranch.Level.Size;

        for (int row = 0; row < size.Side; row++)
        {
            for (int column = 0; column < size.Side; column++)
            {
                for (int value = size.MinValue; value <= size.MaxValue; value++)
                {
                    MoveResult moved = onBranch.PlaceValue(row, column, value);

                    if (!moved.Succeeded)
                    {
                        continue;
                    }

                    violations.AddRange(GameInvariants.CheckTransition(onBranch, moved.State));

                    return;
                }
            }
        }
    }

    private static LevelValidationFinding OptionalBranchFinding(int attempted, int corrupted)
    {
        if (attempted == 0)
        {
            return new LevelValidationFinding(
                LevelValidationCheck.OptionalBranchesLeaveTheOriginalIntact,
                false,
                "No legal Temporal Move was available, so the guarantee could not be shown.");
        }

        return new LevelValidationFinding(
            LevelValidationCheck.OptionalBranchesLeaveTheOriginalIntact,
            corrupted == 0,
            corrupted == 0
                ? $"{attempted} branch(es) taken, each leaving the source timeline untouched."
                : $"{corrupted} of {attempted} branch(es) disturbed the source timeline.");
    }

    private static LevelValidationFinding ImpossibleBranchFinding(
        int attempted,
        string? witness,
        string? failure)
    {
        if (failure is not null)
        {
            return new LevelValidationFinding(
                LevelValidationCheck.ImpossibleBranchesDieImmediately, false, failure);
        }

        if (witness is not null)
        {
            return new LevelValidationFinding(
                LevelValidationCheck.ImpossibleBranchesDieImmediately,
                true,
                $"Branching on {witness} has no completion and was reported dead at once.");
        }

        return new LevelValidationFinding(
            LevelValidationCheck.ImpossibleBranchesDieImmediately,
            false,
            attempted == 0
                ? "No legal Temporal Move was available, so the rule could not be shown."
                : $"None of the {attempted} branch(es) tried was impossible, so the rule could not be shown.");
    }

    private static LevelValidationFinding UsefulBranchFinding(int attempted, string? witness)
    {
        if (witness is not null)
        {
            return new LevelValidationFinding(
                LevelValidationCheck.AUsefulButOptionalBranchExists,
                true,
                $"Branching on {witness} leads somewhere still winnable, on top of a solve path that needed no branch.");
        }

        return new LevelValidationFinding(
            LevelValidationCheck.AUsefulButOptionalBranchExists,
            false,
            attempted == 0
                ? "No legal Temporal Move was available, so the level offers no optional branch."
                : $"None of the {attempted} branch(es) tried led anywhere still winnable.");
    }

    private static LevelValidationFinding InvariantFinding(
        LevelValidationCheck check,
        GameInvariant invariant,
        List<InvariantViolation> violations)
    {
        List<string> matching = new List<string>();

        for (int index = 0; index < violations.Count; index++)
        {
            if (violations[index].Invariant == invariant)
            {
                matching.Add(violations[index].Detail);
            }
        }

        return new LevelValidationFinding(
            check,
            matching.Count == 0,
            matching.Count == 0
                ? "Held after every action taken while validating."
                : $"{matching.Count} violation(s), first: {matching[0]}");
    }
}
