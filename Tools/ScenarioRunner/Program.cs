using System;
using System.IO;
using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Persistence;

namespace FiveDSudoku.Tools.ScenarioRunner;

/// <summary>
/// Text-based developer diagnostics: shows a run without any of the game around
/// it, and moves one in and out of a save file.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (GameSaveException exception)
        {
            Console.Error.WriteLine($"Could not read that save: {exception.Message}");
            return 1;
        }
        catch (IOException exception)
        {
            Console.Error.WriteLine($"File trouble: {exception.Message}");
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        if (args.Length > 0 && (args[0] == "--help" || args[0] == "-h"))
        {
            PrintUsage();
            return 0;
        }

        if (args.Length == 2 && args[0] == "--load")
        {
            Show(GameSaveFormat.Read(File.ReadAllText(args[1])));
            return 0;
        }

        GameState game = DemoScenario.Build();

        if (args.Length == 2 && args[0] == "--save")
        {
            File.WriteAllText(args[1], GameSaveFormat.Write(game));
            Console.WriteLine($"Wrote the demo scenario to {args[1]}.");
            return 0;
        }

        if (args.Length != 0)
        {
            PrintUsage();
            return 1;
        }

        Show(game);
        return 0;
    }

    private static void Show(GameState game)
    {
        Console.Write(StateSummary.Render(game));
        Console.WriteLine();
        Console.Write(StateSummary.RenderTimelineTree(game));
    }

    private static void PrintUsage()
    {
        Console.WriteLine("ScenarioRunner — text diagnostics for a 5D Sudoku run.");
        Console.WriteLine();
        Console.WriteLine("  ScenarioRunner                 print the demo scenario");
        Console.WriteLine("  ScenarioRunner --save <path>   write the demo scenario to a save file");
        Console.WriteLine("  ScenarioRunner --load <path>   read a save file and print it");
    }
}
