using System;
using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="StickPresses"/>: one push of the stick is one press, with
/// hysteresis between pressing and releasing.
/// </summary>
public class StickPressesTests
{
	private const string Left = "move_left";
	private const string Right = "move_right";

	/// <summary>
	/// The push measured in #117, from the centre to the end of the axis and back, as
	/// the strengths Godot reports with a deadzone of 0.2. Read as presses, it gave 9.
	/// </summary>
	private static readonly float[] OnePush = { 0f, 0.0625f, 0.3125f, 0.625f, 0.875f, 1f, 0.975f, 0.9375f, 0.5f, 0.125f, 0f, 0f };

	private static int Presses(StickPresses stick, string action, params float[] strengths) =>
		strengths.Count(strength => stick.Press(action, strength));

	[Fact]
	public void OnePush_IsOnePress()
	{
		Assert.Equal(1, Presses(new StickPresses(), Left, OnePush));
	}

	[Fact]
	public void APushThatStaysBelowPressAt_IsNoPress()
	{
		Assert.Equal(0, Presses(new StickPresses(), Left, 0f, 0.2f, 0.49f, 0.2f, 0f));
	}

	[Fact]
	public void AStrengthOfExactlyPressAt_IsAPress()
	{
		Assert.True(new StickPresses().Press(Left, StickPresses.PressAt));
	}

	[Fact]
	public void AStickTremblingAroundPressAt_PressesOnce()
	{
		Assert.Equal(1, Presses(new StickPresses(), Left, 0.5f, 0.49f, 0.51f, 0.45f, 0.55f, 0.48f, 0.52f));
	}

	[Fact]
	public void AStickTremblingAroundReleaseBelow_PressesOnlyOnceItHasFallenBelowIt()
	{
		var stick = new StickPresses();

		Assert.True(stick.Press(Left, 1f));

		// Down to the release threshold itself, and back up: still the same push.
		Assert.False(stick.Press(Left, StickPresses.ReleaseBelow));
		Assert.False(stick.Press(Left, 0.6f));

		// Below it, the stick is released, and the next push is a press again.
		Assert.False(stick.Press(Left, 0.29f));
		Assert.False(stick.Press(Left, 0.4f));
		Assert.True(stick.Press(Left, 0.5f));
	}

	[Fact]
	public void TwoPushes_AreTwoPresses()
	{
		Assert.Equal(2, Presses(new StickPresses(), Left, OnePush.Concat(OnePush).ToArray()));
	}

	[Fact]
	public void EachAction_HasAStateOfItsOwn()
	{
		var stick = new StickPresses();

		Assert.True(stick.Press(Left, 1f));
		Assert.True(stick.Press(Right, 1f));
		Assert.False(stick.Press(Left, 1f));
		Assert.False(stick.Press(Right, 1f));
	}

	[Fact]
	public void LeftThenRight_WithoutStoppingAtTheCentre_AreTwoPresses_AndLeftAgainIsAThird()
	{
		var stick = new StickPresses();

		Assert.True(stick.Press(Left, 1f));

		// Swept straight across: the axis now gives left nothing and right everything.
		Assert.False(stick.Press(Left, 0f));
		Assert.True(stick.Press(Right, 0.9f));

		Assert.False(stick.Press(Right, 0f));
		Assert.True(stick.Press(Left, 0.9f));
	}

	[Fact]
	public void ANewInstance_CountsAStickAlreadyPushedAsAPress()
	{
		Assert.True(new StickPresses().Press(Left, 1f));
	}

	[Theory]
	[InlineData(0f)]
	[InlineData(1f)]
	public void TheEndsOfTheRange_AreAccepted(float strength)
	{
		new StickPresses().Press(Left, strength);
	}

	[Theory]
	[InlineData(-0.01f)]
	[InlineData(1.01f)]
	[InlineData(float.NaN)]
	[InlineData(float.PositiveInfinity)]
	public void AStrengthOutsideTheRange_IsRefused(float strength)
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new StickPresses().Press(Left, strength));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void AnActionWithNoName_IsRefused(string action)
	{
		Assert.ThrowsAny<ArgumentException>(() => new StickPresses().Press(action, 1f));
	}

	[Fact]
	public void TheThresholds_LeaveABandBetweenThem()
	{
		Assert.True(StickPresses.ReleaseBelow < StickPresses.PressAt);
	}
}
