/// <summary>
/// Every roll a fight makes. Combat never reaches for a random number any other way, so
/// whoever hands it one of these decides what the dice say: the game a seeded generator,
/// a test a list of rolls written by hand.
/// </summary>
/// <remarks>
/// A chance comes up when the roll is below it: <c>random.Percent() &lt; chance</c>. The
/// roll never reaches 100, so a chance of 100 always comes up and a chance of 0 never
/// does, with no special case for either.
/// </remarks>
public interface ICombatRandom
{
	/// <summary>A number from 0 up to, but not including, 100.</summary>
	double Percent();

	/// <summary>An index from 0 up to, but not including, <paramref name="count"/>.</summary>
	int Index(int count);
}
