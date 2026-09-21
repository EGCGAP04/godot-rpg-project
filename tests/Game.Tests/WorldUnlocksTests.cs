using System;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="WorldUnlocks"/>, which answers whether a world can be entered
/// this cycle. The one rule worth pinning down is that the Real world is never
/// optional, however the two flags are set.
/// </summary>
public class WorldUnlocksTests
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(false, true)]
	[InlineData(true, true)]
	public void IsUnlocked_AlwaysReportsTheRealWorldAsAvailable(bool fantasy, bool nightmare)
	{
		var unlocked = new WorldUnlocks(fantasy, nightmare);

		Assert.True(unlocked.IsUnlocked(World.Real));
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void IsUnlocked_ReportsTheFantasyFlagAsGiven(bool fantasy)
	{
		var unlocked = new WorldUnlocks(Fantasy: fantasy, Nightmare: true);

		Assert.Equal(fantasy, unlocked.IsUnlocked(World.Fantasy));
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void IsUnlocked_ReportsTheNightmareFlagAsGiven(bool nightmare)
	{
		var unlocked = new WorldUnlocks(Fantasy: true, Nightmare: nightmare);

		Assert.Equal(nightmare, unlocked.IsUnlocked(World.Nightmare));
	}

	[Fact]
	public void All_UnlocksBothOptionalWorlds()
	{
		WorldUnlocks unlocked = WorldUnlocks.All;

		Assert.True(unlocked.IsUnlocked(World.Fantasy));
		Assert.True(unlocked.IsUnlocked(World.Nightmare));
	}

	[Fact]
	public void IsUnlocked_RejectsAWorldThatIsNotDeclared()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => WorldUnlocks.All.IsUnlocked((World)99));
	}
}
