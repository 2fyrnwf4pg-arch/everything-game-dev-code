using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Level;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;

namespace FiveDSudoku.Core.Persistence;

/// <summary>
/// Writes a run to text and reads it back.
///
/// The format is line-based and self-describing: a kind word, then
/// <c>key=value</c> pairs. That is what makes it version-tolerant in the way that
/// actually matters for a game that will keep growing — a build that meets a key
/// it does not know, or a whole line kind it does not know, skips it and carries
/// on, so a save written by a later build still loads as far as it can be
/// understood. A save written by a build newer than this one is refused outright
/// rather than half-read.
///
/// The text is the save model. There is no parallel set of DTO classes because
/// there would be nothing for them to do: the format is already independent of
/// the runtime types, and one representation is one thing to keep correct.
///
/// The present and the outcome are written down but not trusted on the way back
/// in. They are recomputed from the timelines and compared, so a save that
/// disagrees with the rules is reported instead of loaded.
///
/// Move history is deliberately not stored. Every state of every timeline is,
/// which is strictly more than a replay would reconstruct.
/// </summary>
public static class GameSaveFormat
{
    /// <summary>Magic word every save starts with.</summary>
    public const string Magic = "5dsudoku";

    /// <summary>The format version this build writes.</summary>
    public const int CurrentVersion = 1;

    private const string LevelLine = "level";
    private const string GameLine = "game";
    private const string TimelineLine = "timeline";
    private const string StateLine = "state";

    /// <summary>Writes a run as text.</summary>
    public static string Write(GameState state)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        LevelDefinition level = state.Level;
        StringBuilder builder = new StringBuilder();

        builder.Append(Magic).Append(' ').Append(CurrentVersion).Append('\n');

        builder.Append(LevelLine)
            .Append(" id=").Append(Encode(level.Id))
            .Append(" boxWidth=").Append(level.Size.BoxWidth)
            .Append(" boxHeight=").Append(level.Size.BoxHeight)
            .Append(" budget=").Append(level.TemporalBudget)
            .Append(" window=").Append(level.TemporalWindow)
            .Append(" slots=").Append(level.MaxActiveTimelines)
            .Append(" finalDepth=").Append(level.FinalDepth)
            .Append(" start=").Append(level.StartingBoard.ToCompactString())
            .Append('\n');

        builder.Append(GameLine)
            .Append(" selected=").Append(state.SelectedTimelineId)
            .Append(" budgetLeft=").Append(state.RemainingTemporalBudget)
            .Append(" present=").Append(state.Present is null ? "none" : state.Present.Value.ToString(CultureInfo.InvariantCulture))
            .Append(" outcome=").Append(state.Outcome)
            .Append('\n');

        foreach (Timeline timeline in state.Timelines)
        {
            builder.Append(TimelineLine)
                .Append(" id=").Append(timeline.Id)
                .Append(" parent=").Append(timeline.ParentId is null ? "none" : timeline.ParentId.Value.ToString(CultureInfo.InvariantCulture))
                .Append(" branch=").Append(timeline.BranchTime is null ? "none" : timeline.BranchTime.Value.ToString(CultureInfo.InvariantCulture))
                .Append(" first=").Append(timeline.FirstStateTime)
                .Append(" status=").Append(timeline.Status)
                .Append(" slot=").Append(timeline.OccupiesActiveSlot ? "1" : "0")
                .Append('\n');

            for (int time = timeline.FirstStateTime; time <= timeline.FrontierTime; time++)
            {
                builder.Append(StateLine)
                    .Append(" t=").Append(time)
                    .Append(" board=").Append(timeline.StateAt(time).ToCompactString())
                    .Append('\n');
            }
        }

        return builder.ToString();
    }

    /// <summary>Reads a run back from text.</summary>
    public static GameState Read(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        string[] lines = text.Replace("\r\n", "\n").Split('\n');

        ReadHeader(lines);

        LevelDefinition? level = null;
        BoardSize? size = null;
        int selected = GameState.RootTimelineId;
        int budgetLeft = 0;
        string? recordedPresent = null;
        string? recordedOutcome = null;

        List<Timeline> timelines = new List<Timeline>();
        TimelineUnderConstruction? current = null;

        for (int index = 1; index < lines.Length; index++)
        {
            string line = lines[index].Trim();

            if (line.Length == 0)
            {
                continue;
            }

            string kind = KindOf(line);
            Dictionary<string, string> fields = FieldsOf(line);

            switch (kind)
            {
                case LevelLine:
                    size = ReadBoardSize(fields);
                    level = ReadLevel(fields, size);
                    break;

                case GameLine:
                    selected = ReadInt(fields, "selected");
                    budgetLeft = ReadInt(fields, "budgetLeft");
                    recordedPresent = Required(fields, "present");
                    recordedOutcome = Required(fields, "outcome");
                    break;

                case TimelineLine:
                    Flush(current, timelines);
                    current = ReadTimelineHeader(fields);
                    break;

                case StateLine:
                    if (current is null)
                    {
                        throw new GameSaveException("A state line appeared before any timeline line.");
                    }

                    if (size is null)
                    {
                        throw new GameSaveException("A state line appeared before the level line.");
                    }

                    current.States.Add(ParseBoard(size, Required(fields, "board")));
                    break;

                default:
                    // A line kind this build does not know about. Newer saves may
                    // carry more; skipping is what keeps them loadable.
                    break;
            }
        }

        Flush(current, timelines);

        if (level is null)
        {
            throw new GameSaveException("The save has no level line.");
        }

        if (timelines.Count == 0)
        {
            throw new GameSaveException("The save has no timelines.");
        }

        GameState restored = GameState.Restore(level, timelines.ToArray(), selected, budgetLeft);

        VerifyAgreement(restored, recordedPresent, recordedOutcome);

        return restored;
    }

    private static void ReadHeader(string[] lines)
    {
        if (lines.Length == 0)
        {
            throw new GameSaveException("The save is empty.");
        }

        string[] header = lines[0].Trim().Split(' ');

        if (header.Length < 2 || header[0] != Magic)
        {
            throw new GameSaveException($"The save does not start with \"{Magic}\".");
        }

        if (!int.TryParse(header[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int version))
        {
            throw new GameSaveException($"The save has an unreadable version: \"{header[1]}\".");
        }

        if (version > CurrentVersion)
        {
            throw new GameSaveException(
                $"The save is version {version}; this build reads up to {CurrentVersion}.");
        }
    }

    private static BoardSize ReadBoardSize(Dictionary<string, string> fields) =>
        BoardSize.FromBoxShape(ReadInt(fields, "boxWidth"), ReadInt(fields, "boxHeight"));

    private static LevelDefinition ReadLevel(Dictionary<string, string> fields, BoardSize size)
    {
        try
        {
            return new LevelDefinition(
                Decode(Required(fields, "id")),
                ParseBoard(size, Required(fields, "start")),
                ReadInt(fields, "budget"),
                ReadInt(fields, "window"),
                ReadInt(fields, "slots"),
                ReadInt(fields, "finalDepth"));
        }
        catch (ArgumentException exception)
        {
            throw new GameSaveException($"The saved level is not valid: {exception.Message}", exception);
        }
    }

    private static TimelineUnderConstruction ReadTimelineHeader(Dictionary<string, string> fields)
    {
        string statusText = Required(fields, "status");

        if (!TryParseStatus(statusText, out TimelineStatus status))
        {
            throw new GameSaveException($"Unknown timeline status \"{statusText}\".");
        }

        return new TimelineUnderConstruction
        {
            Id = ReadInt(fields, "id"),
            ParentId = ReadOptionalInt(fields, "parent"),
            BranchTime = ReadOptionalInt(fields, "branch"),
            FirstStateTime = ReadInt(fields, "first"),
            Status = status,
            OccupiesActiveSlot = Required(fields, "slot") == "1",
        };
    }

    private static void Flush(TimelineUnderConstruction? current, List<Timeline> timelines)
    {
        if (current is null)
        {
            return;
        }

        if (current.States.Count == 0)
        {
            throw new GameSaveException($"Timeline L{current.Id} has no states.");
        }

        timelines.Add(Timeline.Restore(
            current.Id,
            current.ParentId,
            current.BranchTime,
            current.FirstStateTime,
            current.States.ToArray(),
            current.Status,
            current.OccupiesActiveSlot));
    }

    /// <summary>
    /// The present and the outcome are derived, so a save that records different
    /// ones was either corrupted or written by rules this build no longer plays by.
    /// Either way, loading it quietly would be worse than saying so.
    /// </summary>
    private static void VerifyAgreement(GameState restored, string? recordedPresent, string? recordedOutcome)
    {
        if (recordedPresent is null || recordedOutcome is null)
        {
            throw new GameSaveException("The save has no game line.");
        }

        string present = restored.Present is null
            ? "none"
            : restored.Present.Value.ToString(CultureInfo.InvariantCulture);

        if (present != recordedPresent)
        {
            throw new GameSaveException(
                $"The save records present \"{recordedPresent}\" but the timelines give \"{present}\".");
        }

        if (restored.Outcome.ToString() != recordedOutcome)
        {
            throw new GameSaveException(
                $"The save records outcome \"{recordedOutcome}\" but the timelines give \"{restored.Outcome}\".");
        }
    }

    private static bool TryParseStatus(string text, out TimelineStatus status)
    {
        foreach (TimelineStatus candidate in new[]
        {
            TimelineStatus.Active, TimelineStatus.Solved, TimelineStatus.Dead, TimelineStatus.Inactive,
        })
        {
            if (candidate.ToString() == text)
            {
                status = candidate;
                return true;
            }
        }

        status = TimelineStatus.Active;
        return false;
    }

    private static SudokuBoard ParseBoard(BoardSize size, string compact)
    {
        try
        {
            return SudokuBoard.Parse(size, compact);
        }
        catch (FormatException exception)
        {
            throw new GameSaveException($"A saved board could not be read: {exception.Message}", exception);
        }
    }

    private static string KindOf(string line)
    {
        int space = line.IndexOf(' ');

        return space < 0 ? line : line.Substring(0, space);
    }

    private static Dictionary<string, string> FieldsOf(string line)
    {
        Dictionary<string, string> fields = new Dictionary<string, string>(StringComparer.Ordinal);
        string[] parts = line.Split(' ');

        for (int index = 1; index < parts.Length; index++)
        {
            int equals = parts[index].IndexOf('=');

            if (equals <= 0)
            {
                continue;
            }

            fields[parts[index].Substring(0, equals)] = parts[index].Substring(equals + 1);
        }

        return fields;
    }

    private static string Required(Dictionary<string, string> fields, string key)
    {
        if (!fields.TryGetValue(key, out string? value))
        {
            throw new GameSaveException($"A line is missing \"{key}\".");
        }

        return value;
    }

    private static int ReadInt(Dictionary<string, string> fields, string key)
    {
        string value = Required(fields, key);

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            throw new GameSaveException($"\"{key}\" is not a number: \"{value}\".");
        }

        return parsed;
    }

    private static int? ReadOptionalInt(Dictionary<string, string> fields, string key)
    {
        string value = Required(fields, key);

        return value == "none" ? (int?)null : ReadInt(fields, key);
    }

    /// <summary>Keeps values free of the space and equals that structure a line.</summary>
    private static string Encode(string value) =>
        value.Replace("%", "%25").Replace(" ", "%20").Replace("=", "%3D");

    private static string Decode(string value) =>
        value.Replace("%3D", "=").Replace("%20", " ").Replace("%25", "%");

    private sealed class TimelineUnderConstruction
    {
        internal int Id { get; set; }

        internal int? ParentId { get; set; }

        internal int? BranchTime { get; set; }

        internal int FirstStateTime { get; set; }

        internal TimelineStatus Status { get; set; }

        internal bool OccupiesActiveSlot { get; set; }

        internal List<SudokuBoard> States { get; } = new List<SudokuBoard>();
    }
}
