using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Level;
using FiveDSudoku.Core.Solver;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;
using FiveDSudoku.Core.Validation;

namespace FiveDSudoku.Tests;

/// <summary>
/// The whole engine at 9x9, on a known unique-solution puzzle.
///
/// A 4x4 prototype proves nothing about 9x9: the same code has to be shown doing
/// the same things on the bigger grid — ordinary solving, a legal branch into
/// history, a contradiction branch dying on arrival, the present moving, and
/// history staying put.
/// </summary>
[TestFixture]
public sealed class NineByNineScenarioTests
{
    private static LevelDefinition NineByNineLevel() =>
        Levels.FromText(
            "9x9-unique",
            BoardSize.NineByNine,
            Puzzles.Unique9,
            temporalBudget: 3,
            temporalWindow: 4,
            maxActiveTimelines: 2,
            finalDepth: 51);

    [Test]
    public void AKnownUniqueSolutionNineByNinePuzzleIsSolvedByOrdinaryPlay()
    {
        LevelDefinition level = NineByNineLevel();
        SudokuBoard expected = SudokuBoard.Parse(BoardSize.NineByNine, Puzzles.Unique9Solution);

        Assert.That(SudokuSolver.HasUniqueSolution(level.StartingBoard), Is.True);

        GameState game = Levels.SolveSelectedTimeline(GameState.Start(level));

        Assert.That(game.Outcome, Is.EqualTo(GameOutcome.Won));
        Assert.That(game.SelectedTimeline.Frontier, Is.EqualTo(expected));
        Assert.That(game.SelectedTimeline.FrontierTime, Is.EqualTo(51), "51 empty cells, 51 placements");
        Assert.That(game.RemainingTemporalBudget, Is.EqualTo(level.TemporalBudget));
    }

    [Test]
    public void AKnownUniqueSolutionNineByNineLevelIsCertifiedByTheValidator()
    {
        LevelValidationReport report = LevelValidator.Validate(NineByNineLevel());

        Assert.That(report.IsReleaseCandidate, Is.True, report.ToString());
    }

    [Test]
    public void ABoundedNineByNineSessionCoversBranchingDeathPresentAndHistory()
    {
        LevelDefinition level = NineByNineLevel();
        SudokuBoard start = level.StartingBoard;
        SudokuBoard solution = SudokuBoard.Parse(BoardSize.NineByNine, Puzzles.Unique9Solution);

        // --- ordinary play ------------------------------------------------
        GameState game = GameState.Start(level);

        game = game.PlaceValue(0, 2, solution[0, 2]).State;
        game = game.PlaceValue(0, 3, solution[0, 3]).State;

        Assert.That(game.Present, Is.EqualTo(2));
        Assert.That(game.SelectedTimeline.Status, Is.EqualTo(TimelineStatus.Active));

        SudokuBoard rootT0 = game.SelectedTimeline.StateAt(0);
        SudokuBoard rootT1 = game.SelectedTimeline.StateAt(1);
        SudokuBoard rootT2 = game.SelectedTimeline.StateAt(2);

        // --- a contradiction branch, dead on arrival ----------------------
        // r0c2 admits 1, 2 and 4 by the local rules; only 4 completes the grid.
        Assert.That(start.IsPlacementLegal(0, 2, 1), Is.True);
        Assert.That(solution[0, 2], Is.EqualTo(4));

        TemporalMoveResult trap = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 2, 1);

        Assert.That(trap.Succeeded, Is.True, trap.Rejection.ToString());

        GameState afterTrap = trap.State;
        Timeline dead = afterTrap.GetTimeline(trap.NewTimelineId!.Value);

        Assert.That(dead.Frontier.IsValid(), Is.True, "the branch breaks no Sudoku rule");
        Assert.That(dead.Status, Is.EqualTo(TimelineStatus.Dead));
        Assert.That(dead.OccupiesActiveSlot, Is.False);
        Assert.That(afterTrap.Present, Is.EqualTo(2), "a dead branch does not move the present");
        Assert.That(afterTrap.Outcome, Is.EqualTo(GameOutcome.InProgress));
        Assert.That(afterTrap.RemainingTemporalBudget, Is.EqualTo(2));

        // --- a legal historical branch that stays winnable ----------------
        TemporalMoveResult detour = afterTrap.PerformTemporalMove(
            GameState.RootTimelineId, 0, 0, 3, solution[0, 3]);

        Assert.That(detour.Succeeded, Is.True, detour.Rejection.ToString());

        GameState afterDetour = detour.State;
        Timeline branch = afterDetour.GetTimeline(detour.NewTimelineId!.Value);

        Assert.That(branch.Status, Is.EqualTo(TimelineStatus.Active));
        Assert.That(branch.ParentId, Is.EqualTo(GameState.RootTimelineId));
        Assert.That(branch.BranchTime, Is.EqualTo(0));
        Assert.That(branch.StateAt(0), Is.EqualTo(start), "the source state came over unchanged");

        // --- the present moves back, and the root has to wait -------------
        Assert.That(afterDetour.Present, Is.EqualTo(1));
        Assert.That(
            afterDetour.PlaceValue(0, 5, solution[0, 5]).Rejection,
            Is.EqualTo(MoveRejection.TimelineNotAtPresent));

        // --- history is exactly where it was ------------------------------
        Timeline root = afterDetour.GetTimeline(GameState.RootTimelineId);

        Assert.That(root.StateCount, Is.EqualTo(3));
        Assert.That(root.StateAt(0), Is.EqualTo(rootT0));
        Assert.That(root.StateAt(1), Is.EqualTo(rootT1));
        Assert.That(root.StateAt(2), Is.EqualTo(rootT2));
        Assert.That(GameInvariants.Check(afterDetour), Is.Empty);

        // --- set the detour aside and finish on the original --------------
        GameState parked = afterDetour.DeactivateTimeline(branch.Id).State;

        Assert.That(parked.Present, Is.EqualTo(2));

        GameState won = Levels.SolveSelectedTimeline(parked.SelectTimeline(GameState.RootTimelineId));

        Assert.That(won.Outcome, Is.EqualTo(GameOutcome.Won));
        Assert.That(won.GetTimeline(GameState.RootTimelineId).Frontier, Is.EqualTo(solution));
        Assert.That(won.GetTimeline(dead.Id).Status, Is.EqualTo(TimelineStatus.Dead));
        Assert.That(won.GetTimeline(branch.Id).Status, Is.EqualTo(TimelineStatus.Inactive));
        Assert.That(won.RemainingTemporalBudget, Is.EqualTo(1), "two branches, two units");
    }
}
