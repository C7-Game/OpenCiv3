using Godot;
using System;

// The standard civ3 menu button with the circle icon and text attached.
//
// This is its own class to allow use in the editor and to allow reuse.
[GlobalClass]
[Tool]
public partial class Civ3MenuButton : BaseButton {
	public enum TextPosition {
		TextLeftOfIcon,
		TextRightOfIcon,
		TextAboveIcon,
		TextBelowIcon
	}

	// These are loaded in _Ready rather than in the field initializers: Godot
	// instantiates nodes with this script in contexts that don't have the
	// texture config set up (exporting a scene, for one), and throwing from the
	// constructor makes such a scene silently lose the script entirely.
	private Texture2D normalTexture;
	private Texture2D hoverTexture;
	private Texture2D pressedTexture;

	private string _text;
	[Export]
	public string Text {
		get => _text;
		set {
			_text = value;
			if (label != null) {
				label.Text = _text;
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
			}
		}
	}
	private TextPosition _textPosition;
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

	public Civ3MenuButton() {
		textPosition = TextPosition.TextRightOfIcon;
	}

	public Civ3MenuButton(TextPosition textPosition = TextPosition.TextRightOfIcon) {
		this.textPosition = textPosition;
	}

	public override void _Ready() {
		normalTexture = TextureLoader.Load("ui.button.inactive");
		hoverTexture = TextureLoader.Load("ui.button.hover");
		pressedTexture = TextureLoader.Load("ui.button.pressed");

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
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
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
