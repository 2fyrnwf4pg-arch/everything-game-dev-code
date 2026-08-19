using System;
using System.Collections.Generic;
using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;

namespace FiveDSudoku.Core.Validation;

/// <summary>
/// Checks a run against the rules it makes about itself.
///
/// <see cref="Check"/> looks at a single state; <see cref="CheckTransition"/>
/// looks at a pair and catches the things only a pair can show — history that
/// changed under the player's feet, budget that went back up, a timeline that
/// vanished. Together they are meant to be run after every state-producing
/// action, which is what makes a long random play session worth anything.
/// </summary>
public static class GameInvariants
{
    /// <summary>Everything wrong with a single state, or an empty list.</summary>
    public static IReadOnlyList<InvariantViolation> Check(GameState state)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        List<InvariantViolation> violations = new List<InvariantViolation>();

        CheckBudgetBounds(state, violations);
        CheckPresent(state, violations);

        return violations;
    }

    /// <summary>True when a state breaks nothing.</summary>
    public static bool Holds(GameState state) => Check(state).Count == 0;

    /// <summary>
    /// Everything wrong with <paramref name="after"/> on its own, plus everything
    /// wrong about how it relates to <paramref name="before"/>.
    /// </summary>
    public static IReadOnlyList<InvariantViolation> CheckTransition(GameState before, GameState after)
    {
        if (before is null)
        {
            throw new ArgumentNullException(nameof(before));
        }

        if (after is null)
        {
            throw new ArgumentNullException(nameof(after));
        }

        List<InvariantViolation> violations = new List<InvariantViolation>(Check(after));

        CheckBudgetDidNotGrow(before, after, violations);
        CheckHistoryOnlyGrew(before, after, violations);

        return violations;
    }

    private static void CheckBudgetBounds(GameState state, List<InvariantViolation> violations)
    {
        if (state.RemainingTemporalBudget < 0)
        {
            violations.Add(new InvariantViolation(
                GameInvariant.TemporalBudgetWithinBounds,
                $"Remaining budget is {state.RemainingTemporalBudget}, which is below zero."));
        }

        if (state.RemainingTemporalBudget > state.Level.TemporalBudget)
        {
            violations.Add(new InvariantViolation(
                GameInvariant.TemporalBudgetWithinBounds,
                $"Remaining budget is {state.RemainingTemporalBudget}, more than the " +
                $"{state.Level.TemporalBudget} the level granted."));
        }
    }

    private static void CheckPresent(GameState state, List<InvariantViolation> violations)
    {
        int? expected = null;

        foreach (Timeline timeline in state.Timelines)
        {
            if (timeline.Status != TimelineStatus.Active || !timeline.OccupiesActiveSlot)
            {
                continue;
            }

            if (expected is null || timeline.FrontierTime < expected.Value)
            {
                expected = timeline.FrontierTime;
            }
        }

        if (state.Present != expected)
        {
            violations.Add(new InvariantViolation(
                GameInvariant.PresentMatchesTheMinimumActiveFrontier,
                $"Present is {Describe(state.Present)} but the earliest active frontier is {Describe(expected)}."));
        }
    }

    private static void CheckBudgetDidNotGrow(
        GameState before,
        GameState after,
        List<InvariantViolation> violations)
    {
        if (after.RemainingTemporalBudget > before.RemainingTemporalBudget)
        {
            violations.Add(new InvariantViolation(
                GameInvariant.TemporalBudgetWithinBounds,
                $"Remaining budget went up from {before.RemainingTemporalBudget} " +
                $"to {after.RemainingTemporalBudget}."));
        }
    }

    private static void CheckHistoryOnlyGrew(
        GameState before,
        GameState after,
        List<InvariantViolation> violations)
    {
        foreach (Timeline earlier in before.Timelines)
        {
            if (!after.HasTimeline(earlier.Id))
            {
                violations.Add(new InvariantViolation(
                    GameInvariant.ImmutableHistory,
                    $"Timeline L{earlier.Id} is gone."));
                continue;
            }

            Timeline later = after.GetTimeline(earlier.Id);

            if (later.FirstStateTime != earlier.FirstStateTime)
            {
                violations.Add(new InvariantViolation(
                    GameInvariant.ImmutableHistory,
                    $"L{earlier.Id} moved its time axis from T{earlier.FirstStateTime} " +
                    $"to T{later.FirstStateTime}."));
                continue;
            }

            if (later.StateCount < earlier.StateCount)
            {
                violations.Add(new InvariantViolation(
                    GameInvariant.ImmutableHistory,
                    $"L{earlier.Id} lost states: {earlier.StateCount} became {later.StateCount}."));
                continue;
            }

            if (later.ParentId != earlier.ParentId || later.BranchTime != earlier.BranchTime)
            {
                violations.Add(new InvariantViolation(
                    GameInvariant.ImmutableHistory,
                    $"L{earlier.Id} changed where it came from."));
            }

            for (int time = earlier.FirstStateTime; time <= earlier.FrontierTime; time++)
            {
                SudokuBoard was = earlier.StateAt(time);
                SudokuBoard now = later.StateAt(time);

                if (!was.Equals(now))
                {
                    violations.Add(new InvariantViolation(
                        GameInvariant.ImmutableHistory,
                        $"L{earlier.Id} rewrote the state at T{time}."));
                }
            }
        }
    }

    private static string Describe(int? time) => time is null ? "absent" : $"T{time.Value}";
}
