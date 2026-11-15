using System.Collections.Generic;
using System.Linq;
using C7GameData;
using Godot;

namespace C7.Map;
// The layer responsible for drawing the cursor and tile effects relating to bombardment.
public class BombardLayer : LooseLayer {
	private readonly ImageTexture bombardCursorTexture;
	private readonly ImageTexture bombardDenyCursorTexture;

	private TextureRect bombardCursorRect = null;
	private TextureRect bombardDenyCursorRect = null;

	private Color bombardRed = Color.Color8(200, 0, 0, 225);
	private float bombardGridLineWidth = (float)1.0;

	private Dictionary<string, List<Tile>> tileSquareCache = new();

	public BombardLayer() {
		bombardCursorTexture = TextureLoader.Load("ui.cursor.bombard");
		bombardDenyCursorTexture = TextureLoader.Load("ui.cursor.bombard_deny");
	}

	public void DrawBombardCursor() {
		Input.SetCustomMouseCursor(bombardCursorTexture, hotspot: bombardCursorTexture.Center());
	}
	public void DrawBombardDenyCursor() {
		Input.SetCustomMouseCursor(bombardDenyCursorTexture, hotspot: bombardDenyCursorTexture.Center());
	}

	public override void onBeginDraw(LooseView looseView, GameData gameData) {
		bombardCursorRect?.Hide();
		bombardDenyCursorRect?.Hide();
	}

	public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {
		var bombardInfo = looseView.mapView.game.bombardInfo;
		if (bombardInfo == null || bombardInfo.bombardingUnit.location != tile)
			return;

		var unit = bombardInfo.bombardingUnit;
		var range = unit.unitType.bombardRange;
		var reachableTiles = GetTileSquare(tile, range);
		var targetTiles = reachableTiles.Where(t => unit.CanBombardTile(t));
		var bombardTiles = targetTiles.ToHashSet();

		// Choose one of two cursors depending on mouse tile hover
		if (bombardInfo.mouseTile != null) {
			var bombardable = bombardTiles.Contains(bombardInfo.mouseTile);
			if (bombardable) {
				DrawBombardCursor();
				drawTargetBombardTile(looseView, TileCenter(bombardInfo.mouseTile));
			} else
				DrawBombardDenyCursor();
		}

		// Draw bombard grid
		foreach (var bt in reachableTiles) {
			drawBombardTile(looseView, TileCenter(bt));
		}
	}

	private List<Tile> GetTileSquare(Tile tile, int range) {
		var key = $"{tile.Id}_{range}";
		if (tileSquareCache.TryGetValue(key, out var square))
			return square;
		tileSquareCache[key] = tile.GetTilesWithinTileSquare(range);
		return tileSquareCache[key];
	}

	private static Vector2 TileCenter(Tile bt) {
		return MapView.cellSize * new Vector2(bt.XCoordinate + 1, bt.YCoordinate + 1);
	}

	private void drawBombardTile(LooseView looseView, Vector2 tileCenter) {
		var cS = MapView.cellSize;
		var left = tileCenter + new Vector2(-cS.X, 0);
		var top = tileCenter + new Vector2(0, -cS.Y);
		var right = tileCenter + new Vector2(cS.X, 0);
		var bottom = tileCenter + new Vector2(0, cS.Y);
		DrawSquare(looseView, left, top, right, bottom);
	}

	private void drawTargetBombardTile(LooseView looseView, Vector2 tileCenter) {
		var cS = MapView.cellSize;
		var inset = 10;
		var left = tileCenter + new Vector2(-cS.X + inset, 0);
		var top = tileCenter + new Vector2(0, -cS.Y + (inset/2f));
		var right = tileCenter + new Vector2(cS.X - inset, 0);
		var bottom = tileCenter + new Vector2(0, cS.Y - (inset/2f));
		DrawSquare(looseView, left, top, right, bottom);
	}

	private void DrawSquare(LooseView looseView, Vector2 left, Vector2 top, Vector2 right, Vector2 bottom) {
		looseView.DrawLine(left, top, bombardRed, bombardGridLineWidth);
		looseView.DrawLine(top, right, bombardRed, bombardGridLineWidth);
		looseView.DrawLine(left, bottom, bombardRed, bombardGridLineWidth);
		looseView.DrawLine(bottom, right, bombardRed, bombardGridLineWidth);
	}
}
