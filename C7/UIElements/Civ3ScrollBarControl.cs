using System.ComponentModel.DataAnnotations;
using System.Dynamic;
using Godot;

namespace C7.UIElements;

[GlobalClass]
[Tool]
public partial class Civ3ScrollBarControl : Control {
	public enum ScrollDirection {
		Vertical,
		Horizontal
	}

	public enum ScrollBarSize {
		Small,
		Large
	}

	public enum ArrowScrollDirection {
		None = 0,
		Decrement = -1,
		Increment = 1
	}

	// Control
	// │
	// ├── ScrollContainer
	// │   └── VBoxContainer
	// │       ├── Item (HBoxContainer or whatever)
	// │       ├── Item
	// │       ├── Item
	// │       └── Item
	// │
	// └── Civ3ScrollBarControl
	//     ├── ScrollBar (Civ3VScrollBar or Civ3HScrollBar)
	//     └── Civ3ScrollHandle

	// ScrollBar replaces the ScrollContainer's scroll bar completely.
	// This way we can place it wherever we want, left, right, top bottom, middle,
	// have it have a different size to the container, etc.
	// ScrollBar, while the visible scroll bar, controls the ScrollContainer (hidden) scroll bar.
	// Civ3ScrollHandle replaces ScrollBar's grabber, because we want to control its texture size.
	// So all in all, when we scroll, the ScrollContainer's bar is actually scrolling,
	// but we see the ScrollBar's scroll textures updating.
	// When we "scroll" using the grabber (Civ3ScrollHandle),
	// we actually update the ScrollContainer's scroll bar, 
	// and "fake" its position along our custom scroll bar,
	// to make it seem like it's moving.

	[Export] public ScrollDirection Direction { get; set; } = ScrollDirection.Vertical;
	[Export] public ScrollBarSize SizePreset { get; set; } = ScrollBarSize.Small;
	[Export] public ScrollContainer TargetContainer { get; set; }
	[Export] public ScrollBar ScrollBar { get; set; }
	[Export] public bool Step { get; set; }
	[Export] public Civ3ScrollHandle ScrollHandle { get; set; }
	// Time we wait before we start repeatedly increment/decrement when pushing the respective buttons
	[Export] public double ArrowScrollInitialWaitTime { get; set; } = 0.3;
	// Time we wait before each repeated increment/decrement action when pushing the respective buttons
	[Export] public double ArrowScrollInterval { get; set; } = 0.2;

	public ScrollBar targetScrollBar { get; private set; }
	public float topPadding { get; private set; } // which is also the decrement icon height (when vertical)
	public float bottomPadding { get; private set; } // which is also the increment icon height (when vertical)
	public float leftPadding { get; private set; } // which is also the decrement icon width (when horizontal)
	public float rightPadding { get; private set; }// which is also the increment icon width (when horizontal)

	private bool syncing;

	private Timer arrowScrollTimer;

	private double lastPressedValue;
	private ArrowScrollDirection arrowScrollDirection = ArrowScrollDirection.None;

	public override void _Ready() {
		this.Visible = false;
		// this.SizePreset = ScrollBarSize.Large;

		if (this.PreLaunchCheckFailed()) {
			return;
		}

		this.TargetContainer.MouseFilter = Control.MouseFilterEnum.Stop;

		this.targetScrollBar = Direction == ScrollDirection.Vertical
			? this.TargetContainer.GetVScrollBar()
			: this.TargetContainer.GetHScrollBar();

		this.SetupScrollBar();

		ScrollHandle.Initialize(this, this.SizePreset);

		this.SetupArrowScrolling();

		CallDeferred(nameof(ConnectScrollBars));

		this.targetScrollBar.VisibilityChanged += () => {
			this.Visible = this.targetScrollBar.Visible;
		};
	}

	private bool PreLaunchCheckFailed() {
		var error = false;
		if (this.TargetContainer is null) {
			GD.PushError($"TargetContainer (ScrollContainer) should be assigned in the editor");
			error = true;
		}
		if (this.ScrollBar is null) {
			GD.PushError($"ScrollBar (Civ3VScrollBar or Civ3HScrollBar) should be assigned in the editor");
			error = true;
		}
		if (this.ScrollHandle is null) {
			GD.PushError($"ScrollHandle (Civ3ScrollHandle) should be assigned in the editor");
			error = true;
		}
		if (this.ScrollBar is not Civ3VScrollBar && this.ScrollBar is not Civ3HScrollBar) {
			GD.PushError($"ScrollBar should either be a Civ3VScrollBar or a Civ3HScrollBar node");
			error = true;
		}
		return error;
	}

	private void SetupScrollBar() {
		ApplyTheme();

		if (this.TargetContainer.GetChild(0) is BoxContainer mainContainer && mainContainer.GetChildCount() > 0) {
			if (mainContainer.GetChild(0) is Control firstItem) {
				if (Step) {
					float step = this.Direction == ScrollDirection.Vertical
						? firstItem.Size.Y
						: firstItem.Size.X;
					this.targetScrollBar.Step = step;
				} else {
					this.targetScrollBar.Step = 20.0f;
				}
			}
		}

		this.ScrollBar.MouseFilter = MouseFilterEnum.Stop;
	}

	private void ConnectScrollBars() {
		this.SyncFromTarget();

		this.targetScrollBar.ValueChanged += value => {
			if (this.syncing)
				return;

			this.syncing = true;
			this.ScrollBar.Value = value;
			this.syncing = false;

			if (!this.ScrollHandle.IsDragging)
				this.ScrollHandle.UpdatePosition();
		};

		this.ScrollBar.ValueChanged += value => {
			if (this.syncing)
				return;

			this.syncing = true;
			this.targetScrollBar.Value = value;
			this.syncing = false;

			if (!this.ScrollHandle.IsDragging)
				this.ScrollHandle.UpdatePosition();
		};

		this.targetScrollBar.Changed += SyncFromTarget;
		this.TargetContainer.Resized += SyncFromTarget;

		CallDeferred(nameof(SyncFromTarget));
	}

	private void SyncFromTarget() {
		syncing = true;

		this.ScrollBar.MinValue = this.targetScrollBar.MinValue;
		this.ScrollBar.MaxValue = this.targetScrollBar.MaxValue;
		this.ScrollBar.Page = this.targetScrollBar.Page;
		this.ScrollBar.Step = this.targetScrollBar.Step;
		this.ScrollBar.Value = this.targetScrollBar.Value;

		this.syncing = false;

		this.ScrollHandle.UpdatePosition();
	}

	public float GetScrollRatio() {
		float range = (float)(this.ScrollBar.MaxValue - this.ScrollBar.Page);

		if (range <= 0)
			return 0;

		return (float)this.ScrollBar.Value / range;
	}

	public float GetHandleTravel() {
		float trackLength;
		float handleLength;
		float padding;

		if (Direction == ScrollDirection.Vertical) {
			trackLength = this.ScrollBar.Size.Y;
			handleLength = this.ScrollHandle.Size.Y;
			padding = this.topPadding + this.bottomPadding;
		} else {
			trackLength = this.ScrollBar.Size.X;
			handleLength = this.ScrollHandle.Size.X;
			padding = this.leftPadding + this.rightPadding;
		}

		return Mathf.Max(0, trackLength - handleLength - padding);
	}

	public void SetHandlePosition(float position) {
		if (Direction == ScrollDirection.Vertical) {
			this.ScrollHandle.Position = new Vector2(this.ScrollHandle.Position.X, this.topPadding + position);
		} else {
			this.ScrollHandle.Position = new Vector2(this.leftPadding + position, this.ScrollHandle.Position.Y);
		}
	}

	public void SetValueFromHandle(float position) {
		float travel = this.GetHandleTravel();

		if (travel <= 0)
			return;

		float ratio = position / travel;
		float value = (float)(ratio * (this.ScrollBar.MaxValue - this.ScrollBar.Page));

		this.ScrollBar.Value = value;
	}

	private void SetupArrowScrolling() {
		this.arrowScrollTimer = new Timer {
			WaitTime = this.ArrowScrollInterval,
			OneShot = false
		};

		this.AddChild(this.arrowScrollTimer);
		this.arrowScrollTimer.Timeout += ScrollArrowRepeat;
		this.ScrollBar.GuiInput += OnScrollBarInput;
	}

	private void OnScrollBarInput(InputEvent @event) {
		if (@event is not InputEventMouseButton mouse || mouse.ButtonIndex != MouseButton.Left)
			return;

		if (mouse.Pressed) {
			this.lastPressedValue = this.targetScrollBar.Value;
			CallDeferred(nameof(DetermineArrowDirection));
		} else {
			this.StopArrowScrolling();
		}
	}

	private void DetermineArrowDirection() {
		if (targetScrollBar.Value > this.lastPressedValue)
			this.arrowScrollDirection = ArrowScrollDirection.Increment;
		else if (this.targetScrollBar.Value < this.lastPressedValue)
			this.arrowScrollDirection = ArrowScrollDirection.Decrement;
		else
			this.arrowScrollDirection = ArrowScrollDirection.None;

		if (this.arrowScrollDirection == ArrowScrollDirection.None)
			return;

		this.arrowScrollTimer.WaitTime = this.ArrowScrollInitialWaitTime;
		this.arrowScrollTimer.OneShot = true;
		this.arrowScrollTimer.Start();
	}

	private void StartArrowRepeat() {
		this.arrowScrollTimer.WaitTime = this.ArrowScrollInterval;
		this.arrowScrollTimer.OneShot = false;

		this.arrowScrollTimer.Start();
	}

	private void ScrollArrowRepeat() {
		if (this.arrowScrollDirection == 0)
			return;

		// switch from initial delay to repeat interval
		this.arrowScrollTimer.WaitTime = this.ArrowScrollInterval;
		this.arrowScrollTimer.OneShot = false;
		this.arrowScrollTimer.Start();

		this.targetScrollBar.Value += (int)this.arrowScrollDirection * this.targetScrollBar.Step;
	}

	private void ScrollArrowOnce() {
		this.targetScrollBar.Value += (int)this.arrowScrollDirection * this.targetScrollBar.Step;
	}

	private void StopArrowScrolling() {
		this.arrowScrollTimer.Stop();
		this.arrowScrollDirection = 0;
	}

	private void ApplyTheme() {
		string size = this.SizePreset.ToString().ToLower();
		string direction = this.Direction.ToString().ToLower();

		StyleBoxEmpty empty = new();

		if (this.ScrollBar is ICiv3Range scrl) {
			scrl.rangeTheme
				.AddScrollStyleBox(new StyleBoxTexture {
					Texture = TextureLoader.Load($"ui.scrollbar.scroll.{direction}.{size}"),
					AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Tile,
					AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Tile
				})
				.AddIncrement(TextureLoader.Load($"ui.scrollbar.increment.{direction}.{size}.normal"))
				.AddIncrementHighlight(TextureLoader.Load($"ui.scrollbar.increment.{direction}.{size}.hover"))
				.AddIncrementPressed(TextureLoader.Load($"ui.scrollbar.increment.{direction}.{size}.pressed"))
				.AddDecrement(TextureLoader.Load($"ui.scrollbar.decrement.{direction}.{size}.normal"))
				.AddDecrementHighlight(TextureLoader.Load($"ui.scrollbar.decrement.{direction}.{size}.hover"))
				.AddDecrementPressed(TextureLoader.Load($"ui.scrollbar.decrement.{direction}.{size}.pressed"))
				.AddGrabberStyleBox(empty)
				.AddGrabberHighlightStyleBox(empty)
				.AddGrabberPressedStyleBox(empty);

			if (Direction == ScrollDirection.Vertical) {
				this.topPadding = scrl.rangeTheme.Decrement.GetHeight();
				this.bottomPadding = scrl.rangeTheme.Increment.GetHeight();
			} else {
				this.leftPadding = scrl.rangeTheme.Decrement.GetWidth();
				this.rightPadding = scrl.rangeTheme.Increment.GetWidth();
			}
		}

		Civ3RangeTheme targetScrollBarTheme = new Civ3RangeTheme(this.targetScrollBar);

		targetScrollBarTheme.AddScrollStyleBox(empty)
			.AddGrabberStyleBox(empty)
			.AddGrabberHighlightStyleBox(empty)
			.AddGrabberPressedStyleBox(empty);
	}
}
