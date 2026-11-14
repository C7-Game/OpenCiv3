using C7GameData;
using Godot;
using static C7GameData.TileRandom;

namespace C7.Map;

public class HillsLayer : LooseLayer {
	public static readonly Vector2 mountainSize = new Vector2(128, 88);
	public static readonly Vector2 volcanoSize = new Vector2(128, 88);  //same as mountain
	public static readonly Vector2 hillsSize = new Vector2(128, 72);
	private ImageTexture mountainTexture;
	private ImageTexture snowMountainTexture;
	private ImageTexture forestMountainTexture;
	private ImageTexture jungleMountainTexture;
	private ImageTexture hillsTexture;
	private ImageTexture forestHillsTexture;
	private ImageTexture jungleHillsTexture;
	private ImageTexture volcanosTexture;
	private ImageTexture forestVolcanoTexture;
	private ImageTexture jungleVolcanoTexture;

	public HillsLayer() {
		mountainTexture = TextureLoader.Load("terrain.mountain.base");
		snowMountainTexture = TextureLoader.Load("terrain.mountain.snow");
		forestMountainTexture = TextureLoader.Load("terrain.mountain.forest");
		jungleMountainTexture = TextureLoader.Load("terrain.mountain.jungle");
		hillsTexture = TextureLoader.Load("terrain.hill.base");
		forestHillsTexture = TextureLoader.Load("terrain.hill.forest");
		jungleHillsTexture = TextureLoader.Load("terrain.hill.jungle");
		volcanosTexture = TextureLoader.Load("terrain.volcano.base");
		forestVolcanoTexture = TextureLoader.Load("terrain.volcano.forest");
		jungleVolcanoTexture = TextureLoader.Load("terrain.volcano.jungle");
	}

	public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {
		if (!tile.overlayTerrainType.isHilly()) return;

		int pcxIndex = GetMountainIndex(tile);

		int row = pcxIndex / 4;
		int column = pcxIndex % 4;

		if (tile.IsMountains()) {
			Rect2 mountainRectangle = new Rect2(column * mountainSize.X, row * mountainSize.Y, mountainSize);
			Rect2 screenTarget = new Rect2(tileCenter - (float)0.5 * mountainSize + new Vector2(0, -12), mountainSize);
			ImageTexture mountainGraphics;
			if (tile.isSnowCapped) {
				mountainGraphics = snowMountainTexture;
			} else {
				TerrainType dominantVegetation = GetDominantVegetationNearHillyTile(tile);
				if (dominantVegetation.isForest()) {
					mountainGraphics = forestMountainTexture;
				} else if (dominantVegetation.isJungle()) {
					mountainGraphics = jungleMountainTexture;
				} else {
					mountainGraphics = mountainTexture;
				}
			}
			looseView.DrawTextureRectRegion(mountainGraphics, screenTarget, mountainRectangle);
		} else if (tile.IsHills()) {
			Rect2 hillsRectangle = new Rect2(column * hillsSize.X, row * hillsSize.Y, hillsSize);
			Rect2 screenTarget = new Rect2(tileCenter - (float)0.5 * hillsSize + new Vector2(0, -4), hillsSize);
			ImageTexture hillGraphics;
			TerrainType dominantVegetation = GetDominantVegetationNearHillyTile(tile);
			if (dominantVegetation.isForest()) {
				hillGraphics = forestHillsTexture;
			} else if (dominantVegetation.isJungle()) {
				hillGraphics = jungleHillsTexture;
			} else {
				hillGraphics = hillsTexture;
			}
			looseView.DrawTextureRectRegion(hillGraphics, screenTarget, hillsRectangle);
		} else if (tile.IsVolcano()) {
			Rect2 volcanoRectangle = new Rect2(column * volcanoSize.X, row * volcanoSize.Y, volcanoSize);
			Rect2 screenTarget = new Rect2(tileCenter - (float)0.5 * volcanoSize + new Vector2(0, -12), volcanoSize);
			ImageTexture volcanoGraphics;
			TerrainType dominantVegetation = GetDominantVegetationNearHillyTile(tile);
			if (dominantVegetation.isForest()) {
				volcanoGraphics = forestVolcanoTexture;
			} else if (dominantVegetation.isJungle()) {
				volcanoGraphics = jungleVolcanoTexture;
			} else {
				volcanoGraphics = volcanosTexture;
			}
			looseView.DrawTextureRectRegion(volcanoGraphics, screenTarget, volcanoRectangle);
		}

	}

	private TerrainType GetDominantVegetationNearHillyTile(Tile center) {
		TerrainType northeastType = center.neighbors[TileDirection.NORTHEAST].overlayTerrainType;
		TerrainType northwestType = center.neighbors[TileDirection.NORTHWEST].overlayTerrainType;
		TerrainType southeastType = center.neighbors[TileDirection.SOUTHEAST].overlayTerrainType;
		TerrainType southwestType = center.neighbors[TileDirection.SOUTHWEST].overlayTerrainType;

		TerrainType[] neighborTerrains = { northeastType, northwestType, southeastType, southwestType };

		int hills = 0;
		int forests = 0;
		int jungles = 0;
		//These references are so we can return the appropriate type, and because we don't have a good way
		//to grab them directly at this point in time.
		TerrainType forest = null;
		TerrainType jungle = null;
		foreach (TerrainType type in neighborTerrains) {
			if (type.isHilly()) {
				hills++;
			} else if (type.isForest()) {
				forests++;
				forest = type;
			} else if (type.isJungle()) {
				jungles++;
				jungle = type;
			}
		}

		//some surrounding tiles are neither forested nor hilly
		if (hills + forests + jungles < 4) {
			return TerrainType.NONE;
		}

		//all hills
		if (forests == 0 && jungles == 0) {
			return TerrainType.NONE;
		}
		if (forests > jungles) {
			return forest;
		}
		if (jungles > forests) {
			return jungle;
		}

		//If we get here, it's a tie between forest and jungle. Choose one randomly.
		if (GetRandomInt(center.Id, gameSeed, 2, 0x7d4) > 0) {
			return forest;
		}
		return jungle;
	}

	private int GetMountainIndex(Tile tile) {
		int index = 0;

		if (HasVirtualHillConnection(tile, TileDirection.NORTHWEST, gameSeed)) {
			index++;
		}

		if (HasVirtualHillConnection(tile, TileDirection.NORTHEAST, gameSeed)) {
			index += 2;
		}

		if (HasVirtualHillConnection(tile, TileDirection.SOUTHWEST, gameSeed)) {
			index += 4;
		}

		if (HasVirtualHillConnection(tile, TileDirection.SOUTHEAST, gameSeed)) {
			index += 8;
		}
		return index;
	}
}
