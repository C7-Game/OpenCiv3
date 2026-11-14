using System.Linq;
using C7GameData;
using Godot;
using static C7GameData.TileRandom;

namespace C7.Map;

public class MarshLayer : LooseLayer {
	public static readonly Vector2 marshSize = new Vector2(128, 88);
	//Because the marsh graphics are 88 pixels tall instead of the 64 of a tile, we also need an addition 12 pixel offset to the top
	//88 - 64 = 24; 24/2 = 12.  This keeps the marsh centered with half the extra 24 pixels above the tile and half below.
	readonly Vector2 MARSH_OFFSET = (float)0.5 * marshSize;// + new Vector2(0, -12);

	private ImageTexture largeMarshTexture;
	private ImageTexture smallMarshTexture;

	public MarshLayer() {
		largeMarshTexture = TextureLoader.Load("terrain.marsh.large");
		smallMarshTexture = TextureLoader.Load("terrain.marsh.small");
	}

	public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {
		bool renderLarge = tile.GetEdgeNeighbors().Count(t => t.IsFlatLand()) > 3;

		if (tile.HasMarsh()) {
			int marshRow = 0;
			int marshColumn;
			ImageTexture marshTexture = largeMarshTexture;

			if (renderLarge) {
				marshColumn = GetRandomInt(tile.Id, gameSeed, 8, 0x332);
				if (marshColumn > 3) {
					marshColumn %= 4;
					marshRow++;
				}
			} else {
				marshColumn = GetRandomInt(tile.Id, gameSeed, 18, 0xb52);
				if (marshColumn < 4) {
					marshColumn %= 4;
				} else if (marshColumn < 8) {
					marshColumn %= 4;
					marshRow += 1;
				} else if (marshColumn < 13) {
					marshTexture = smallMarshTexture;
					marshColumn %= 5;
				} else {
					marshTexture = smallMarshTexture;
					marshColumn %= 5;
					marshRow += 1;
				}
			}

			Rect2 jungleRectangle = new Rect2(marshColumn * marshSize.X, marshRow * marshSize.Y, marshSize);
			Rect2 screenTarget = new Rect2(tileCenter - MARSH_OFFSET, marshSize);
			looseView.DrawTextureRectRegion(marshTexture, screenTarget, jungleRectangle);
		}
	}
}
