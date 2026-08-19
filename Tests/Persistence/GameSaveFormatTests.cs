using System;
using System.Linq;
using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Level;
using FiveDSudoku.Core.Persistence;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;

namespace FiveDSudoku.Tests;

/// <summary>
/// Saving and loading a run.
/// </summary>
[TestFixture]
public sealed class GameSaveFormatTests
{
    /// <summary>
    /// A run worth saving: history on the root, one branch that died on arrival,
    /// and one still in play, so every kind of thing a save has to carry is there.
    /// </summary>
    private static GameState BuildRunWithABranchAndADeadTimeline()
    {
        GameState game = GameState.Start(
            Levels.SolvableFourByFour(temporalBudget: 3, temporalWindow: 4, maxActiveTimelines: 3));

        game = game.PlaceValue(0, 1, 4).State;
        game = game.PlaceValue(0, 2, 3).State;

        TemporalMoveResult dead = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3);
        Assert.That(dead.Succeeded, Is.True, dead.Rejection.ToString());
        game = dead.State;

        TemporalMoveResult alive = game.PerformTemporalMove(GameState.RootTimelineId, 1, 0, 2, 3);
        Assert.That(alive.Succeeded, Is.True, alive.Rejection.ToString());
        game = alive.State;

        Assert.That(game.Timelines.Select(timeline => timeline.Status), Does.Contain(TimelineStatus.Dead));
        Assert.That(game.Timelines, Has.Count.EqualTo(3));

        return game;
    }

    private static void AssertSameRun(GameState expected, GameState actual)
    {
        Assert.That(actual.Level.Id, Is.EqualTo(expected.Level.Id));
        Assert.That(actual.Level.Size, Is.EqualTo(expected.Level.Size));
        Assert.That(actual.Level.StartingBoard, Is.EqualTo(expected.Level.StartingBoard));
        Assert.That(actual.Level.TemporalBudget, Is.EqualTo(expected.Level.TemporalBudget));
        Assert.That(actual.Level.TemporalWindow, Is.EqualTo(expected.Level.TemporalWindow));
        Assert.That(actual.Level.MaxActiveTimelines, Is.EqualTo(expected.Level.MaxActiveTimelines));
        Assert.That(actual.Level.FinalDepth, Is.EqualTo(expected.Level.FinalDepth));

        Assert.That(actual.SelectedTimelineId, Is.EqualTo(expected.SelectedTimelineId));
        Assert.That(actual.RemainingTemporalBudget, Is.EqualTo(expected.RemainingTemporalBudget));
        Assert.That(actual.Present, Is.EqualTo(expected.Present));
        Assert.That(actual.Outcome, Is.EqualTo(expected.Outcome));
        Assert.That(actual.FreeActiveSlots, Is.EqualTo(expected.FreeActiveSlots));
        Assert.That(actual.Timelines, Has.Count.EqualTo(expected.Timelines.Count));

        foreach (Timeline before in expected.Timelines)
        {
            Assert.That(actual.HasTimeline(before.Id), Is.True, $"L{before.Id} is missing");

            Timeline after = actual.GetTimeline(before.Id);

            Assert.That(after.ParentId, Is.EqualTo(before.ParentId), $"L{before.Id} parent");
            Assert.That(after.BranchTime, Is.EqualTo(before.BranchTime), $"L{before.Id} branch time");
            Assert.That(after.FirstStateTime, Is.EqualTo(before.FirstStateTime), $"L{before.Id} first state");
            Assert.That(after.FrontierTime, Is.EqualTo(before.FrontierTime), $"L{before.Id} frontier");
            Assert.That(after.StateCount, Is.EqualTo(before.StateCount), $"L{before.Id} state count");
            Assert.That(after.Status, Is.EqualTo(before.Status), $"L{before.Id} status");
            Assert.That(after.OccupiesActiveSlot, Is.EqualTo(before.OccupiesActiveSlot), $"L{before.Id} slot");

            for (int time = before.FirstStateTime; time <= before.FrontierTime; time++)
            {
                Assert.That(after.StateAt(time), Is.EqualTo(before.StateAt(time)), $"L{before.Id} T{time}");
            }
        }
    }

    // ---- the round trip ----------------------------------------------------

    [Test]
    public void ARunWithABranchAndADeadTimelineSurvivesASaveAndLoad()
    {
        GameState before = BuildRunWithABranchAndADeadTimeline();

        GameState after = GameSaveFormat.Read(GameSaveFormat.Write(before));

        AssertSameRun(before, after);
    }

    [Test]
    public void SavingALoadedRunProducesTheSameTextAgain()
    {
        string first = GameSaveFormat.Write(BuildRunWithABranchAndADeadTimeline());
        string second = GameSaveFormat.Write(GameSaveFormat.Read(first));

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void ALoadedRunCanBePlayedOnAsIfItHadNeverBeenSaved()
    {
        GameState before = BuildRunWithABranchAndADeadTimeline();
        GameState after = GameSaveFormat.Read(GameSaveFormat.Write(before));

        MoveResult direct = before.SelectTimeline(2).PlaceValue(0, 3, 2);
        MoveResult loaded = after.SelectTimeline(2).PlaceValue(0, 3, 2);

        Assert.That(loaded.Succeeded, Is.EqualTo(direct.Succeeded), direct.Rejection.ToString());
        Assert.That(loaded.Rejection, Is.EqualTo(direct.Rejection));
        Assert.That(GameSaveFormat.Write(loaded.State), Is.EqualTo(GameSaveFormat.Write(direct.State)));
    }

    [TestCase(GameOutcome.Won)]
    [TestCase(GameOutcome.GameOver)]
    public void FinishedRunsSurviveTheRoundTripToo(GameOutcome outcome)
    {
        GameState finished = outcome == GameOutcome.Won
            ? Levels.SolveSelectedTimeline(GameState.Start(Levels.SolvableFourByFour()))
            : GameState.Start(Levels.FromText("stuck", BoardSize.FourByFour, Puzzles.Blocked4));

        Assert.That(finished.Outcome, Is.EqualTo(outcome));

        AssertSameRun(finished, GameSaveFormat.Read(GameSaveFormat.Write(finished)));
    }

    [Test]
    public void ANineByNineRunSurvivesTheRoundTrip()
    {
        GameState game = GameState.Start(
            Levels.FromText("9x9", BoardSize.NineByNine, Puzzles.Unique9, temporalBudget: 2, temporalWindow: 4));

        game = game.PlaceValue(0, 2, 4).State;
        game = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 2, 1).State;

        AssertSameRun(game, GameSaveFormat.Read(GameSaveFormat.Write(game)));
    }

    [Test]
    public void ALevelIdWithAwkwardCharactersSurvives()
    {
        LevelDefinition level = new LevelDefinition(
            "world 2 = the hard one (100%)",
            SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Unique4),
            3, 4, 2, 12);

        GameState loaded = GameSaveFormat.Read(GameSaveFormat.Write(GameState.Start(level)));

        Assert.That(loaded.Level.Id, Is.EqualTo("world 2 = the hard one (100%)"));
    }

    // ---- version tolerance -------------------------------------------------

    [Test]
    public void ASaveCarryingKeysThisBuildDoesNotKnowStillLoads()
    {
        string save = GameSaveFormat.Write(BuildRunWithABranchAndADeadTimeline())
            .Replace("game selected=", "game mood=cheerful selected=")
            .Replace("timeline id=", "timeline colour=blue id=");

        AssertSameRun(BuildRunWithABranchAndADeadTimeline(), GameSaveFormat.Read(save));
    }

    [Test]
    public void ASaveCarryingWholeLinesThisBuildDoesNotKnowStillLoads()
    {
        GameState expected = BuildRunWithABranchAndADeadTimeline();
        string save = GameSaveFormat.Write(expected) + "achievements unlocked=3\nreplay steps=17\n";

        AssertSameRun(expected, GameSaveFormat.Read(save));
    }

    [Test]
    public void ASaveFromANewerBuildIsRefusedRatherThanHalfRead()
    {
        string save = GameSaveFormat.Write(BuildRunWithABranchAndADeadTimeline())
            .Replace($"{GameSaveFormat.Magic} {GameSaveFormat.CurrentVersion}", $"{GameSaveFormat.Magic} 99");

        Assert.That(
            () => GameSaveFormat.Read(save),
            Throws.TypeOf<GameSaveException>().With.Message.Contains("version 99"));
    }

    // ---- refusing what cannot be trusted -----------------------------------

    [Test]
    public void TextThatIsNotASaveIsRefused()
    {
        Assert.That(() => GameSaveFormat.Read(""), Throws.TypeOf<GameSaveException>());
        Assert.That(() => GameSaveFormat.Read("hello"), Throws.TypeOf<GameSaveException>());
        Assert.That(() => GameSaveFormat.Read("5dsudoku"), Throws.TypeOf<GameSaveException>());
        Assert.That(() => GameSaveFormat.Read("5dsudoku x\n"), Throws.TypeOf<GameSaveException>());
    }

    [Test]
    public void ASaveWithNoLevelOrNoTimelinesIsRefused()
    {
        Assert.That(
            () => GameSaveFormat.Read($"{GameSaveFormat.Magic} 1\n"),
            Throws.TypeOf<GameSaveException>().With.Message.Contains("no level"));

        string levelOnly = GameSaveFormat.Write(GameState.Start(Levels.SolvableFourByFour()))
            .Split('\n')
            .Where(line => !line.StartsWith("timeline", StringComparison.Ordinal)
                && !line.StartsWith("state", StringComparison.Ordinal))
            .Aggregate(string.Empty, (text, line) => text + line + "\n");

        Assert.That(
            () => GameSaveFormat.Read(levelOnly),
            Throws.TypeOf<GameSaveException>().With.Message.Contains("no timelines"));
    }

    [Test]
    public void ASaveWhoseRecordedPresentDisagreesWithItsTimelinesIsRefused()
    {
        // The present is derived, so a save that claims a different one is either
        // corrupt or was written by rules this build no longer plays by.
        string save = GameSaveFormat.Write(BuildRunWithABranchAndADeadTimeline())
            .Replace("present=2", "present=1");

        Assert.That(
            () => GameSaveFormat.Read(save),
            Throws.TypeOf<GameSaveException>().With.Message.Contains("present"));
    }

    [Test]
    public void ASaveWhoseRecordedOutcomeDisagreesWithItsTimelinesIsRefused()
    {
        string save = GameSaveFormat.Write(BuildRunWithABranchAndADeadTimeline())
            .Replace("outcome=InProgress", "outcome=Won");

        Assert.That(
            () => GameSaveFormat.Read(save),
            Throws.TypeOf<GameSaveException>().With.Message.Contains("outcome"));
    }

    [Test]
    public void ASaveWithAnUnknownStatusIsRefused()
    {
        string save = GameSaveFormat.Write(BuildRunWithABranchAndADeadTimeline())
            .Replace("status=Dead", "status=Haunted");

        Assert.That(
            () => GameSaveFormat.Read(save),
            Throws.TypeOf<GameSaveException>().With.Message.Contains("Haunted"));
    }

    [Test]
    public void SavingOrLoadingNothingIsRejected()
    {
        Assert.That(() => GameSaveFormat.Write(null!), Throws.ArgumentNullException);
        Assert.That(() => GameSaveFormat.Read(null!), Throws.ArgumentNullException);
    }

    // ---- what the save actually carries ------------------------------------

    [Test]
    public void TheSaveCarriesEverythingTheSpecificationAsksFor()
    {
        string save = GameSaveFormat.Write(BuildRunWithABranchAndADeadTimeline());

        Assert.That(save, Does.StartWith($"{GameSaveFormat.Magic} {GameSaveFormat.CurrentVersion}\n"));

        foreach (string key in new[]
        {
            "id=", "boxWidth=", "boxHeight=", "budget=", "window=", "slots=", "finalDepth=", "start=",
            "selected=", "budgetLeft=", "present=", "outcome=",
            "parent=", "branch=", "first=", "status=", "slot=", "board=",
        })
        {
            Assert.That(save, Does.Contain(key), $"the save should record {key}");
        }
    }
}
