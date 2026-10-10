using Godot;
using System;

// The standard civ3 checkbox with text attached.
[GlobalClass]
[Tool]
public partial class Civ3Checkbox : CheckBox {
	public enum TextPosition {
		TextLeftOfIcon,
		TextRightOfIcon,
		TextAboveIcon,
		TextBelowIcon
	}

	private Texture2D normalTexture;
	private Texture2D hoverTexture;
	private Texture2D pressedTexture;

	public Civ3Checkbox() {
	}

	private string _text;
	[Export]
	public string Text {
		get => _text;
		set {
			_text = value;
			if (label != null) {
				label.Text = _text;
				RefreshMinimumSize();
			}
		}
	}
	private int _fontSize;
	[Export]
	public int FontSize {
		get => _fontSize;
		set {
			_fontSize = value;
			if (label != null) {
				label.AddThemeFontSizeOverride("font_size", _fontSize);
				RefreshMinimumSize();
			}
		}
	}
	private TextPosition _textPosition = TextPosition.TextRightOfIcon;
	[Export]
	public TextPosition textPosition {
		get => _textPosition;
		set {
			_textPosition = value;
			if (label != null && textureRect != null) {
				SetUpLayout();
			}
		}
	}

	private Color fontColor;
	private Color hoverColor;
	private Color pressedColor;

	private Label label;
	private TextureRect textureRect;
	private BoxContainer boxContainer;

	private bool hovered = false;

	public Civ3Checkbox(TextPosition textPosition = TextPosition.TextRightOfIcon) {
		this.textPosition = textPosition;

		Flat = true;
		FontSize = 12;
		SizeFlagsHorizontal = SizeFlags.Expand;
		SizeFlagsVertical = SizeFlags.ShrinkCenter;
	}

	public override void _Ready() {
		normalTexture = TextureLoader.Load("ui.checkbox.inactive");
		hoverTexture = TextureLoader.Load("ui.checkbox.hover");
		pressedTexture = TextureLoader.Load("ui.checkbox.pressed");

		fontColor = GetThemeColor("font_color", "Button");
		hoverColor = GetThemeColor("font_hover_color", "Button");
		pressedColor = GetThemeColor("font_pressed_color", "Button");

		// Set up the label and texture.
		label = new() {
			Text = Text,
			MouseFilter = MouseFilterEnum.Pass,
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		label.AddThemeFontSizeOverride("font_size", FontSize);

		textureRect = new() {
			Texture = normalTexture,
			MouseFilter = MouseFilterEnum.Pass,
			CustomMinimumSize = new Vector2(normalTexture.GetWidth(), normalTexture.GetHeight()),
			ExpandMode = TextureRect.ExpandModeEnum.KeepSize,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};

		SetUpLayout();

		// Hook up our code to the button signals.
		MouseEntered += () => {
			hovered = true;
			UpdateVisuals();
		};
		MouseExited += () => {
			hovered = false;
			UpdateVisuals();
		};
		Pressed += UpdateVisuals;
		Toggled += (bool toggledOn) => { UpdateVisuals(); };

		UpdateVisuals();
	}

	private void SetUpLayout() {
		if (boxContainer != null) {
			boxContainer.RemoveChild(label);
			boxContainer.RemoveChild(textureRect);
			RemoveChild(boxContainer);
			boxContainer.QueueFree();
		}

		// Depending on the text positioning, add the label and texture as children
		// of the appropriate layout.
		if (textPosition == TextPosition.TextAboveIcon || textPosition == TextPosition.TextBelowIcon) {
			boxContainer = new VBoxContainer();
		} else {
			boxContainer = new HBoxContainer() {
				Alignment = (textPosition == TextPosition.TextLeftOfIcon) ? BoxContainer.AlignmentMode.End : BoxContainer.AlignmentMode.Begin,
			};
		}
		boxContainer.SetAnchorsPreset(LayoutPreset.FullRect);
		boxContainer.MouseFilter = MouseFilterEnum.Pass;
		AddChild(boxContainer);

		if (textPosition == TextPosition.TextAboveIcon || textPosition == TextPosition.TextLeftOfIcon) {
			boxContainer.AddChild(label);
			boxContainer.AddChild(textureRect);
		} else {
			boxContainer.AddChild(textureRect);
			boxContainer.AddChild(label);
		}

		RefreshMinimumSize();
	}

	// Grow the button to cover the icon and the label so the whole thing is
	// clickable. Godot caches the bare CheckBox size, so it has to be set
	// explicitly whenever the contents change.
	private void RefreshMinimumSize() {
		if (boxContainer == null) {
			return;
		}
		CustomMinimumSize = boxContainer.GetCombinedMinimumSize();
	}

	// Expand the size of the button to contain the texture and the label.
	public override Vector2 _GetMinimumSize() {
		if (boxContainer == null) {
			return base._GetMinimumSize();
		}
		return boxContainer.GetCombinedMinimumSize();
	}

	public void UpdateVisuals() {
		Texture2D currentTexture = normalTexture;
		Color currentTextColor = fontColor;

		// ButtonPressed is true if ToggleMode is on and button is selected
		bool isEffectivelyPressed = (ToggleMode && ButtonPressed);

		if (isEffectivelyPressed) {
			currentTexture = pressedTexture;
			currentTextColor = pressedColor;
		} else if (hovered) {
			currentTexture = hoverTexture;
			currentTextColor = hoverColor;
		}

		textureRect.Texture = currentTexture;
		label.AddThemeColorOverride("font_color", currentTextColor);
	}
}
