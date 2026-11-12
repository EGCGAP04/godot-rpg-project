using System;

/// <summary>
/// The curve that turns a stat into a percentage with diminishing returns:
/// <c>cap * stat / (stat + k)</c>. Armor becomes damage reduction through it, and luck
/// becomes critical chance.
/// </summary>
/// <remarks>
/// <para>
/// The result starts at 0 for a stat of 0, is half the cap when the stat equals
/// <c>k</c>, and approaches the cap without ever reaching it. So the ceiling comes out of
/// the formula and needs no clamp, and <c>k</c> is the one knob: the stat value at which
/// half the cap is reached.
/// </para>
/// <para>
/// Every input is checked. With a <c>k</c> of 0 the formula would return the full cap
/// for any stat, and with a negative one it would divide by zero or go negative: all
/// plausible-looking numbers, which is why they are refused instead.
/// </para>
/// </remarks>
public static class DiminishingReturns
{
	/// <param name="cap">The percentage the curve approaches, above 0.</param>
	/// <param name="stat">The stat's value, 0 or more.</param>
	/// <param name="k">The stat value that gives half the cap, above 0.</param>
	public static double Percentage(double cap, double stat, double k)
	{
		if (!double.IsFinite(cap) || cap <= 0)
			throw new ArgumentOutOfRangeException(nameof(cap), cap, "The cap must be a number above 0.");

		if (!double.IsFinite(stat) || stat < 0)
			throw new ArgumentOutOfRangeException(nameof(stat), stat, "The stat must be a number, 0 or more.");

		if (!double.IsFinite(k) || k <= 0)
			throw new ArgumentOutOfRangeException(nameof(k), k, "K must be a number above 0.");

		return cap * stat / (stat + k);
	}
}
