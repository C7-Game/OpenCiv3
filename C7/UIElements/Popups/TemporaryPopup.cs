using Godot;
using System.Threading.Tasks;

public partial class TemporaryPopup : Label {
	private int durationInMillis;

	public TemporaryPopup(string text, float durationInSec) {
		ZIndex = 3;
		Text = text;
		this.durationInMillis = (int)(durationInSec * 1000);

		AddThemeStyleboxOverride("normal", PopupStyleBox());
		AddThemeColorOverride("font_color", Colors.White);
		AddThemeFontSizeOverride("font_size", 12);
	}

	// A stylebox that works well for temporary popups or tooltips in game.
	public static StyleBoxFlat TooltipStyleBox() {
		StyleBoxFlat styleBox = new();
		styleBox.ContentMarginLeft = 5;
		styleBox.ContentMarginRight = 5;
		styleBox.ContentMarginTop = 2;
		styleBox.ContentMarginBottom = 2;
		styleBox.BgColor = Color.FromHtml("1C1C1C");
		return styleBox;
	}

	public static StyleBoxFlat PopupStyleBox() {
		StyleBoxFlat styleBox = new();
		styleBox.ContentMarginLeft = 5;
		styleBox.ContentMarginRight = 5;
		styleBox.ContentMarginTop = 2;
		styleBox.ContentMarginBottom = 2;
		styleBox.BgColor = Color.FromHtml("1C1C1C99");
		return styleBox;
	}

	public static StyleBoxFlat PopupTechStyleBox() {
		StyleBoxFlat styleBox = new();
		styleBox.ContentMarginLeft = 5;
		styleBox.ContentMarginRight = 5;
		styleBox.ContentMarginTop = 2;
		styleBox.ContentMarginBottom = 2;
		styleBox.BorderColor = Colors.Black;
		styleBox.BorderWidthBottom = 1;
		styleBox.BorderWidthTop = 1;
		styleBox.BorderWidthLeft = 1;
		styleBox.BorderWidthRight = 1;
		styleBox.BgColor = Color.FromHtml("F8F8F8");
		return styleBox;
	}

	public static Theme GetToolTipTheme() {
		var customTheme = new Theme();
		customTheme.SetStylebox("panel", "TooltipPanel", PopupTechStyleBox());
		customTheme.SetColor("font_color", "TooltipLabel", Colors.Black);
		return customTheme;
	}

	public static void Show(Node parent, string msg, Vector2 rootPosition) {
		TemporaryPopup popup = new(msg, 2);
		popup.SetPosition(rootPosition);
		parent.AddChild(popup);

		// Center the text, adjust it slightly above the target (tile) position.
		// Note: Label size isn't defined until node has been added to the tree.
		Vector2 offset = new(-(popup.Size.X / 2), -64);
		popup.SetPosition(rootPosition + offset);

		popup.ShowPopup();
	}

	public async void ShowPopup() {
		// Wait until we hit our duration then destroy ourself.
		await Task.Delay(durationInMillis);

		Visible = false;
		QueueFree();
	}
}
