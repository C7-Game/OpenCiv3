using System.Collections.Generic;
using System.Linq;
using Godot;
using System.Threading.Tasks;
using C7GameData;

public partial class TemporaryPopup : Label {
	private int durationInMillis;
	private Tile initialLocation;
	private MapView mapView;

	public override void _Process(double delta) {
		base._Process(delta);
		if (!this.Visible) return;

		var posInQueue = 0;
		if (Game.tempPopupQueue.TryGetValue(initialLocation, out var queue)) {
			posInQueue = queue.ToList().IndexOf(this);
		}

		var popupMargin = 50 * mapView.cameraZoom;
		Vector2 offset = new(-(this.Size.X / 2), -(32 * mapView.cameraZoom) - this.Size.Y -(posInQueue * popupMargin));
		Vector2 screenLocation = mapView.screenLocationOfTileAbsolute(initialLocation);
		this.SetPosition(screenLocation + offset);
	}

	public TemporaryPopup(MapView mapView, Tile initialLocation, string text, float durationInSec) {
		ZIndex = 3;
		Text = text;
		this.durationInMillis = (int)(durationInSec * 1000);
		this.mapView = mapView;
		this.initialLocation = initialLocation;

		AddThemeStyleboxOverride("normal", TemporaryPopupStyleBox());
		AddThemeColorOverride("font_color", Colors.White);
		AddThemeFontSizeOverride("font_size", 12);
	}

	public static StyleBoxFlat TemporaryPopupStyleBox() {
		StyleBoxFlat styleBox = new();
		styleBox.ContentMarginLeft = 2;
		styleBox.ContentMarginRight = 2;
		styleBox.ContentMarginTop = 1;
		styleBox.ContentMarginBottom = 1;
		styleBox.BgColor = Color.FromHtml("1C1C1C70");
		return styleBox;
	}

	public static StyleBoxFlat InfoStyleBox() {
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
		customTheme.SetStylebox("panel", "TooltipPanel", InfoStyleBox());
		customTheme.SetColor("font_color", "TooltipLabel", Colors.Black);
		return customTheme;
	}

	public static void Show(MapView mapView, Node parent, string msg, Tile rootPosition) {
		TemporaryPopup popup = new(mapView, rootPosition, msg, 2);
		parent.AddChild(popup);

		if (Game.tempPopupQueue.TryGetValue(rootPosition, out Queue<TemporaryPopup> queue)) {
			queue.Enqueue(popup);
		} else {
			var newQueue = new Queue<TemporaryPopup>();
			newQueue.Enqueue(popup);
			Game.tempPopupQueue.TryAdd(rootPosition, newQueue);
		}

		popup.ShowPopup();
	}

	public async void ShowPopup() {
		// Wait until we hit our duration then destroy ourself.
		await Task.Delay(durationInMillis);

		Visible = false;
		if (Game.tempPopupQueue.TryGetValue(initialLocation, out Queue<TemporaryPopup> queue)) {
			queue.TryDequeue(out _);
		}
		QueueFree();
	}
}
