using System;
using FiveDSudoku.Core.Level;
using FiveDSudoku.Core.Sudoku;

namespace FiveDSudoku.Tests;

[TestFixture]
public sealed class LevelDefinitionTests
{
    [Test]
    public void LevelCarriesItsTuningNumbers()
    {
        LevelDefinition level = Levels.SolvableFourByFour(temporalBudget: 5, maxActiveTimelines: 3, finalDepth: 20);

        Assert.That(level.Id, Is.EqualTo("4x4-unique"));
        Assert.That(level.TemporalBudget, Is.EqualTo(5));
        Assert.That(level.MaxActiveTimelines, Is.EqualTo(3));
        Assert.That(level.FinalDepth, Is.EqualTo(20));
        Assert.That(level.Size, Is.EqualTo(BoardSize.FourByFour));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void LevelNeedsANonEmptyId(string id)
    {
        Assert.That(
            () => new LevelDefinition(id, SudokuBoard.Empty(BoardSize.FourByFour), 1, 1, 1),
            Throws.ArgumentException);
    }

    [Test]
    public void LevelNeedsAStartingBoard()
    {
        Assert.That(
            () => new LevelDefinition("id", null!, 1, 1, 1),
            Throws.ArgumentNullException);
    }

    [Test]
    public void TemporalBudgetCannotBeNegative()
    {
        Assert.That(
            () => new LevelDefinition("id", SudokuBoard.Empty(BoardSize.FourByFour), -1, 1, 1),
            Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void ZeroTemporalBudgetIsAllowed()
    {
        Assert.That(
            () => new LevelDefinition("id", SudokuBoard.Empty(BoardSize.FourByFour), 0, 1, 1),
            Throws.Nothing);
    }

    [Test]
    public void AtLeastOneActiveSlotIsNeededForTheRootTimeline()
    {
        Assert.That(
            () => new LevelDefinition("id", SudokuBoard.Empty(BoardSize.FourByFour), 1, 0, 1),
            Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void FinalDepthCannotBeNegative()
    {
        Assert.That(
            () => new LevelDefinition("id", SudokuBoard.Empty(BoardSize.FourByFour), 1, 1, -1),
            Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void LevelDefinitionDoesNotJudgePuzzleQuality()
    {
        // Whether a puzzle is fit for release — solvable at all, uniquely solvable —
        // is not decided here. A definition must be able to carry a broken puzzle so
        // that a validation layer has something to reject.
        Assert.That(
            () => Levels.FromText("zero-solutions", BoardSize.FourByFour, Puzzles.Zero4),
            Throws.Nothing);
        Assert.That(
            () => Levels.FromText("invalid", BoardSize.FourByFour, Puzzles.Invalid4),
            Throws.Nothing);
    }
}
