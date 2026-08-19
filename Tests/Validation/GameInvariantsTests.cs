using System.Linq;
using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Validation;

namespace FiveDSudoku.Tests;

/// <summary>
/// The invariant checker, from both sides: it must stay quiet through a real
/// session, and it must actually speak up when something is wrong.
///
/// The engine will not produce a broken state, so the violations are provoked by
/// handing the checker a transition that runs backwards in time. A state that
/// went from three placements to one, with budget going up rather than down, is
/// exactly the shape of the corruption these invariants exist to catch.
/// </summary>
[TestFixture]
public sealed class GameInvariantsTests
{
    private static GameState StartGame() =>
        GameState.Start(Levels.SolvableFourByFour(temporalBudget: 3, temporalWindow: 16));

    // ---- quiet through a real session -------------------------------------

    [Test]
    public void InvariantsHoldFromTheVeryFirstState()
    {
        Assert.That(GameInvariants.Check(StartGame()), Is.Empty);
        Assert.That(GameInvariants.Holds(StartGame()), Is.True);
    }

    [Test]
    public void InvariantsHoldAfterEveryActionOfAMultiTimelineSession()
    {
        GameState game = StartGame();

        void Step(GameState next)
        {
            Assert.That(
                GameInvariants.CheckTransition(game, next).Select(violation => violation.ToString()),
                Is.Empty);
            game = next;
        }

        // ordinary play
        Step(game.PlaceValue(0, 1, 4).State);
        Step(game.PlaceValue(0, 2, 3).State);

        // a branch that turns out impossible
        TemporalMoveResult doomed = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3);
        Assert.That(doomed.Succeeded, Is.True, doomed.Rejection.ToString());
        Step(doomed.State);

        // switching, which changes nothing
        Step(game.SelectTimeline(doomed.NewTimelineId!.Value));
        Step(game.SelectTimeline(GameState.RootTimelineId));

        // parking and bringing back the root
        Step(game.DeactivateTimeline(GameState.RootTimelineId).State);
        Step(game.ActivateTimeline(GameState.RootTimelineId).State);

        // and on to a win
        Step(Levels.SolveSelectedTimeline(game));

        Assert.That(game.Outcome, Is.EqualTo(GameOutcome.Won));
    }

    [Test]
    public void InvariantsHoldThroughARunThatIsLostAndThenRecovered()
    {
        GameState game = StartGame();
        GameState previous = game;

        game = game.PlaceValue(0, 1, 3).State;

        while (game.Outcome == GameOutcome.InProgress)
        {
            Assert.That(GameInvariants.CheckTransition(previous, game), Is.Empty);
            previous = game;
            game = Levels.PlayAnyLegalMove(game);
        }

        Assert.That(game.Outcome, Is.EqualTo(GameOutcome.GameOver));
        Assert.That(GameInvariants.Check(game), Is.Empty);

        TemporalMoveResult rescue = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 4);

        Assert.That(rescue.Succeeded, Is.True, rescue.Rejection.ToString());
        Assert.That(GameInvariants.CheckTransition(game, rescue.State), Is.Empty);
    }

    // ---- and loud when something is wrong ---------------------------------

    [Test]
    public void HistoryThatShrankIsReported()
    {
        GameState early = StartGame();
        GameState late = early.PlaceValue(0, 1, 4).State.PlaceValue(0, 2, 3).State;

        // Read the transition backwards: the root would have lost two states.
        var violations = GameInvariants.CheckTransition(late, early);

        Assert.That(
            violations.Select(violation => violation.Invariant),
            Does.Contain(GameInvariant.ImmutableHistory));
        Assert.That(
            violations.Select(violation => violation.Detail),
            Has.Some.Contains("lost states"));
    }

    [Test]
    public void ATimelineThatDisappearedIsReported()
    {
        GameState before = StartGame().PlaceValue(0, 1, 4).State;
        GameState withBranch = before.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3).State;

        var violations = GameInvariants.CheckTransition(withBranch, before);

        Assert.That(
            violations.Select(violation => violation.Detail),
            Has.Some.Contains("is gone"));
    }

    [Test]
    public void BudgetThatWentBackUpIsReported()
    {
        GameState before = StartGame().PlaceValue(0, 1, 4).State;
        GameState spent = before.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3).State;

        Assert.That(spent.RemainingTemporalBudget, Is.LessThan(before.RemainingTemporalBudget));

        var violations = GameInvariants.CheckTransition(spent, before);

        Assert.That(
            violations.Select(violation => violation.Invariant),
            Does.Contain(GameInvariant.TemporalBudgetWithinBounds));
        Assert.That(
            violations.Select(violation => violation.Detail),
            Has.Some.Contains("went up"));
    }

    [Test]
    public void RewrittenHistoryIsReported()
    {
        // Two runs of the same level that diverge at the first move: read as a
        // transition, the second looks like the first with T1 rewritten.
        GameState first = StartGame().PlaceValue(0, 1, 4).State;
        GameState second = StartGame().PlaceValue(0, 1, 3).State;

        var violations = GameInvariants.CheckTransition(first, second);

        Assert.That(
            violations.Select(violation => violation.Detail),
            Has.Some.Contains("rewrote the state at T1"));
    }

    [Test]
    public void CheckingNeedsStates()
    {
        Assert.That(() => GameInvariants.Check(null!), Throws.ArgumentNullException);
        Assert.That(() => GameInvariants.CheckTransition(null!, StartGame()), Throws.ArgumentNullException);
        Assert.That(() => GameInvariants.CheckTransition(StartGame(), null!), Throws.ArgumentNullException);
    }
}
