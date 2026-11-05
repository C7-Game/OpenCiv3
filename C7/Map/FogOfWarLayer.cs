using C7GameData;
using Godot;

namespace C7.Map;

public class FogOfWarLayer : LooseLayer {

	private readonly ImageTexture fogOfWarTexture;
	private readonly Vector2 tileSize;

	public FogOfWarLayer() {
		fogOfWarTexture = TextureLoader.Load("terrain.fog_of_war");
		tileSize = fogOfWarTexture.GetSize() / 9;
	}

	public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {

		// Fog of war is computed from the squares' intersections.
		// Thus, for each iteration, we have to consider that we are at the north intersection of the iterated tile

		Tile north = tile.neighbors[TileDirection.NORTH];
		Tile south = tile;
		Tile east = tile.neighbors[TileDirection.NORTHEAST];
		Tile west = tile.neighbors[TileDirection.NORTHWEST];

		var tk = gameData.GetFirstHumanPlayer().tileKnowledge;
		var ti = looseView.mapView.game.tileInfo;

		(bool, bool, bool) Status(Tile tile) {
			return (tk.isTileKnown(tile), tk.isActiveTile(tile), ti?.targetTile == tile);
		}

		int column = 0;
		int row = 0;
		bool known, active, target;

		(known, active, target) = Status(north);
		column += known ? (active || target) ? 2 : 1 : 0;

		(known, active, target) = Status(west);
		column += known ? (active || target) ? 6 : 3 : 0;

		(known, active, target) = Status(east);
		row += known ? (active || target) ? 2 : 1 : 0;

		(known, active, target) = Status(south);
		row += known ? (active || target) ? 6 : 3 : 0;

		// save a few draw calls when the tile is completely visible
		if (column == 8 && row == 8) {
			return;
		}

		var fogOrigin = new Vector2(tileCenter.X - tileSize.X/2, tileCenter.Y - tileSize.Y);
		var fogRect = new Rect2(fogOrigin, tileSize);
		var spriteRect = new Rect2(column * tileSize.X, row * tileSize.Y, tileSize * 0.999f);
		looseView.DrawTextureRectRegion(fogOfWarTexture, fogRect, spriteRect);
	}
}
