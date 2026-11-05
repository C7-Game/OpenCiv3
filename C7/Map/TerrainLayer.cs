using System;
using System.Collections.Generic;
using C7GameData;
using Godot;

namespace C7.Map;

public class TerrainLayer : LooseLayer {
	public static readonly Vector2 terrainSpriteSize = new Vector2(128, 64);

	// TileToDraw stores the arguments passed to drawObject so the draws can be sorted by texture before being submitted. This significantly
	// reduces the number of draw calls Godot must generate (1483 to 312 when fully zoomed out on our test map) and modestly improves framerate
	// (by about 14% on my system).
	private class TileToDraw : IComparable<TileToDraw> {
		public Tile tile;
		public Vector2 tileCenter;

		public TileToDraw(Tile tile, Vector2 tileCenter) {
			this.tile = tile;
			this.tileCenter = tileCenter;
		}

		public int CompareTo(TileToDraw other) {
			var a= this?.tile?.ExtraInfo?.BaseTerrainFileID ?? int.MaxValue;
			var b= other?.tile?.ExtraInfo?.BaseTerrainFileID ?? int.MaxValue;
			return a.CompareTo(b);
		}
	}

	private List<TileToDraw> tilesToDraw = new List<TileToDraw>();

	public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {
		tilesToDraw.Add(new TileToDraw(tile, tileCenter));
		tilesToDraw.Add(new TileToDraw(tile.neighbors[TileDirection.SOUTH], tileCenter + new Vector2(0, 64)));
		tilesToDraw.Add(new TileToDraw(tile.neighbors[TileDirection.SOUTHWEST], tileCenter + new Vector2(-64, 32)));
		tilesToDraw.Add(new TileToDraw(tile.neighbors[TileDirection.SOUTHEAST], tileCenter + new Vector2(64, 32)));
	}

	public override void onEndDraw(LooseView looseView, GameData gameData) {

		tilesToDraw.Sort();

		foreach (TileToDraw tTD in tilesToDraw) {
			if (tTD.tile == Tile.NONE) { continue; }

			ImageTexture texture = TextureLoader.Load("terrain.base", tTD.tile, useCache: true);

			Vector2 terrainOffset = new Vector2(0, -1 * MapView.cellSize.Y);
			Vector2 position = tTD.tileCenter - (float)0.5 * terrainSpriteSize + terrainOffset;

			// Multiply size by 100.1% so avoid "seams" in the map.  See issue #106.
			// Jim's option of a whole-map texture is less hacky, but this is quicker and seems to be working well.
			Rect2 screenRect = new Rect2(position, terrainSpriteSize * 1.001f);

			looseView.DrawTextureRect(texture, screenRect, tile: false);
		}

		tilesToDraw.Clear();
	}
}
