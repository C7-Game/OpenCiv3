using Godot;
using ConvertCiv3Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Serilog;

public partial class Margins {
	public Margins(float top = 0, float bottom = 0, float left = 0, float right = 0) {
		this.top = top;
		this.bottom = bottom;
		this.left = left;
		this.right = right;
	}

	public float top;
	public float bottom;
	public float left;
	public float right;
}

/*
 * Popup displays a popup menu or dialog. Margins alignment determines where the popup appears on the screen,
 * and margins can be used to add spacing around the popup. The AddBackground method will add the background
 * texture to the popup, which will be rendered at the position determined by the alignment and margins specified.
 * Additionally, AddBackground accepts an int vOffset parameter which will render the background texture vOffset
 * pixels lower than it would otherwise. This is used when a popup includes an advisor, since the vOffset adds a
 * transparent region above the top of the popup background in which the advisor texture can be drawn.
 */
public partial class Popup : TextureRect {
	private ILogger log = LogManager.ForContext<Popup>();

	public BoxContainer.AlignmentMode alignment;
	public Margins margins;

	const int HTILE_SIZE = 61;
	const int VTILE_SIZE = 44;

	private static Dictionary<(int, int), ImageTexture> backgroundCache = new Dictionary<(int, int), ImageTexture>();

	protected void AddButton(string label, int verticalPosition, Action action) {
		const int HORIZONTAL_POSITION = 30;

		Civ3MenuButton button = new() {
			Text = label,
			FontSize = 14,
		};
		button.SetPosition(new Vector2(HORIZONTAL_POSITION, verticalPosition));
		AddChild(button);
		button.Pressed += () => {
			action();
			ReleaseFocus();
		};
	}

	protected void AddHeader(string text, int vOffset) {
		HBoxContainer header = new HBoxContainer();
		header.Alignment = BoxContainer.AlignmentMode.Center;
		Label advisorType = new Label();

		//Set the font size.  For labels, there is no one-off override, so we have to
		//set it on a theme like this.
		//The SetFont arguments aren't documented in a way that a non-Godot expert can understand
		//My current understanding is that we need to set the first parameter to "font", and the
		//second to whatever type it should apply to.  But that is based on nothing official.
		//Also you can set the size with bigFont.Size = 72, but that applies everywhere the font
		//is used in the whole program.  Not recommended.
		FontFile bigFont = ResourceLoader.Load<FontFile>("res://Fonts/NSansFont24Pt.tres");
		Theme theme = new Theme();
		theme.SetFont("font", "Label", bigFont);
		theme.SetFontSize("font_size", "Label", 24);
		advisorType.Theme = theme;
		advisorType.Text = text;
		header.AddChild(advisorType);
		header.SetPosition(new Vector2(0, vOffset));
		header.AnchorLeft = 0.0f;
		header.AnchorRight = 1.0f;
		header.OffsetRight = 10;    // For some reason this isn't causing it to be indented 10 pixels from the right.
		AddChild(header);
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

	protected void AddTexture(int width, int height) {
		Image image = Image.Create(width, height, false, Image.Format.Rgba8);
		image.Fill(Color.Color8(0, 0, 0, 0));
		this.Texture = ImageTexture.CreateFromImage(image);
	}

	protected void AddBackground(int width, int height, int vOffset = 0) {
		TextureRect background = CreateBackground(width, height);
		background.SetPosition(new Vector2(0, vOffset));
		AddChild(background);
	}

	private TextureRect CreateBackground(int width, int height) {
		TextureRect rect = new TextureRect();

		if (backgroundCache.ContainsKey((width, height))) {
			rect.Texture = backgroundCache[(width, height)];
			return rect;
		}

		Image image = Image.Create(width, height, false, Image.Format.Rgba8);

		//The pop-up part is the tricky part
		Stopwatch imageTimer = new Stopwatch();
		imageTimer.Start();
		Image topLeftPopup = TextureLoader.Load("popup_background.top_left").GetImage();
		Image topCenterPopup = TextureLoader.Load("popup_background.top_center").GetImage();
		Image topRightPopup = TextureLoader.Load("popup_background.top_right").GetImage();
		Image middleLeftPopup = TextureLoader.Load("popup_background.middle_left").GetImage();
		Image middleCenterPopup = TextureLoader.Load("popup_background.middle_center").GetImage();
		Image middleRightPopup = TextureLoader.Load("popup_background.middle_right").GetImage();
		Image bottomLeftPopup = TextureLoader.Load("popup_background.bottom_left").GetImage();
		Image bottomCenterPopup = TextureLoader.Load("popup_background.bottom_center").GetImage();
		Image bottomRightPopup = TextureLoader.Load("popup_background.bottom_right").GetImage();
		imageTimer.Stop();
		TimeSpan stopwatchElapsed = imageTimer.Elapsed;
		log.Debug("Image creation time: " + Convert.ToInt32(stopwatchElapsed.TotalMilliseconds) + " ms");

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
		backgroundCache.Add((width, height), texture);

		rect.Texture = texture;
		return rect;
	}

	protected void AddConfirmButton(Vector2 position, Action action) {
		ImageTexture circleTexture= TextureLoader.Load("ui.confirm.normal");
		ImageTexture circleHover = TextureLoader.Load("ui.confirm.hover");
		ImageTexture circlePressed = TextureLoader.Load("ui.confirm.pressed");
		TextureButton confirmButton = new TextureButton();
		confirmButton.TextureNormal = circleTexture;
		confirmButton.TextureHover = circleHover;
		confirmButton.TexturePressed = circlePressed;
		confirmButton.SetPosition(position);
		confirmButton.Pressed += action;
		AddChild(confirmButton);
	}

	protected void AddCancelButton(Vector2 position) {
		ImageTexture xTexture = TextureLoader.Load("ui.cancel.normal");
		ImageTexture xHover = TextureLoader.Load("ui.cancel.hover");
		ImageTexture xPressed = TextureLoader.Load("ui.cancel.pressed");
		TextureButton cancelButton = new TextureButton();
		cancelButton.TextureNormal = xTexture;
		cancelButton.TextureHover = xHover;
		cancelButton.TexturePressed = xPressed;
		cancelButton.SetPosition(position);
		cancelButton.Pressed += GetParent<PopupOverlay>().OnHidePopup;
		AddChild(cancelButton);
	}
}
