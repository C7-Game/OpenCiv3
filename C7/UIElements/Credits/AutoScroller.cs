using Godot;

public class AutoScroller {
	private readonly ScrollContainer _scrollContainer;
	private readonly VBoxContainer _box;

	// How fast the credits scroll on their own, in pixels per second. Slow
	// enough to read at a relaxed pace.
	private const float AutoScrollSpeed = 24.0f;

	// After the player scrolls the credits themselves, leave the column alone
	// for a moment so we don't fight them.
	private const float PauseAfterManualScroll = 3.0f;

	private float autoScrollPause;
	private float scrollRemainder;
	private int lastScrollPosition;

	public AutoScroller(ScrollContainer scrollContainer, VBoxContainer box) {
		_scrollContainer = scrollContainer;
		_box = box;
	}

	public void ScrollByDelta(double delta) {
		int current = _scrollContainer.ScrollVertical;

		// Scrolling up means the player wants to read something, so give them
		// a few seconds before we resume.
		if (current < lastScrollPosition) {
			autoScrollPause = PauseAfterManualScroll;
		}
		lastScrollPosition = current;

		if (autoScrollPause > 0) {
			autoScrollPause -= (float)delta;
			return;
		}

		int maxScroll = Mathf.Max(0, (int)(_box.Size.Y - _scrollContainer.Size.Y));
		if (current >= maxScroll) {
			return;
		}

		// Accumulate the fractional pixels so the scroll still advances when
		// the speed is lower than one pixel per frame.
		scrollRemainder += AutoScrollSpeed * (float)delta;
		int wholePixels = (int)scrollRemainder;
		if (wholePixels == 0) {
			return;
		}
		scrollRemainder -= wholePixels;

		int target = Mathf.Min(current + wholePixels, maxScroll);
		_scrollContainer.ScrollVertical = target;
		lastScrollPosition = target;
	}
}
