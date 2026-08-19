using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Persistence;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;
using FiveDSudoku.Tools.ScenarioRunner;

namespace FiveDSudoku.Tests;

/// <summary>
/// The diagnostics output, pinned down character for character.
///
/// A snapshot test earns its keep here: the point of this view is that a person
/// can read a whole run out of it at a glance, and a change to the layout that
/// nobody meant to make is exactly the kind of thing that slips through
/// otherwise.
/// </summary>
[TestFixture]
public sealed class StateSummaryTests
{
    [Test]
    public void TheDemoScenarioIsTheOneTheDiagnosticsWereWrittenAgainst()
    {
        GameState game = DemoScenario.Build();

        Assert.That(game.Timelines, Has.Count.EqualTo(3));
        Assert.That(game.GetTimeline(0).Status, Is.EqualTo(TimelineStatus.Active));
        Assert.That(game.GetTimeline(1).Status, Is.EqualTo(TimelineStatus.Dead));
        Assert.That(game.GetTimeline(2).Status, Is.EqualTo(TimelineStatus.Active));
        Assert.That(game.Present, Is.EqualTo(2));
        Assert.That(game.RemainingTemporalBudget, Is.EqualTo(1));

        // The living branch has to be a real alternative. A branch that replays the
        // move its parent went on to make is legal, but it would illustrate nothing.
        Assert.That(
            game.GetTimeline(2).Frontier,
            Is.Not.EqualTo(game.GetTimeline(0).Frontier),
            "the branch must diverge from the root, not shadow it");
        Assert.That(game.GetTimeline(2).StateAt(1), Is.EqualTo(game.GetTimeline(0).StateAt(1)));
    }

    [Test]
    public void TheStateSummaryRendersExactlyAsExpected()
    {
        const string expected =
            "PRESENT T2\n" +
            "OUTCOME InProgress\n" +
            "\n" +
            "L0  ACTIVE   frontier=T2\n" +
            "L1  DEAD     frontier=T1\n" +
            "L2  ACTIVE   frontier=T2\n" +
            "\n" +
            "Budget: 2/3 used\n" +
            "\n" +
            "L0 board:\n" +
            "1 4 | 3 .\n" +
            ". . | 1 .\n" +
            "----+----\n" +
            ". 2 | . .\n" +
            ". . | . 3\n";

        Assert.That(StateSummary.Render(DemoScenario.Build()), Is.EqualTo(expected));
    }

    [Test]
    public void TheTimelineTreeRendersExactlyAsExpected()
    {
        const string expected =
            "L0\n" +
            "├── L1 (DEAD)\n" +
            "└── L2\n";

        Assert.That(StateSummary.RenderTimelineTree(DemoScenario.Build()), Is.EqualTo(expected));
    }

    [Test]
    public void TheTreeNestsABranchOfABranchUnderIt()
    {
        GameState game = DemoScenario.Build();

        // Branch off the still-playable L2, so the tree has a level below the root.
        TemporalMoveResult nested = game.PerformTemporalMove(2, 1, 0, 3, 2);

        Assert.That(nested.Succeeded, Is.True, nested.Rejection.ToString());

        const string expected =
            "L0\n" +
            "├── L1 (DEAD)\n" +
            "└── L2\n" +
            "    └── L3\n";

        Assert.That(StateSummary.RenderTimelineTree(nested.State), Is.EqualTo(expected));
    }

    [Test]
    public void TheSummaryShowsWhicheverTimelineIsSelected()
    {
        GameState game = DemoScenario.Build().SelectTimeline(1);

        Assert.That(StateSummary.Render(game), Does.Contain("L1 board:"));
        Assert.That(StateSummary.Render(game), Does.Contain("1 3 | . ."));
    }

    [Test]
    public void TheSummarySaysSoWhenThereIsNoPresentLeft()
    {
        GameState won = Levels.SolveSelectedTimeline(GameState.Start(Levels.SolvableFourByFour()));

        Assert.That(StateSummary.Render(won), Does.StartWith("PRESENT none\nOUTCOME Won\n"));
    }

    [Test]
    public void TheDiagnosticsWorkOnALoadedRunJustAsWellAsOnALivingOne()
    {
        GameState game = DemoScenario.Build();
        GameState loaded = GameSaveFormat.Read(GameSaveFormat.Write(game));

        Assert.That(StateSummary.Render(loaded), Is.EqualTo(StateSummary.Render(game)));
        Assert.That(StateSummary.RenderTimelineTree(loaded), Is.EqualTo(StateSummary.RenderTimelineTree(game)));
    }

    [Test]
    public void TheSummaryScalesToANineByNineGrid()
    {
        GameState game = GameState.Start(
            Levels.FromText("9x9", BoardSize.NineByNine, Puzzles.Unique9, temporalBudget: 2, temporalWindow: 4));

        string rendered = StateSummary.Render(game);

        Assert.That(rendered, Does.StartWith("PRESENT T0\n"));
        Assert.That(rendered, Does.Contain("5 3 . | . 7 . | . . ."));
        Assert.That(StateSummary.RenderTimelineTree(game), Is.EqualTo("L0\n"));
    }
}
