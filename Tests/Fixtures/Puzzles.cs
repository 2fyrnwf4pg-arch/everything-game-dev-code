namespace FiveDSudoku.Tests;

/// <summary>
/// Hand-checked puzzle fixtures. Every solution count claimed here was verified
/// against the solver before being written down, and the tests assert those
/// counts, so a regression in the solver shows up as a failing assertion rather
/// than as a quietly changed expectation.
///
/// Boards are written as readable grids; <see cref="Core.Sudoku.SudokuBoard.Parse"/>
/// ignores whitespace and separator characters.
/// </summary>
internal static class Puzzles
{
    // ---- 4x4 (2x2 boxes) -------------------------------------------------

    /// <summary>A complete, valid 4x4 solution.</summary>
    internal const string Solved4 = """
        12|34
        34|12
        --+--
        21|43
        43|21
        """;

    /// <summary>4x4 puzzle with exactly one solution.</summary>
    internal const string Unique4 = """
        1.|..
        ..|1.
        --+--
        .2|..
        ..|.3
        """;

    /// <summary>
    /// 4x4 puzzle that breaks no constraint yet cannot be completed: the top-left
    /// cell is excluded from 1 and 4 by its box, from 2 by its row, and from 3 by
    /// its column, so it has no candidate left.
    /// </summary>
    internal const string Zero4 = """
        .1|2.
        .4|..
        --+--
        3.|..
        ..|..
        """;

    /// <summary>
    /// 4x4 puzzle with exactly two solutions: the four blank cells form a
    /// rectangle across two boxes whose 1/2 pair can be swapped.
    /// </summary>
    internal const string Multi4 = """
        ..|34
        34|12
        --+--
        ..|43
        43|21
        """;

    /// <summary>
    /// 4x4 board with two 1s in the top row. They sit in different boxes and
    /// different columns, so only the row constraint rejects this board.
    /// </summary>
    internal const string Invalid4 = """
        1.|1.
        ..|..
        --+--
        ..|..
        ..|..
        """;

    // ---- 9x9 (3x3 boxes) -------------------------------------------------

    /// <summary>9x9 puzzle with exactly one solution.</summary>
    internal const string Unique9 = """
        53.|.7.|...
        6..|195|...
        .98|...|.6.
        ---+---+---
        8..|.6.|..3
        4..|8.3|..1
        7..|.2.|..6
        ---+---+---
        .6.|...|28.
        ...|419|..5
        ...|.8.|.79
        """;

    /// <summary>The one solution of <see cref="Unique9"/>.</summary>
    internal const string Unique9Solution = """
        534|678|912
        672|195|348
        198|342|567
        ---+---+---
        859|761|423
        426|853|791
        713|924|856
        ---+---+---
        961|537|284
        287|419|635
        345|286|179
        """;

    /// <summary>
    /// <see cref="Unique9"/> with the value 1 written into r0c2. The placement is
    /// locally legal — no row, column, or box conflict — but it leaves the grid
    /// with zero completions.
    /// </summary>
    internal const string Zero9 = """
        531|.7.|...
        6..|195|...
        .98|...|.6.
        ---+---+---
        8..|.6.|..3
        4..|8.3|..1
        7..|.2.|..6
        ---+---+---
        .6.|...|28.
        ...|419|..5
        ...|.8.|.79
        """;

    /// <summary>
    /// <see cref="Unique9"/> with its first two givens removed, leaving exactly
    /// two solutions.
    /// </summary>
    internal const string Multi9 = """
        ...|.7.|...
        6..|195|...
        .98|...|.6.
        ---+---+---
        8..|.6.|..3
        4..|8.3|..1
        7..|.2.|..6
        ---+---+---
        .6.|...|28.
        ...|419|..5
        ...|.8.|.79
        """;

    /// <summary>
    /// 9x9 board with two 5s in the leftmost column. They sit in different boxes
    /// and different rows, so only the column constraint rejects this board.
    /// </summary>
    internal const string Invalid9 = """
        5..|...|...
        ...|...|...
        ...|...|...
        ---+---+---
        5..|...|...
        ...|...|...
        ...|...|...
        ---+---+---
        ...|...|...
        ...|...|...
        ...|...|...
        """;

    // ---- 6x6 (3x2 boxes) -------------------------------------------------

    /// <summary>
    /// A complete, valid 6x6 solution. Present to prove the engine is driven by
    /// <see cref="Core.Sudoku.BoardSize"/> rather than by a hard-coded square box
    /// shape — 6x6 uses boxes that are 3 wide and 2 tall.
    /// </summary>
    internal const string Solved6 = """
        123|456
        456|123
        ---+---
        234|561
        561|234
        ---+---
        345|612
        612|345
        """;
}
