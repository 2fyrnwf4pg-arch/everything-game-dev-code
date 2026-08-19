using System;

namespace FiveDSudoku.Tests;

/// <summary>
/// Where the randomness in the stress tests comes from.
///
/// One fixed seed by default, so a failure is reproducible and a green run means
/// the same thing today as tomorrow. Set the environment variable
/// <c>FIVED_SUDOKU_SEED</c> to sweep other seeds — in CI, on a machine left
/// running overnight, or to re-run the exact seed a failure was reported under.
///
/// Each test derives its own generator from the run seed and its own name, so
/// tests never share a stream and can be run in any order, alone or together,
/// and still see the same numbers.
/// </summary>
internal static class StressSeeds
{
    /// <summary>The seed used unless the environment says otherwise.</summary>
    internal const int DefaultSeed = 20260819;

    /// <summary>Environment variable that overrides <see cref="DefaultSeed"/>.</summary>
    internal const string SeedVariable = "FIVED_SUDOKU_SEED";

    private static readonly int RunSeed = ReadSeed();

    /// <summary>The seed this run is using.</summary>
    internal static int Seed => RunSeed;

    /// <summary>A generator for one named test, derived from the run seed.</summary>
    internal static Random For(string testName)
    {
        if (testName is null)
        {
            throw new ArgumentNullException(nameof(testName));
        }

        return new Random(Derive(RunSeed, testName));
    }

    private static int ReadSeed()
    {
        string? configured = Environment.GetEnvironmentVariable(SeedVariable);

        return int.TryParse(configured, out int parsed) ? parsed : DefaultSeed;
    }

    /// <summary>
    /// FNV-1a rather than <see cref="string.GetHashCode()"/>: string hashing is
    /// randomised per process on modern .NET, which would make "the same seed"
    /// mean something different on every run.
    /// </summary>
    private static int Derive(int seed, string testName)
    {
        unchecked
        {
            uint hash = 2166136261u ^ (uint)seed;

            foreach (char character in testName)
            {
                hash ^= character;
                hash *= 16777619u;
            }

            return (int)(hash & 0x7FFFFFFF);
        }
    }
}
