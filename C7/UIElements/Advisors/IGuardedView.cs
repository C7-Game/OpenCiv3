using System;

/// <summary>
/// A view that may need something from the player before it can close, e.g. confirming a choice.
/// </summary>
public interface IGuardedView {
	/// <summary>
	/// Calls onClosed once it's OK to close, either right away or after asking the player.
	/// Never calling it means the view stays open.
	/// </summary>
	void RequestClose(Action onClosed);
}
