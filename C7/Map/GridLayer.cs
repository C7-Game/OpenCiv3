using C7GameData;
using Godot;

namespace C7.Map;

public class GridLayer : LooseLayer {
	public Color color = Color.Color8(50, 50, 50, 150);
	public float lineWidth = (float)1.0;

	public GridLayer() { }

	public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {
		Vector2 cS = MapView.cellSize;
		Vector2 left = tileCenter + new Vector2(-cS.X, 0);
		Vector2 top = tileCenter + new Vector2(0, -cS.Y);
		Vector2 right = tileCenter + new Vector2(cS.X, 0);
		looseView.DrawLine(left, top, color, lineWidth);
		looseView.DrawLine(top, right, color, lineWidth);

		if (gameData.observerMode && gameData.showGridCoordinates) {
			looseView.DrawString(ThemeDB.FallbackFont,
				tileCenter + new Vector2(-16, 0), tile.XCoordinate + "," + tile.YCoordinate,
				HorizontalAlignment.Center, -1, 24, Color.Color8(255, 0, 0));
		}
	}
}
