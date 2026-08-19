using System.Text;
using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;

namespace FiveDSudoku.Tools.ScenarioRunner;

/// <summary>
/// Renders a run as text you can read at a glance in a terminal.
///
/// This is for debugging and for pinning scenario output down in tests, not a
/// step towards a user interface. It lives here rather than in Core because it is
/// presentation: Core answers what the state is, this decides how to look at it.
/// </summary>
public static class StateSummary
{
    /// <summary>Widest status word, so the frontier column lines up.</summary>
    private const int StatusWidth = 8;

    /// <summary>
    /// The whole run: where the present is, how the run stands, what every timeline
    /// is doing, what the budget has cost, and the board of the timeline currently
    /// being looked at.
    /// </summary>
    public static string Render(GameState state)
    {
        StringBuilder builder = new StringBuilder();

        builder.Append("PRESENT ").Append(state.Present is null ? "none" : $"T{state.Present.Value}").Append('\n');
        builder.Append("OUTCOME ").Append(state.Outcome).Append('\n');
        builder.Append('\n');

        foreach (Timeline timeline in state.Timelines)
        {
            builder.Append('L').Append(timeline.Id).Append("  ")
                .Append(timeline.Status.ToString().ToUpperInvariant().PadRight(StatusWidth))
                .Append(" frontier=T").Append(timeline.FrontierTime)
                .Append('\n');
        }

        builder.Append('\n');
        builder.Append("Budget: ")
            .Append(state.Level.TemporalBudget - state.RemainingTemporalBudget)
            .Append('/').Append(state.Level.TemporalBudget).Append(" used").Append('\n');
        builder.Append('\n');

        builder.Append('L').Append(state.SelectedTimelineId).Append(" board:").Append('\n');
        builder.Append(state.SelectedTimeline.Frontier.ToString());

        return builder.ToString();
    }

    /// <summary>
    /// The timelines as a tree, so where each one branched off is visible at a
    /// glance. A status is shown only when it is not the ordinary one.
    /// </summary>
    public static string RenderTimelineTree(GameState state)
    {
        StringBuilder builder = new StringBuilder();

        foreach (Timeline timeline in state.Timelines)
        {
            if (timeline.ParentId is null)
            {
                builder.Append(Label(timeline)).Append('\n');
                AppendChildren(builder, state, timeline.Id, string.Empty);
            }
        }

        return builder.ToString();
    }

    private static void AppendChildren(StringBuilder builder, GameState state, int parentId, string indent)
    {
        Timeline[] children = ChildrenOf(state, parentId);

        for (int index = 0; index < children.Length; index++)
        {
            bool last = index == children.Length - 1;

            builder.Append(indent)
                .Append(last ? "└── " : "├── ")
                .Append(Label(children[index]))
                .Append('\n');

            AppendChildren(builder, state, children[index].Id, indent + (last ? "    " : "│   "));
        }
    }

    private static Timeline[] ChildrenOf(GameState state, int parentId)
    {
        int count = 0;

        foreach (Timeline timeline in state.Timelines)
        {
            if (timeline.ParentId == parentId)
            {
                count++;
            }
        }

        Timeline[] children = new Timeline[count];
        int next = 0;

        // Timelines are stored in the order they were created, which is ascending
        // id, so the tree comes out in a stable order without sorting.
        foreach (Timeline timeline in state.Timelines)
        {
            if (timeline.ParentId == parentId)
            {
                children[next++] = timeline;
            }
        }

        return children;
    }

    private static string Label(Timeline timeline) =>
        timeline.Status == TimelineStatus.Active
            ? $"L{timeline.Id}"
            : $"L{timeline.Id} ({timeline.Status.ToString().ToUpperInvariant()})";
}
