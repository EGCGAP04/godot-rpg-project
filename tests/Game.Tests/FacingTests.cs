using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="Facing"/>, which decides whether a character is looking at a
/// target. Offsets follow Godot's 2D axes: x grows to the right, y grows downwards.
/// </summary>
public class FacingTests
{
	[Theory]
	[InlineData(Direction.Right, 16, 0)]
	[InlineData(Direction.Left, -16, 0)]
	[InlineData(Direction.Down, 0, 16)]
	[InlineData(Direction.Up, 0, -16)]
	public void ATargetStraightAhead_IsFaced(Direction facing, float dx, float dy)
	{
		Assert.True(Facing.IsToward(facing, dx, dy));
	}

	[Theory]
	[InlineData(Direction.Right, -16, 0)]
	[InlineData(Direction.Left, 16, 0)]
	[InlineData(Direction.Down, 0, -16)]
	[InlineData(Direction.Up, 0, 16)]
	public void ATargetBehind_IsNotFaced(Direction facing, float dx, float dy)
	{
		Assert.False(Facing.IsToward(facing, dx, dy));
	}

	[Theory]
	[InlineData(Direction.Right, 0, 16)]
	[InlineData(Direction.Down, 16, 0)]
	[InlineData(Direction.Left, 0, -16)]
	[InlineData(Direction.Up, -16, 0)]
	public void ATargetToOneSide_IsNotFaced(Direction facing, float dx, float dy)
	{
		Assert.False(Facing.IsToward(facing, dx, dy));
	}

	[Theory]
	[InlineData(Direction.Right, 16, 10, true)]
	[InlineData(Direction.Down, 16, 10, false)]
	[InlineData(Direction.Down, 10, 16, true)]
	[InlineData(Direction.Right, 10, 16, false)]
	public void AnOffTarget_IsFacedAlongItsLongerAxis(Direction facing, float dx, float dy, bool expected)
	{
		Assert.Equal(expected, Facing.IsToward(facing, dx, dy));
	}

	[Theory]
	[InlineData(Direction.Right)]
	[InlineData(Direction.Down)]
	public void ATargetOnTheDiagonal_IsFacedFromBothItsSides(Direction facing)
	{
		Assert.True(Facing.IsToward(facing, 12, 12));
	}

	[Theory]
	[InlineData(Direction.Down)]
	[InlineData(Direction.Up)]
	[InlineData(Direction.Left)]
	[InlineData(Direction.Right)]
	public void ATargetRightWhereTheCharacterIs_IsFacedWhicheverWay(Direction facing)
	{
		Assert.True(Facing.IsToward(facing, 0, 0));
	}
}
