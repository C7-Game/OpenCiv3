using Godot;
using static C7.UIElements.Civ3ScrollBarControl;

namespace C7.UIElements;

[GlobalClass]
[Tool]
public partial class Civ3ScrollHandle : Civ3TextureButton {
	[Export] public bool UseTextureSize { get; set; } = true;

	private Civ3ScrollBarControl owner;
	public bool IsDragging => dragging;
	private bool dragging;
	// private Vector2 dragOffset;
	private double savedStep;

	public override void _Ready() {
		// This means that when scrolling using the mouse wheel,
		// if the mouse happens to come over the grabber texture,
		// it will not scroll!
		this.MouseFilter = MouseFilterEnum.Stop;
		this.ZIndex = 10;
	}

	public void Initialize(Civ3ScrollBarControl scrollBarControl, ScrollBarSize scrollSize) {
		this.owner = scrollBarControl;

		this.savedStep = this.owner.targetScrollBar.Step;

		ApplyTexture(scrollSize);

		UpdatePosition();

		ButtonDown += StartDrag;
	}

	private void ApplyTexture(ScrollBarSize scrollSize) {
		var grabberTexture = TextureLoader.Load($"ui.scrollbar.grabber.{scrollSize.ToString().ToLower()}");

		this.TextureNormal = grabberTexture;

		if (this.UseTextureSize)
			this.Size = grabberTexture.GetSize();
	}

	public void UpdatePosition() {
		// if (owner == null)
		// 	return;

		float position = owner.GetHandleTravel() * owner.GetScrollRatio();

		this.owner.SetHandlePosition(position);
	}

	public override void _Input(InputEvent @event) {
		if (@event is InputEventMouseButton mouseButton) {
			if (mouseButton.ButtonIndex == MouseButton.Left && !mouseButton.Pressed) {
				StopDrag();
			}
		}

		if (@event is InputEventMouseMotion && this.dragging)
			Drag();
	}

	private void StartDrag() {
		if (!this.owner.Step) {
			this.savedStep = owner.targetScrollBar.Step;
			this.owner.targetScrollBar.Step = 0;
		}

		this.dragging = true;
	}

	private void StopDrag() {
		if (!this.owner.Step) {
			this.owner.targetScrollBar.Step = this.savedStep;
		}
		this.dragging = false;
	}

	private void Drag() {
		Vector2 mouse = GetParent<Control>().GetGlobalMousePosition();

		float position;

		if (this.owner.Direction == ScrollDirection.Vertical)
			position = mouse.Y - this.owner.ScrollBar.GlobalPosition.Y - this.owner.topPadding;
		else
			position = mouse.X - this.owner.ScrollBar.GlobalPosition.X - this.owner.leftPadding;

		position = Mathf.Clamp(position, 0, this.owner.GetHandleTravel());

		this.owner.SetHandlePosition(position);
		this.owner.SetValueFromHandle(position);
	}
}
