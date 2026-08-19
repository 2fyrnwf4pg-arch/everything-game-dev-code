using System.Collections.Generic;
using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;

namespace FiveDSudoku.Tests;

/// <summary>
/// Scenario E — the acceptance test for slot pressure.
///
/// Make more branches than the level has active slots, and confirm the extras
/// are parked as inactive rather than crowding in, and that being parked really
/// does keep them out of the run: they must not drag the present back to their
/// own early frontiers.
/// </summary>
[TestFixture]
public sealed class ScenarioETests
{
    /// <summary>A 4x4 with a single given, so branches are plentiful and playable.</summary>
    private const string SparseBoard = """
        1.|..
        ..|..
        --+--
        ..|..
        ..|..
        """;

    [Test]
    public void BranchesBeyondTheAvailableSlotsAreParkedAndDoNotMoveThePresent()
    {
        // --- arrange: two active slots, one of which the root takes ------
        GameState game = GameState.Start(
            Levels.FromText(
                "sparse",
                BoardSize.FourByFour,
                SparseBoard,
                temporalBudget: 4,
                temporalWindow: 16,
                maxActiveTimelines: 2));

        Assert.That(game.FreeActiveSlots, Is.EqualTo(1));

        // Advance the root so that T0 is well behind the present.
        for (int move = 0; move < 3; move++)
        {
            game = Levels.PlayAnyLegalMove(game);
        }

        Assert.That(game.Present, Is.EqualTo(3));

        // --- act: the first branch finds the last slot free --------------
        TemporalMoveResult first = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3);

        Assert.That(first.Succeeded, Is.True, first.Rejection.ToString());

        GameState afterFirst = first.State;
        Timeline firstBranch = afterFirst.GetTimeline(first.NewTimelineId!.Value);

        Assert.That(firstBranch.Status, Is.EqualTo(TimelineStatus.Active));
        Assert.That(firstBranch.OccupiesActiveSlot, Is.True);
        Assert.That(afterFirst.FreeActiveSlots, Is.EqualTo(0));
        Assert.That(afterFirst.Present, Is.EqualTo(1), "the new branch pulled the present back");

        // Bring it level with the root again so the present is back at T3.
        GameState onBranch = afterFirst.SelectTimeline(firstBranch.Id);
        onBranch = Levels.PlayAnyLegalMove(onBranch);
        onBranch = Levels.PlayAnyLegalMove(onBranch);

        Assert.That(onBranch.GetTimeline(firstBranch.Id).FrontierTime, Is.EqualTo(3));
        Assert.That(onBranch.Present, Is.EqualTo(3));

        // --- act: two more branches, with no slot left -------------------
        List<int> parked = new List<int>();
        GameState game3 = onBranch;

        foreach (int value in new[] { 4, 2 })
        {
            TemporalMoveResult extra = game3.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, value);

            Assert.That(extra.Succeeded, Is.True, $"value {value}: {extra.Rejection}");

            game3 = extra.State;
            parked.Add(extra.NewTimelineId!.Value);
        }

        // --- assert: the extras are inactive -----------------------------
        Assert.That(game3.Timelines, Has.Count.EqualTo(4));
        Assert.That(game3.FreeActiveSlots, Is.EqualTo(0));

        foreach (int id in parked)
        {
            Timeline timeline = game3.GetTimeline(id);

            Assert.That(timeline.Status, Is.EqualTo(TimelineStatus.Inactive), $"L{id}");
            Assert.That(timeline.OccupiesActiveSlot, Is.False, $"L{id}");
            Assert.That(timeline.IsCapableOfFurtherPlay, Is.False, $"L{id}");
            Assert.That(timeline.FrontierTime, Is.EqualTo(1), $"L{id} sits far behind the present");
        }

        // --- assert: being parked keeps them out of the present ----------
        Assert.That(
            game3.Present,
            Is.EqualTo(3),
            "the parked branches sit at T1 and would have dragged the present back if they counted");

        Assert.That(game3.Outcome, Is.EqualTo(GameOutcome.InProgress));
        Assert.That(game3.RemainingTemporalBudget, Is.EqualTo(1));

        // --- assert: and out of play, until a slot frees up --------------
        foreach (int id in parked)
        {
            Assert.That(
                game3.ActivateTimeline(id).Rejection,
                Is.EqualTo(TimelineActivationRejection.NoFreeActiveSlot),
                $"L{id}");
            Assert.That(
                game3.SelectTimeline(id).PlaceValue(3, 3, 4).Rejection,
                Is.EqualTo(MoveRejection.TimelineNotActive),
                $"L{id}");
        }
    }
}
