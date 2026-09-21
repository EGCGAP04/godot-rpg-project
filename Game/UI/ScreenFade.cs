using System;
using Godot;

/// <summary>
/// A full-screen black rectangle that can be tweened in and out, used both by the
/// combat overlay and by world transitions so the two never drift apart in colour or
/// duration.
/// </summary>
/// <remarks>
/// <para>
/// The tween is created on this node (<c>CreateTween()</c>), never on the tree. A
/// tween is bound to the node that creates it and follows that node's process mode,
/// and this scene sets <c>process_mode = Always</c> — which is what keeps the fade
/// playing while a combat has the rest of the tree paused.
/// </para>
/// <para>
/// It is a <c>Control</c> rather than a <c>CanvasLayer</c> so the host decides where
/// it sits: <c>Combat</c> is already a <c>CanvasLayer</c> and adds it as its last
/// child, while <c>Main</c> puts it on a HUD layer of its own.
/// </para>
/// </remarks>
public partial class ScreenFade : ColorRect
{
	[Export]
	public float Duration = 0.25f;

	/// <summary>Fully black, hiding whatever is behind it.</summary>
	public const float Opaque = 1f;

	/// <summary>Fully transparent.</summary>
	public const float Clear = 0f;

	private Tween _tween;

	/// <summary>Jumps straight to an alpha with no tween, for setting up a starting state.</summary>
	public void SetAlpha(float alpha)
	{
		_tween?.Kill();
		Color = new Color(Color, alpha);
	}

	/// <summary>
	/// Tweens to the given alpha and runs <paramref name="onFinished"/> afterwards.
	/// A fade already in flight is killed first, so overlapping calls cannot leave two
	/// tweens fighting over the same property or fire a stale callback.
	/// </summary>
	/// <remarks>
	/// A killed tween never raises <c>Finished</c>, so <b>do not park state that must be
	/// cleared in <paramref name="onFinished"/> alone</b> unless the caller is the only
	/// one that can start a fade on this instance. <c>Main</c> gets away with it because
	/// its two callers are mutually exclusive and combat owns a separate instance; a
	/// third caller would strand whatever flag the interrupted callback was going to
	/// clear.
	/// </remarks>
	public void FadeTo(float alpha, Action onFinished = null)
	{
		_tween?.Kill();
		_tween = CreateTween();
		_tween.TweenProperty(this, "color:a", alpha, Duration);

		if (onFinished != null)
			_tween.Finished += onFinished;
	}
}
