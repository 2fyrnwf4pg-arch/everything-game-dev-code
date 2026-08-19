using System;
using FiveDSudoku.Core.Solver;
using FiveDSudoku.Core.Sudoku;

namespace FiveDSudoku.Tests;

/// <summary>
/// Solver correctness on both required board sizes, including the zero-,
/// one-, and multi-solution fixtures later phases depend on to classify boards.
/// </summary>
[TestFixture]
public sealed class SudokuSolverTests
{
    private const int GenerousLimit = 10;

    // ---- validity and legality pass through to the board rules -----------

    [Test]
    public void ValidityMatchesTheBoardRules()
    {
        SudokuBoard valid = SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Unique4);
        SudokuBoard invalid = SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Invalid4);

        Assert.That(SudokuSolver.IsValid(valid), Is.True);
        Assert.That(SudokuSolver.IsValid(invalid), Is.False);
    }

    [Test]
    public void PlacementLegalityMatchesTheBoardRules()
    {
        SudokuBoard board = SudokuBoard.Parse(BoardSize.NineByNine, Puzzles.Unique9);

        Assert.That(SudokuSolver.IsPlacementLegal(board, 0, 2, 4), Is.True);
        Assert.That(SudokuSolver.IsPlacementLegal(board, 0, 2, 5), Is.False);
    }

    [Test]
    public void SolvedRecognisesACompleteValidBoard()
    {
        Assert.That(
            SudokuSolver.IsSolved(SudokuBoard.Parse(BoardSize.NineByNine, Puzzles.Unique9Solution)),
            Is.True);
        Assert.That(
            SudokuSolver.IsSolved(SudokuBoard.Parse(BoardSize.NineByNine, Puzzles.Unique9)),
            Is.False);
    }

    // ---- 4x4 solution counts --------------------------------------------

    [Test]
    public void FourByFourZeroSolutionBoardIsValidButUnsolvable()
    {
        SudokuBoard board = SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Zero4);

        Assert.That(board.IsValid(), Is.True, "the fixture must break no constraint");
        Assert.That(SudokuSolver.CountSolutions(board, GenerousLimit), Is.EqualTo(0));
        Assert.That(SudokuSolver.HasSolution(board), Is.False);
        Assert.That(SudokuSolver.FindFirstSolution(board), Is.Null);
    }

    [Test]
    public void FourByFourOneSolutionBoardHasExactlyOneSolution()
    {
        SudokuBoard board = SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Unique4);

        Assert.That(SudokuSolver.CountSolutions(board, GenerousLimit), Is.EqualTo(1));
        Assert.That(SudokuSolver.HasSolution(board), Is.True);
        Assert.That(SudokuSolver.HasUniqueSolution(board), Is.True);
    }

    [Test]
    public void FourByFourMultiSolutionBoardHasExactlyTwoSolutions()
    {
        SudokuBoard board = SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Multi4);

        Assert.That(SudokuSolver.CountSolutions(board, GenerousLimit), Is.EqualTo(2));
        Assert.That(SudokuSolver.HasSolution(board), Is.True);
        Assert.That(SudokuSolver.HasUniqueSolution(board), Is.False);
    }

    [Test]
    public void FourByFourInvalidBoardHasNoSolutions()
    {
        SudokuBoard board = SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Invalid4);

        Assert.That(SudokuSolver.CountSolutions(board, GenerousLimit), Is.EqualTo(0));
    }

    [Test]
    public void FourByFourSolvedBoardCountsAsItsOwnSingleSolution()
    {
        SudokuBoard board = SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Solved4);

        Assert.That(SudokuSolver.CountSolutions(board, GenerousLimit), Is.EqualTo(1));
        Assert.That(SudokuSolver.FindFirstSolution(board), Is.EqualTo(board));
    }

    // ---- 9x9 solution counts --------------------------------------------

    [Test]
    public void NineByNineZeroSolutionBoardIsValidButUnsolvable()
    {
        SudokuBoard board = SudokuBoard.Parse(BoardSize.NineByNine, Puzzles.Zero9);

        Assert.That(board.IsValid(), Is.True, "the fixture must break no constraint");
        Assert.That(SudokuSolver.CountSolutions(board, GenerousLimit), Is.EqualTo(0));
        Assert.That(SudokuSolver.FindFirstSolution(board), Is.Null);
    }

    [Test]
    public void NineByNineOneSolutionBoardHasExactlyOneSolution()
    {
        SudokuBoard board = SudokuBoard.Parse(BoardSize.NineByNine, Puzzles.Unique9);

        Assert.That(SudokuSolver.CountSolutions(board, GenerousLimit), Is.EqualTo(1));
        Assert.That(SudokuSolver.HasUniqueSolution(board), Is.True);
    }

    [Test]
    public void NineByNineMultiSolutionBoardHasExactlyTwoSolutions()
    {
        SudokuBoard board = SudokuBoard.Parse(BoardSize.NineByNine, Puzzles.Multi9);

        Assert.That(SudokuSolver.CountSolutions(board, GenerousLimit), Is.EqualTo(2));
        Assert.That(SudokuSolver.HasUniqueSolution(board), Is.False);
    }

    [Test]
    public void NineByNineInvalidBoardHasNoSolutions()
    {
        SudokuBoard board = SudokuBoard.Parse(BoardSize.NineByNine, Puzzles.Invalid9);

        Assert.That(SudokuSolver.CountSolutions(board, GenerousLimit), Is.EqualTo(0));
    }

    // ---- the produced solutions are actually solutions -------------------

    [Test]
    public void FirstSolutionOfTheFourByFourPuzzleIsTheKnownSolution()
    {
        SudokuBoard puzzle = SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Unique4);
        SudokuBoard expected = SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Unique4Solution);

        Assert.That(expected.IsSolved(), Is.True);
        Assert.That(SudokuSolver.FindFirstSolution(puzzle), Is.EqualTo(expected));
    }

    [Test]
    public void FirstSolutionOfTheNineByNinePuzzleIsTheKnownSolution()
    {
        SudokuBoard puzzle = SudokuBoard.Parse(BoardSize.NineByNine, Puzzles.Unique9);
        SudokuBoard expected = SudokuBoard.Parse(BoardSize.NineByNine, Puzzles.Unique9Solution);

        Assert.That(SudokuSolver.FindFirstSolution(puzzle), Is.EqualTo(expected));
    }

    [Test]
    public void FirstSolutionKeepsEveryGivenAndSolvesTheBoard()
    {
        BoardSize size = BoardSize.NineByNine;
        SudokuBoard puzzle = SudokuBoard.Parse(size, Puzzles.Unique9);

        SudokuBoard? solution = SudokuSolver.FindFirstSolution(puzzle);

        Assert.That(solution, Is.Not.Null);
        Assert.That(solution!.IsSolved(), Is.True);

        for (int row = 0; row < size.Side; row++)
        {
            for (int column = 0; column < size.Side; column++)
            {
                if (!puzzle.IsEmpty(row, column))
                {
                    Assert.That(
                        solution[row, column],
                        Is.EqualTo(puzzle[row, column]),
                        $"given at r{row}c{column} must be preserved");
                }
            }
        }
    }

    [Test]
    public void SolvingLeavesTheInputBoardUntouched()
    {
        SudokuBoard puzzle = SudokuBoard.Parse(BoardSize.NineByNine, Puzzles.Unique9);
        SudokuBoard before = SudokuBoard.Parse(BoardSize.NineByNine, Puzzles.Unique9);

        SudokuSolver.FindFirstSolution(puzzle);
        SudokuSolver.CountSolutions(puzzle, GenerousLimit);

        Assert.That(puzzle, Is.EqualTo(before));
    }

    // ---- generic across board sizes -------------------------------------

    [Test]
    public void SolverWorksOnANonSquareBoxShape()
    {
        BoardSize size = BoardSize.FromBoxShape(boxWidth: 3, boxHeight: 2);
        SudokuBoard solved = SudokuBoard.Parse(size, Puzzles.Solved6);

        Assert.That(solved.IsSolved(), Is.True);
        Assert.That(SudokuSolver.CountSolutions(solved, GenerousLimit), Is.EqualTo(1));

        SudokuBoard withHoles = solved
            .WithValue(0, 0, BoardSize.EmptyCell)
            .WithValue(2, 4, BoardSize.EmptyCell);

        Assert.That(SudokuSolver.FindFirstSolution(withHoles), Is.EqualTo(solved));
    }

    // ---- bounded counting -----------------------------------------------

    [Test]
    public void CountingStopsAtTheRequestedLimit()
    {
        SudokuBoard empty = SudokuBoard.Empty(BoardSize.FourByFour);

        Assert.That(SudokuSolver.CountSolutions(empty, 1), Is.EqualTo(1));
        Assert.That(SudokuSolver.CountSolutions(empty, 5), Is.EqualTo(5));
        Assert.That(SudokuSolver.CountSolutions(empty, GenerousLimit), Is.EqualTo(GenerousLimit));
    }

    [Test]
    public void CountingAnEmptyNineByNineGridStaysBounded()
    {
        SudokuBoard empty = SudokuBoard.Empty(BoardSize.NineByNine);

        Assert.That(SudokuSolver.CountSolutions(empty, 2), Is.EqualTo(2));
        Assert.That(SudokuSolver.HasUniqueSolution(empty), Is.False);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void CountingRejectsANonPositiveLimit(int limit)
    {
        SudokuBoard board = SudokuBoard.Empty(BoardSize.FourByFour);

        Assert.That(
            () => SudokuSolver.CountSolutions(board, limit),
            Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void SolverRejectsANullBoard()
    {
        Assert.That(() => SudokuSolver.IsValid(null!), Throws.ArgumentNullException);
        Assert.That(() => SudokuSolver.CountSolutions(null!, 1), Throws.ArgumentNullException);
        Assert.That(() => SudokuSolver.FindFirstSolution(null!), Throws.ArgumentNullException);
    }

    // ---- determinism -----------------------------------------------------

    [Test]
    public void RepeatedSolvesProduceIdenticalResults()
    {
        SudokuBoard board = SudokuBoard.Parse(BoardSize.NineByNine, Puzzles.Multi9);

        SudokuBoard? first = SudokuSolver.FindFirstSolution(board);
        SudokuBoard? second = SudokuSolver.FindFirstSolution(board);

        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.EqualTo(first));
        Assert.That(
            SudokuSolver.CountSolutions(board, GenerousLimit),
            Is.EqualTo(SudokuSolver.CountSolutions(board, GenerousLimit)));
    }
}
