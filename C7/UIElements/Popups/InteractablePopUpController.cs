using System;
using System.Collections.Generic;
using C7Engine;
using Godot;
using Serilog;
using static TemporaryPopup;

namespace C7.UIElements.Popups;

[GlobalClass]
[Tool]
public partial class InteractablePopUpController : Control {
	private ILogger log = LogManager.ForContext<InteractablePopUpController>();

	[Export] public MarginContainer mainContainer;
	[Export] public MarginContainer advisorIconContainer;
	[Export] public Panel panel;
	[Export] public VBoxContainer mainBgContainer;
	[Export] public TextureRect bgTextureRect;
	[Export] public TextureRect advisorTextureRect;
	[Export] public MarginContainer headerContainer;
	[Export] public MarginContainer mainTextContainer;
	[Export] public Label headerLabel;
	[Export] public RichTextLabel mainTextLabel;

	[Export] public MarginContainer lineEditContainer;
	[Export] public Label lineEditLabel;
	[Export] public LineEdit lineEdit;

	[Export] public MarginContainer buttons;
	[Export] public VBoxContainer buttonContainer;

	[Export] public MarginContainer confirmAndExitMargin;
	[Export] public HBoxContainer confirmAndExitContainer;

	[Export] public TextureButton confirm;
	[Export] public TextureButton cancel;

	private ButtonGroup buttonGroup;

	private BaseButton lastPressedButton;
	public const LayoutPreset DEFAULT_LAYOUT_PRESET = LayoutPreset.CenterTop;
	private LayoutPreset layoutPreset = DEFAULT_LAYOUT_PRESET;

	private LineEditComponent lineEditComponent;

	private float defaultOffsetTop;
	private float defaultOffsetBottom;
	private float defaultOffsetLeft;
	private float defaultOffsetRight;

	private Dictionary<(int, int), ImageTexture> backgroundCache = new Dictionary<(int, int), ImageTexture>();

	AudioManager audioManager;

	public override void _Ready() {
		base._Ready();

		this.audioManager = GetNode<AudioManager>("/root/GlobalAudioManager");

		this.defaultOffsetTop = mainContainer.OffsetTop;
		this.defaultOffsetBottom = mainContainer.OffsetBottom;
		this.defaultOffsetLeft = mainContainer.OffsetLeft;
		this.defaultOffsetRight = mainContainer.OffsetRight;

		this.CloseAndDelete();

		this.SetUpConfirmButton();
		this.SetUpCancelButton();

		this.confirm.Pressed += this.OnConfirm;
		this.cancel.Pressed += this.OnCancel;
		this.lineEdit.TextSubmitted += this.LineTextSubmitted;
	}

	// An intermediary method so that we can edit and submit text without having to grab/release focus
	private void LineTextSubmitted(string text) {
		this.OnConfirm();
	}

	public override void _UnhandledInput(InputEvent @event) {
		base._UnhandledInput(@event);
		if (this.Visible && @event is InputEventKey eventKey && eventKey.Pressed) {
			if (eventKey.Keycode == Key.Enter || eventKey.Keycode == Key.KpEnter) {
				this.OnConfirm();
			}
			if (eventKey.Keycode == Key.Up) {
				this.Cycle(false);
			}
			if (eventKey.Keycode == Key.Down) {
				this.Cycle(true);
			}
		}
	}

	public void OnShowInteractablePopUp(ParameterWrapper<InteractablePopUp> interactablePopUp) {
		this.CloseAndDelete();
		OnShowInteractablePopUpLocked(interactablePopUp.Value);
		// this.mainContainer.SetAnchorsPreset(interactablePopUp.Value.layoutPreset);
	}

	private async void OnShowInteractablePopUpLocked(InteractablePopUp value) {
		this.mainBgContainer.CustomMinimumSize = new Vector2(value.hSize, 0);
		this.headerContainer.CustomMinimumSize = new Vector2(value.hSize, 0);
		this.mainTextContainer.CustomMinimumSize = new Vector2(value.hSize, 0);
		this.panel.CustomMinimumSize = new Vector2(value.hSize, 0);
		this.bgTextureRect.CustomMinimumSize = new Vector2(value.hSize, 0);

		if (value.advisorDetails.advisor != AdvisorHead.Advisor.None) {
			this.advisorIconContainer.Show();
			TextureRect advisorHead = new();
			advisorHead.Texture = AdvisorHead.GetPopupImage(value.advisorDetails.advisor, value.advisorDetails.mood, value.contollerEraIndex);
			this.advisorTextureRect.Texture = advisorHead.Texture;
		}

		this.headerLabel.Text = value.header;

		this.mainTextContainer.Visible = true;
		this.mainTextLabel.Visible = true;
		this.mainTextLabel.Text = value.message;

		if (string.IsNullOrEmpty(this.mainTextLabel.Text)) {
			this.mainTextContainer.Visible = false;
			this.mainTextLabel.Visible = false;
		}

		if (value.lineEditComponent == null) {
			this.lineEditContainer.Visible = false;
			this.lineEditLabel.Visible = false;
			this.lineEdit.Visible = false;
		} else {
			this.lineEditComponent = value.lineEditComponent;
			this.lineEditContainer.Visible = true;
			this.lineEditLabel.Visible = true;
			this.lineEdit.Visible = true;

			this.lineEditLabel.Text = value.lineEditComponent.label;
			this.lineEdit.Text = value.lineEditComponent.placeholderText;
			this.lineEdit.SelectAll();
			this.lineEdit.GrabFocus();
		}

		if (value.buttonActions is { Count: > 0 }) {
			this.buttonContainer.Show();
			buttonGroup = new ButtonGroup();
			foreach (var buttonAction in value.buttonActions) {
				var hContainer = new HBoxContainer();
				hContainer.SetSize(this.mainBgContainer.GetSize());
				var button = AddOptionButton(hContainer, buttonAction.message,  buttonAction.pressed, buttonAction.action, buttonGroup);
				button.ButtonPressed = buttonAction.pressed;
				this.buttonContainer.AddChild(hContainer);
			}
			this.buttons.Show();
		} else {
			this.buttonContainer.Hide();
		}

		if (value.hasConfirm) {
			this.confirmAndExitMargin.Show();
			this.AddConfirmButton();
		}

		if (value.hasCancel) {
			this.confirmAndExitMargin.Show();
			this.AddCancelButton();
		}

		this.mainContainer.SetAnchorsPreset(value.layoutPreset);
		this.mainContainer.SetOffsetsPreset(value.layoutPreset, LayoutPresetMode.KeepSize);

		this.mainContainer.OffsetTop += value.margins.top;
		this.mainContainer.OffsetBottom += value.margins.bottom;
		this.mainContainer.OffsetLeft += value.margins.left;
		this.mainContainer.OffsetRight += value.margins.right;

		this.Show();

		// wait for the UI layout to settle
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		// this should always be done at the very end, so the height is correct based on the dynamic elements
		this.bgTextureRect.Texture = this.CreateTextureBackGround((int)this.mainBgContainer.GetSize().X, (int)this.mainBgContainer.GetSize().Y);
	}

	protected BaseButton AddOptionButton(Node parent, string label, bool pressed, Action action, ButtonGroup buttonGroup = null) {
		Civ3MenuButton button = new() {
			Text = label,
			FontSize = 14,
			ButtonGroup = buttonGroup,
			ButtonPressed = pressed,
			ToggleMode = true,
		};
		parent.AddChild(button);

		if (pressed)
			this.lastPressedButton = button;

		button.Pressed += () => {
			Toggle(button, action);
		};

		return button;
	}

	private void Toggle(BaseButton button, Action action) {
		if (this.lastPressedButton == button) {
			this.CloseAndDelete();
			action?.Invoke();
			new MsgUiDisengaged().send();
		} else {
			this.lastPressedButton = button;
			button.SetPressedNoSignal(true);
		}
	}

	private void Cycle(bool forward = true) {
		var btns = buttonGroup.GetButtons();
		if (btns.Count < 1) {
			return;
		}
		var buttonIndex = btns.IndexOf(lastPressedButton);
		var nextButtonIndex = buttonIndex + (forward ? 1 : -1);
		var nextButtonNormalizedIndex = Mathf.PosMod(nextButtonIndex, btns.Count);

		var nextButton = btns[nextButtonNormalizedIndex];
		nextButton.ButtonPressed = true;
		this.Toggle(nextButton, null);
	}

	private void AddConfirmButton() {
		this.confirm.Show();
	}

	private void SetUpConfirmButton() {
		ImageTexture circleTexture= TextureLoader.Load("ui.confirm.normal");
		ImageTexture circleHover = TextureLoader.Load("ui.confirm.hover");
		ImageTexture circlePressed = TextureLoader.Load("ui.confirm.pressed");
		this.confirm.TextureNormal = circleTexture;
		this.confirm.TextureHover = circleHover;
		this.confirm.TexturePressed = circlePressed;

		this.confirm.TooltipText = "OK";
		this.confirm.Theme = GetToolTipTheme();
	}

	private void OnConfirm() {
		if (this.lastPressedButton != null)
			this.lastPressedButton.EmitSignal(BaseButton.SignalName.Pressed);

		if (this.lineEditComponent != null) {
			this.lineEditComponent.callback.Invoke(this.lineEdit.Text.StripEdges());
		}

		this.audioManager.PlayUIAudio("buttons.button_ok");
		this.Hide();
		this.CloseAndDelete();
		new MsgUiDisengaged().send();
	}

	private void AddCancelButton() {
		cancel.Show();
	}

	private void SetUpCancelButton() {
		ImageTexture xTexture = TextureLoader.Load("ui.cancel.normal");
		ImageTexture xHover = TextureLoader.Load("ui.cancel.hover");
		ImageTexture xPressed = TextureLoader.Load("ui.cancel.pressed");
		this.cancel.TextureNormal = xTexture;
		this.cancel.TextureHover = xHover;
		this.cancel.TexturePressed = xPressed;

		this.cancel.TooltipText = "CANCEL";
		this.cancel.Theme = GetToolTipTheme();
	}

	private void OnCancel() {
		this.audioManager.PlayUIAudio("buttons.button_cancel");
		this.Hide();
		this.CloseAndDelete();
		new MsgUiDisengaged().send();
	}

	private void CloseAndDelete() {
		this.Hide();

		this.layoutPreset = DEFAULT_LAYOUT_PRESET;
		this.mainContainer.SetAnchorsPreset(layoutPreset);

		this.mainContainer.OffsetTop = defaultOffsetTop;
		this.mainContainer.OffsetBottom = defaultOffsetBottom;
		this.mainContainer.OffsetLeft = defaultOffsetLeft;
		this.mainContainer.OffsetRight = defaultOffsetRight;

		this.buttonContainer.Hide();
		this.lastPressedButton = null;
		this.bgTextureRect.Texture = null;
		this.advisorIconContainer.Hide();
		this.advisorTextureRect.Texture = null;
		this.lineEditContainer.Hide();

		foreach (var child in buttonContainer.GetChildren()) {
			buttonContainer.RemoveChild(child);
			child.QueueFree();
		}
		this.confirmAndExitMargin.Hide();
		this.cancel.Hide();
		this.confirm.Hide();
	}

	public override void _ExitTree() {
		base._ExitTree();
		this.confirm.Pressed -= this.OnConfirm;
		this.cancel.Pressed -= this.OnCancel;
	}

	const int HTILE_SIZE = 61;
	const int VTILE_SIZE = 44;

	protected ImageTexture CreateTextureBackGround(int width, int height) {
		if (this.backgroundCache.ContainsKey((width, height))) {
			return this.backgroundCache[(width, height)];
		}

		Image image = Image.Create(width, height, false, Image.Format.Rgba8);

		//The pop-up part is the tricky part
		Image topLeftPopup = TextureLoader.Load("popup_background.top_left").GetImage();
		Image topCenterPopup = TextureLoader.Load("popup_background.top_center").GetImage();
		Image topRightPopup = TextureLoader.Load("popup_background.top_right").GetImage();
		Image middleLeftPopup = TextureLoader.Load("popup_background.middle_left").GetImage();
		Image middleCenterPopup = TextureLoader.Load("popup_background.middle_center").GetImage();
		Image middleRightPopup = TextureLoader.Load("popup_background.middle_right").GetImage();
		Image bottomLeftPopup = TextureLoader.Load("popup_background.bottom_left").GetImage();
		Image bottomCenterPopup = TextureLoader.Load("popup_background.bottom_center").GetImage();
		Image bottomRightPopup = TextureLoader.Load("popup_background.bottom_right").GetImage();

		//Dimensions are 530x320.  The leaderhead takes up 110.  So the popup is 530x210.
		//We have multiples of... 62? For the horizontal dimension, 45 for vertical.
		//45 does not fit into 210.  90, 135, 180, 215.  Well, 215 is sorta closeish.
		//62, we got 62, 124, 248, 496, 558.  Doesn't match up at all.
		//Which means that partial textures can be used.  Lovely.

		//Let's try adding some helper functions so this can be refactored later into a more general-purpose popup popper
		int vOffset = 0;
		DrawRow(image, vOffset, width, topLeftPopup, topCenterPopup, topRightPopup);
		vOffset += VTILE_SIZE;
		for (; vOffset < height - VTILE_SIZE; vOffset += VTILE_SIZE) {
			DrawRow(image, vOffset, width, middleLeftPopup, middleCenterPopup, middleRightPopup);
		}
		vOffset = height - VTILE_SIZE;
		DrawRow(image, vOffset, width, bottomLeftPopup, bottomCenterPopup, bottomRightPopup);

		ImageTexture texture = ImageTexture.CreateFromImage(image);
		this.backgroundCache.Add((width, height), texture);

		return texture;
	}

	private void DrawRow(Image image, int vOffset, int width, Image left, Image center, Image right) {

		image.BlitRect(left, new Rect2I(new Vector2I(0, 0), new Vector2I(left.GetWidth(), left.GetHeight())), new Vector2I(0, vOffset));

		int leftOffset = HTILE_SIZE;
		for (; leftOffset < width - HTILE_SIZE; leftOffset += HTILE_SIZE) {
			image.BlitRect(center, new Rect2I(new Vector2I(0, 0), new Vector2I(center.GetWidth(), center.GetHeight())), new Vector2I(leftOffset, vOffset));
		}

		leftOffset = width - HTILE_SIZE;
		image.BlitRect(right, new Rect2I(new Vector2I(0, 0), new Vector2I(right.GetWidth(), right.GetHeight())), new Vector2I(leftOffset, vOffset));
	}
}
