using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="DirectionResolver"/>, which turns several simultaneously held
/// directions into the one the character is actually moving in, and remembers the
/// facing after movement stops. Nothing here touches Godot or an Input Map.
/// </summary>
public class DirectionResolverTests
{
	[Fact]
	public void Active_IsNullBeforeAnythingIsPressed()
	{
		var directions = new DirectionResolver();

		Assert.Null(directions.Active);
	}

	[Fact]
	public void Facing_StartsFacingDown()
	{
		var directions = new DirectionResolver();

		Assert.Equal(Direction.Down, directions.Facing);
	}

	[Theory]
	[InlineData(Direction.Up)]
	[InlineData(Direction.Down)]
	[InlineData(Direction.Left)]
	[InlineData(Direction.Right)]
	public void Press_MakesThatDirectionActiveAndFacing(Direction direction)
	{
		var directions = new DirectionResolver();

		directions.Press(direction);

		Assert.Equal(direction, directions.Active);
		Assert.Equal(direction, directions.Facing);
		Assert.True(directions.IsHeld(direction));
	}

	[Fact]
	public void Press_TheMostRecentOneWinsWhileBothAreHeld()
	{
		var directions = new DirectionResolver();

		directions.Press(Direction.Right);
		directions.Press(Direction.Up);

		Assert.Equal(Direction.Up, directions.Active);
		Assert.True(directions.IsHeld(Direction.Right));
	}

	[Fact]
	public void Release_FallsBackToTheMostRecentDirectionStillHeld()
	{
		// The case this class exists for: rolling a thumb from one key onto another and
		// off again should resume the first direction, not stop the character dead.
		var directions = new DirectionResolver();

		directions.Press(Direction.Right);
		directions.Press(Direction.Up);
		directions.Release(Direction.Up);

		Assert.Equal(Direction.Right, directions.Active);
		Assert.Equal(Direction.Right, directions.Facing);
	}

	[Fact]
	public void Release_FallsBackThroughThreeDirectionsInReversePressOrder()
	{
		var directions = new DirectionResolver();

		directions.Press(Direction.Left);
		directions.Press(Direction.Down);
		directions.Press(Direction.Right);
		Assert.Equal(Direction.Right, directions.Active);

		directions.Release(Direction.Right);
		Assert.Equal(Direction.Down, directions.Active);

		directions.Release(Direction.Down);
		Assert.Equal(Direction.Left, directions.Active);

		directions.Release(Direction.Left);
		Assert.Null(directions.Active);
	}

	[Fact]
	public void Release_OfADirectionThatIsNotActiveLeavesMovementAlone()
	{
		var directions = new DirectionResolver();

		directions.Press(Direction.Right);
		directions.Press(Direction.Up);
		directions.Release(Direction.Right);

		Assert.Equal(Direction.Up, directions.Active);
		Assert.Equal(Direction.Up, directions.Facing);
		Assert.False(directions.IsHeld(Direction.Right));
	}

	[Fact]
	public void Facing_SurvivesEverythingBeingReleased()
	{
		var directions = new DirectionResolver();

		directions.Press(Direction.Left);
		directions.Release(Direction.Left);

		Assert.Null(directions.Active);
		Assert.Equal(Direction.Left, directions.Facing);
	}

	[Fact]
	public void Press_OfAnAlreadyHeldDirectionMovesItBackToTheFront()
	{
		var directions = new DirectionResolver();

		directions.Press(Direction.Right);
		directions.Press(Direction.Up);
		directions.Press(Direction.Right);
		Assert.Equal(Direction.Right, directions.Active);

		// Right was re-pressed, so releasing it must fall back to Up rather than
		// leaving a stale duplicate behind that keeps reporting Right.
		directions.Release(Direction.Right);

		Assert.Equal(Direction.Up, directions.Active);
		Assert.False(directions.IsHeld(Direction.Right));
	}

	[Fact]
	public void Release_OfSomethingNeverPressedChangesNothing()
	{
		var directions = new DirectionResolver();

		directions.Press(Direction.Up);
		directions.Release(Direction.Down);

		Assert.Equal(Direction.Up, directions.Active);
		Assert.Equal(Direction.Up, directions.Facing);
	}

	[Fact]
	public void IsHeld_ReportsOnlyWhatIsCurrentlyHeld()
	{
		var directions = new DirectionResolver();

		directions.Press(Direction.Left);
		directions.Press(Direction.Down);

		Assert.True(directions.IsHeld(Direction.Left));
		Assert.True(directions.IsHeld(Direction.Down));
		Assert.False(directions.IsHeld(Direction.Up));
		Assert.False(directions.IsHeld(Direction.Right));
	}

	[Fact]
	public void OppositeDirectionsHeldTogetherResolveToTheLatest()
	{
		// Holding Left and Right at once is reachable on a keyboard. It has to pick one
		// rather than cancelling out, or the character freezes for as long as both are
		// down.
		var directions = new DirectionResolver();

		directions.Press(Direction.Left);
		directions.Press(Direction.Right);

		Assert.Equal(Direction.Right, directions.Active);

		directions.Release(Direction.Right);

		Assert.Equal(Direction.Left, directions.Active);
	}

	[Fact]
	public void Active_PrefersTheStrongerDirectionOverTheMoreRecentOne()
	{
		// A stick pushed mostly right and a little down: the character should go right,
		// whichever of the two axes happened to cross the deadzone first.
		var directions = new DirectionResolver();

		directions.Press(Direction.Right, 0.9f);
		directions.Press(Direction.Down, 0.2f);

		Assert.Equal(Direction.Right, directions.Active);
		Assert.Equal(Direction.Right, directions.Facing);
	}

	[Fact]
	public void Active_KeepsPressOrderWhenTwoAnalogDirectionsTie()
	{
		var directions = new DirectionResolver();

		directions.Press(Direction.Right, 0.7f);
		directions.Press(Direction.Down, 0.7f);

		Assert.Equal(Direction.Down, directions.Active);
	}

	[Fact]
	public void SetStrength_HandsTheWinToTheOtherAxisWhenItTakesOver()
	{
		var directions = new DirectionResolver();

		directions.Press(Direction.Right, 0.9f);
		directions.Press(Direction.Down, 0.2f);
		Assert.Equal(Direction.Right, directions.Active);

		// The stick keeps rotating: neither direction is pressed or released, only the
		// magnitudes change.
		directions.SetStrength(Direction.Right, 0.2f);
		directions.SetStrength(Direction.Down, 0.9f);

		Assert.Equal(Direction.Down, directions.Active);
		Assert.Equal(Direction.Down, directions.Facing);
	}

	[Fact]
	public void SetStrength_OnADirectionThatIsNotHeldIsIgnored()
	{
		var directions = new DirectionResolver();

		directions.Press(Direction.Up, 0.5f);
		directions.SetStrength(Direction.Left, 1f);

		Assert.Equal(Direction.Up, directions.Active);
		Assert.False(directions.IsHeld(Direction.Left));
	}

	[Theory]
	[InlineData(5f)]
	[InlineData(-3f)]
	public void Press_ClampsStrengthWithoutRejectingTheDirection(float strength)
	{
		var directions = new DirectionResolver();

		directions.Press(Direction.Left, strength);

		Assert.Equal(Direction.Left, directions.Active);
		Assert.True(directions.IsHeld(Direction.Left));
	}

	[Fact]
	public void AStickSweptFromOneDirectionToTheNextChangesOverExactlyOnce()
	{
		// The bug this rule exists for. Under plain press order, a stick rotating from
		// right to down crosses the deadzone on both axes and the winner follows
		// whichever crossed most recently, flipping repeatedly across the arc. Following
		// the stronger axis instead makes the handover happen once, at 45 degrees.
		var directions = new DirectionResolver();
		directions.Press(Direction.Right, 1f);
		directions.Press(Direction.Down, 0f);

		Direction? previous = directions.Active;
		int changes = 0;

		for (int degrees = 0; degrees <= 90; degrees++)
		{
			double angle = degrees * System.Math.PI / 180d;
			directions.SetStrength(Direction.Right, (float)System.Math.Cos(angle));
			directions.SetStrength(Direction.Down, (float)System.Math.Sin(angle));

			if (directions.Active != previous)
				changes++;

			previous = directions.Active;
		}

		Assert.Equal(1, changes);
		Assert.Equal(Direction.Down, directions.Active);
	}
}
