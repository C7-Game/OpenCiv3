using C7GameData;
using Godot;


namespace C7.Map;

public class RiverLayer : LooseLayer {
	public static readonly Vector2 riverSize = new Vector2(128, 64);
	public static readonly Vector2 riverCenterOffset = new Vector2(riverSize.X / 2, 0);
	private ImageTexture narrowTexture;
	private ImageTexture broadTexture;

	public RiverLayer() {
		// Both textures are 4x4 grids of equal dimensions
		// The narrow texture has four river deltas and 12 narrow river segments
		// The broad texture has 16 broad river segments with more meandering
		narrowTexture = TextureLoader.Load("terrain.river_delta");
		broadTexture = TextureLoader.Load("terrain.river");
	}

	public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {
		// The point where four terrain tiles meet and where we want to center our river texture.
		// It is the easternmost point of the current tile.
		Vector2 thePoint = tileCenter + riverCenterOffset;

		// We draw the texture centered on the point by starting the draw from half of its width away  
		Vector2 drawOffset = -0.5f * riverSize;

		// The right river texture is calculated by evaluating the tiles around the point
		Tile northOfPoint = tile.neighbors[TileDirection.NORTHEAST];
		Tile eastOfPoint = tile.neighbors[TileDirection.EAST];
		Tile southOfPoint = tile.neighbors[TileDirection.SOUTHEAST];
		Tile westOfPoint = tile;

		var (row, col, idx) = DeriveTextureIndex(northOfPoint, eastOfPoint, southOfPoint, westOfPoint);
		if (row < 0 || col < 0 || idx < 0)
			return;

		Rect2 riverRectangle = new Rect2(col * riverSize.X, row * riverSize.Y, riverSize);
		Rect2 screenTarget = new Rect2(thePoint + drawOffset, riverSize);

		// Draw a river texture from one of the textures, depending on terrain.
		// The placement calculation is the same for both river textures.

		if (idx is 1 or 2 or 4 or 8) {
			// Narrow set has deltas at these indexes
			if (HasWater(northOfPoint, eastOfPoint, southOfPoint, westOfPoint))
				looseView.DrawTextureRectRegion(narrowTexture, screenTarget, riverRectangle);
			else
				looseView.DrawTextureRectRegion(broadTexture, screenTarget, riverRectangle);
		} else {
			// TODO: When to draw from the broad texture? Is it semi-random? Is it based on elevation?

			// Default: narrow rivers, less meandering
			looseView.DrawTextureRectRegion(narrowTexture, screenTarget, riverRectangle);
		}
	}

	private static (int, int, int) DeriveTextureIndex(Tile north, Tile east, Tile south, Tile west) {
		var textureIndex = 0;

		if (north.riverSouthwest || west.riverNortheast) {
			textureIndex += 1;
		}
		if (east.riverNorthwest || north.riverSoutheast) {
			textureIndex += 2;
		}
		if (west.riverSoutheast || south.riverNorthwest) {
			textureIndex += 4;
		}
		if (south.riverNortheast || east.riverSouthwest) {
			textureIndex += 8;
		}

		// Passes "The Point" only: an oasis
		if (textureIndex == 0 && (north.riverSouth || east.riverWest || west.riverEast || south.riverNorth))
			return (0, 0, 0);

		if (textureIndex == 0)
			return (-1, -1, -1);

		// We will index into a 4x4 texture matrix
		return (textureIndex / 4, textureIndex % 4, textureIndex);
	}

	private bool HasWater(Tile north, Tile east, Tile south, Tile west) {
		return north.IsWater() || east.IsWater() || south.IsWater() || west.IsWater();
	}
}
