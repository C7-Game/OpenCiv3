using System.Collections.Generic;
using System;
using System.Linq;
using C7.Map;
using Godot;
using C7GameData;
using C7Engine;
using Serilog;
using System.Diagnostics;

// Loose layers are for drawing things on the map on a per-tile basis. (Historical aside: There used to be another kind of layer called a TileLayer
// that was intended to draw regularly tiled objects like terrain sprites but using LooseLayers for everything was found to be a prefereable
// approach.) LooseLayer is effectively the standard map layer. The MapView contains a list of loose layers, inside a LooseView object. Right now to
// add a new layer you must modify the MapView constructor to add it to the list, but (TODO) eventually that will be made moddable.
public abstract class LooseLayer {
	// drawObject draws the things this layer is supposed to draw that are associated with the given tile. Its parameters are:
	//   looseView: The Node2D to actually draw to, e.g., use looseView.DrawCircle(...) to draw a circle. This object also contains a reference to
	//     the MapView in case you need it.
	//   gameData: A reference to the game data so each layer doesn't have to redundantly request access.
	//   tile: The game tile whose contents are to be drawn. This function gets called for each tile in view of the camera and none out of
	//     view. The same tile may be drawn multiple times at different locations due to edge wrapping.
	//   tileCenter: The location to draw to. You should draw around this location without adjusting for the camera location or zoom since the
	//     MapView already transforms the looseView node to account for those things.
	public abstract void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter);

	public virtual void onBeginDraw(LooseView looseView, GameData gameData) { }
	public virtual void onEndDraw(LooseView looseView, GameData gameData) { }

	// The layer will be skipped during map drawing if visible is false
	public bool visible = true;
}

public partial class TerrainLayer : LooseLayer {

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

public partial class HillsLayer : LooseLayer {
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
		if (tile.overlayTerrainType.isHilly()) {
			int pcxIndex = getMountainIndex(tile);
			int row = pcxIndex / 4;
			int column = pcxIndex % 4;
			if (tile.overlayTerrainType.Key == "mountains") {
				Rect2 mountainRectangle = new Rect2(column * mountainSize.X, row * mountainSize.Y, mountainSize);
				Rect2 screenTarget = new Rect2(tileCenter - (float)0.5 * mountainSize + new Vector2(0, -12), mountainSize);
				ImageTexture mountainGraphics;
				if (tile.isSnowCapped) {
					mountainGraphics = snowMountainTexture;
				} else {
					TerrainType dominantVegetation = getDominantVegetationNearHillyTile(tile);
					if (dominantVegetation.Key == "forest") {
						mountainGraphics = forestMountainTexture;
					} else if (dominantVegetation.Key == "jungle") {
						mountainGraphics = jungleMountainTexture;
					} else {
						mountainGraphics = mountainTexture;
					}
				}
				looseView.DrawTextureRectRegion(mountainGraphics, screenTarget, mountainRectangle);
			} else if (tile.overlayTerrainType.Key == "hills") {
				Rect2 hillsRectangle = new Rect2(column * hillsSize.X, row * hillsSize.Y, hillsSize);
				Rect2 screenTarget = new Rect2(tileCenter - (float)0.5 * hillsSize + new Vector2(0, -4), hillsSize);
				ImageTexture hillGraphics;
				TerrainType dominantVegetation = getDominantVegetationNearHillyTile(tile);
				if (dominantVegetation.Key == "forest") {
					hillGraphics = forestHillsTexture;
				} else if (dominantVegetation.Key == "jungle") {
					hillGraphics = jungleHillsTexture;
				} else {
					hillGraphics = hillsTexture;
				}
				looseView.DrawTextureRectRegion(hillGraphics, screenTarget, hillsRectangle);
			} else if (tile.overlayTerrainType.Key == "volcano") {
				Rect2 volcanoRectangle = new Rect2(column * volcanoSize.X, row * volcanoSize.Y, volcanoSize);
				Rect2 screenTarget = new Rect2(tileCenter - (float)0.5 * volcanoSize + new Vector2(0, -12), volcanoSize);
				ImageTexture volcanoGraphics;
				TerrainType dominantVegetation = getDominantVegetationNearHillyTile(tile);
				if (dominantVegetation.Key == "forest") {
					volcanoGraphics = forestVolcanoTexture;
				} else if (dominantVegetation.Key == "jungle") {
					volcanoGraphics = jungleVolcanoTexture;
				} else {
					volcanoGraphics = volcanosTexture;
				}
				looseView.DrawTextureRectRegion(volcanoGraphics, screenTarget, volcanoRectangle);
			}
		}
	}

	private TerrainType getDominantVegetationNearHillyTile(Tile center) {
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
			} else if (type.Key == "forest") {
				forests++;
				forest = type;
			} else if (type.Key == "jungle") {
				jungles++;
				jungle = type;
			}
		}

		if (hills + forests + jungles < 4) {    //some surrounding tiles are neither forested nor hilly
			return TerrainType.NONE;
		}
		if (forests == 0 && jungles == 0) {
			return TerrainType.NONE;    //all hills
		}
		if (forests > jungles) {
			return forest;
		}
		if (jungles > forests) {
			return jungle;
		}

		//If we get here, it's a tie between forest and jungle.  Deterministically choose one so it doesn't change on every render
		if (center.XCoordinate % 2 == 0) {
			return forest;
		}
		return jungle;
	}

	private int getMountainIndex(Tile tile) {
		int index = 0;
		if (tile.neighbors[TileDirection.NORTHWEST].overlayTerrainType.isHilly()) {
			index++;
		}
		if (tile.neighbors[TileDirection.NORTHEAST].overlayTerrainType.isHilly()) {
			index += 2;
		}
		if (tile.neighbors[TileDirection.SOUTHWEST].overlayTerrainType.isHilly()) {
			index += 4;
		}
		if (tile.neighbors[TileDirection.SOUTHEAST].overlayTerrainType.isHilly()) {
			index += 8;
		}
		return index;
	}
}

public partial class ForestLayer : LooseLayer {
	public static readonly Vector2 forestJungleSize = new Vector2(128, 88);

	private ImageTexture largeJungleTexture;
	private ImageTexture smallJungleTexture;
	private ImageTexture largeForestTexture;
	private ImageTexture largePlainsForestTexture;
	private ImageTexture largeTundraForestTexture;
	private ImageTexture smallForestTexture;
	private ImageTexture smallPlainsForestTexture;
	private ImageTexture smallTundraForestTexture;
	private ImageTexture pineForestTexture;
	private ImageTexture pinePlainsTexture;
	private ImageTexture pineTundraTexture;

	public ForestLayer() {
		largeJungleTexture = TextureLoader.Load("terrain.jungle.large");
		smallJungleTexture = TextureLoader.Load("terrain.jungle.small");
		largeForestTexture = TextureLoader.Load("terrain.forest.large");
		largePlainsForestTexture = TextureLoader.Load("terrain.forest.plains.large");
		largeTundraForestTexture = TextureLoader.Load("terrain.forest.tundra.large");
		smallForestTexture = TextureLoader.Load("terrain.forest.small");
		smallPlainsForestTexture = TextureLoader.Load("terrain.forest.plains.small");
		smallTundraForestTexture = TextureLoader.Load("terrain.forest.tundra.small");
		pineForestTexture = TextureLoader.Load("terrain.pine.forest");
		pinePlainsTexture = TextureLoader.Load("terrain.pine.plains");
		pineTundraTexture = TextureLoader.Load("terrain.pine.tundra");
	}

	public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {
		if (tile.overlayTerrainType.Key == "jungle") {
			//Randomly, but predictably, choose a large jungle graphic
			//More research is needed on when to use large vs small jungles.  Probably, small is used when neighboring fewer jungles.
			//For the first pass, we're just always using large jungles.
			int randomJungleRow = tile.YCoordinate % 2;
			int randomJungleColumn;
			ImageTexture jungleTexture;
			if (tile.GetEdgeNeighbors().Any(t => t.IsWater())) {
				randomJungleColumn = tile.XCoordinate % 6;
				jungleTexture = smallJungleTexture;
			} else {
				randomJungleColumn = tile.XCoordinate % 4;
				jungleTexture = largeJungleTexture;
			}
			Rect2 jungleRectangle = new Rect2(randomJungleColumn * forestJungleSize.X, randomJungleRow * forestJungleSize.Y, forestJungleSize);
			Rect2 screenTarget = new Rect2(tileCenter - (float)0.5 * forestJungleSize + new Vector2(0, -12), forestJungleSize);
			looseView.DrawTextureRectRegion(jungleTexture, screenTarget, jungleRectangle);
		}
		if (tile.overlayTerrainType.Key == "forest") {
			int forestRow = 0;
			int forestColumn = 0;
			ImageTexture forestTexture;
			if (tile.isPineForest) {
				forestRow = tile.YCoordinate % 2;
				forestColumn = tile.XCoordinate % 6;
				if (tile.baseTerrainType.Key == "grassland") {
					forestTexture = pineForestTexture;
				} else if (tile.baseTerrainType.Key == "plains") {
					forestTexture = pinePlainsTexture;
				} else { //Tundra
					forestTexture = pineTundraTexture;
				}
			} else {
				forestRow = tile.YCoordinate % 2;
				if (tile.GetEdgeNeighbors().Any(t => t.IsWater())) {
					forestColumn = tile.XCoordinate % 5;
					if (tile.baseTerrainType.Key == "grassland") {
						forestTexture = smallForestTexture;
					} else if (tile.baseTerrainType.Key == "plains") {
						forestTexture = smallPlainsForestTexture;
					} else {    //tundra
						forestTexture = smallTundraForestTexture;
					}
				} else {
					forestColumn = tile.XCoordinate % 4;
					if (tile.baseTerrainType.Key == "grassland") {
						forestTexture = largeForestTexture;
					} else if (tile.baseTerrainType.Key == "plains") {
						forestTexture = largePlainsForestTexture;
					} else {    //tundra
						forestTexture = largeTundraForestTexture;
					}
				}
			}
			Rect2 forestRectangle = new Rect2(forestColumn * forestJungleSize.X, forestRow * forestJungleSize.Y, forestJungleSize);
			Rect2 screenTarget = new Rect2(tileCenter - (float)0.5 * forestJungleSize + new Vector2(0, -12), forestJungleSize);
			looseView.DrawTextureRectRegion(forestTexture, screenTarget, forestRectangle);
		}
	}
}
public partial class MarshLayer : LooseLayer {
	public static readonly Vector2 marshSize = new Vector2(128, 88);
	//Because the marsh graphics are 88 pixels tall instead of the 64 of a tile, we also need an addition 12 pixel offset to the top
	//88 - 64 = 24; 24/2 = 12.  This keeps the marsh centered with half the extra 24 pixels above the tile and half below.
	readonly Vector2 MARSH_OFFSET = (float)0.5 * marshSize + new Vector2(0, -12);

	private ImageTexture largeMarshTexture;
	private ImageTexture smallMarshTexture;

	public MarshLayer() {
		largeMarshTexture = TextureLoader.Load("terrain.marsh.large");
		smallMarshTexture = TextureLoader.Load("terrain.marsh.small");
	}

	public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {
		if (tile.overlayTerrainType.Key == "marsh") {
			int randomJungleRow = tile.YCoordinate % 2;
			int randomMarshColumn;
			ImageTexture marshTexture;
			if (tile.GetEdgeNeighbors().Any(t => t.IsWater())) {
				randomMarshColumn = tile.XCoordinate % 5;
				marshTexture = smallMarshTexture;
			} else {
				randomMarshColumn = tile.XCoordinate % 4;
				marshTexture = largeMarshTexture;
			}
			Rect2 jungleRectangle = new Rect2(randomMarshColumn * marshSize.X, randomJungleRow * marshSize.Y, marshSize);
			Rect2 screenTarget = new Rect2(tileCenter - MARSH_OFFSET, marshSize);
			looseView.DrawTextureRectRegion(marshTexture, screenTarget, jungleRectangle);
		}
	}
}

public partial class RiverLayer : LooseLayer {
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

public partial class GridLayer : LooseLayer {
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

public partial class BuildingLayer : LooseLayer {
	private ImageTexture barbCamp;

	public BuildingLayer() {
		barbCamp = TextureLoader.Load("terrain.barbarian_camp");
	}

	public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {
		if (tile.hasBarbarianCamp) {
			Rect2 screenRect = new(tileCenter - 0.5f * barbCamp.GetSize(), barbCamp.GetSize());
			looseView.DrawTextureRect(barbCamp, screenRect, tile: false);
		}
	}
}

public partial class LooseView : Node2D {
	public MapView mapView;
	public List<LooseLayer> layers = new List<LooseLayer>();
	private static ILogger log = Log.ForContext<LooseView>();

	protected List<VisibleTile> visibleKnownTiles;
	protected List<VisibleTile> visibleUnKnownTiles;
	public Dictionary<Tile, Vector2> tileCenters { get; private set; } = new Dictionary<Tile, Vector2>();

	public LooseView(MapView mapView) {
		this.mapView = mapView;
	}

	protected struct VisibleTile {
		public Tile tile;
		public Vector2 tileCenter;
	}

	public override void _Draw() {
		base._Draw();

		EngineStorage.ReadGameData((GameData gD) => {
			// Iterating over visible tiles is unfortunately pretty expensive. Assemble a list of Tile references and centers first so we don't
			// have to reiterate for each layer. Doing this improves framerate significantly.
			MapView.VisibleRegion visRegion = mapView.getVisibleRegion();
			visibleKnownTiles = new List<VisibleTile>();
			visibleUnKnownTiles = new List<VisibleTile>();
			GetVisibleTiles(visRegion, gD);

			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();
			foreach (LooseLayer layer in layers.FindAll(L => L.visible && !(L is FogOfWarLayer) && !(L is GridLayer))) {
				layer.onBeginDraw(this, gD);
				foreach (VisibleTile vT in visibleKnownTiles) {
					layer.drawObject(this, gD, vT.tile, vT.tileCenter);
				}

				layer.onEndDraw(this, gD);
			}

			foreach (var layer in layers.Where(layer => layer.visible && layer is GridLayer)) {
				layer.onBeginDraw(this, gD);
				foreach (VisibleTile vT in visibleKnownTiles.Concat(visibleUnKnownTiles)) {
					layer.drawObject(this, gD, vT.tile, vT.tileCenter);
				}

				layer.onEndDraw(this, gD);
			}

			if (stopwatch.ElapsedMilliseconds > 100) {
				log.Warning($"-> End draw: {stopwatch.ElapsedMilliseconds} milliseconds");
			}

			if (!gD.observerMode && !gD.revealMap) {
				foreach (FogOfWarLayer layer in layers.Where(layer => layer is FogOfWarLayer).Cast<FogOfWarLayer>()) {
					for (int Y = visRegion.upperLeftY; Y < visRegion.lowerRightY; Y++) {
						if (gD.map.isRowAt(Y)) {
							for (int X = visRegion.getRowStartX(Y); X < visRegion.lowerRightX; X += 2) {
								Tile tile = gD.map.tileAt(X, Y);
								if (tile != Tile.NONE) {
									VisibleTile invisibleTile = new VisibleTile {
										tile = tile,
										tileCenter = MapView.cellSize * new Vector2(X + 1, Y + 1)
									};
									layer.drawObject(this, gD, tile, invisibleTile.tileCenter);
								}
							}
						}
					}
				}
			}
		});
	}

	private void GetVisibleTiles(MapView.VisibleRegion visRegion, GameData gD) {
		for (int Y = visRegion.upperLeftY; Y < visRegion.lowerRightY; Y++) {
			if (gD.map.isRowAt(Y)) {
				for (int X = visRegion.getRowStartX(Y); X < visRegion.lowerRightX; X += 2) {
					Tile tile = gD.map.tileAt(X, Y);
					Vector2 tileCenter = MapView.cellSize * new Vector2(X + 1, Y + 1);
					if (IsTileKnown(tile, gD)) {
						visibleKnownTiles.Add(new VisibleTile {
							tile = tile,
							tileCenter = tileCenter
						});
					} else {
						visibleUnKnownTiles.Add(new VisibleTile {
							tile = tile,
							tileCenter = tileCenter
						});
					}
					tileCenters[tile] = tileCenter;
				}
			}
		}
	}

	private static bool IsTileKnown(Tile tile, GameData gameData) {
		if (gameData.observerMode || gameData.revealMap) {
			return true;
		}
		TileKnowledge knowledge = gameData.GetFirstHumanPlayer().tileKnowledge;
		return tile != Tile.NONE && knowledge.isTileKnown(tile);
	}

	public bool IsTileCoveredByTileInfo(Tile tile) {
		var target = mapView.game.tileInfo;
		if (target == null) return false;

		return target.IsCovered(tile);
	}

	public bool HasCityLabelToHideFromTileInfo(Tile tile) {
		if (!tile.HasCity()) return false;

		var target = mapView.game.tileInfo;
		if (target == null) return false;

		return target.HasCityLabelToCover(tile);
	}
}

public partial class MapView : Node2D {
	// cellSize is half the size of the tile sprites, or the amount of space each tile takes up when they are packed on the grid (note tiles are
	// staggered and half overlap).
	public static readonly Vector2 cellSize = new Vector2(64, 32);
	public Vector2 scaledCellSize {
		get { return cellSize * new Vector2(cameraZoom, cameraZoom); }
	}

	public Game game;

	public MapView() { }

	public int mapWidth { get; private set; }
	public int mapHeight { get; private set; }
	public bool wrapHorizontally { get; private set; }
	public bool wrapVertically { get; private set; }

	private Vector2 internalCameraLocation = new Vector2(0, 0);
	public Vector2 cameraLocation {
		get {
			return internalCameraLocation;
		}
		set {
			setCameraLocation(value);
		}
	}
	public float internalCameraZoom = 1;
	public float cameraZoom {
		get { return internalCameraZoom; }
		set { setCameraZoomFromMiddle(value); }
	}

	private List<LooseView> looseViews = new();

	// Specifies a rectangular block of tiles that are currently potentially on screen. Accessible through getVisibleRegion(). Tile coordinates
	// are "virtual", i.e. "unwrapped", so there isn't necessarily a tile at each location. The region is intended to include the upper left
	// coordinates but not the lower right ones. When iterating over all tiles in the region you must account for the fact that map rows are
	// staggered, see LooseView._Draw for an example.
	public struct VisibleRegion {
		public int upperLeftX, upperLeftY;
		public int lowerRightX, lowerRightY;

		public int getRowStartX(int y) {
			return upperLeftX + (y - upperLeftY) % 2;
		}
	}

	public GridLayer gridLayer { get; private set; }
	public CityLayer cityLayer { get; private set; }
	public TileAssignmentLayer tileAssignmentLayer { get; private set; }

	const float MIN_SCALE = 0.1f;
	const float MAX_SCALE = 4.0f;

	private TransportInfoBox transportInfoBox;
	private LowerRightInfoBox lowerRightInfoBox;
	private MiniMap miniMap;

	public override void _Ready() {
		lowerRightInfoBox = GetNode<LowerRightInfoBox>("/root/C7Game/CanvasLayer/Control/GameStatus/LowerRightInfoBox");
		lowerRightInfoBox.game = game;
		lowerRightInfoBox.CenterCameraOnActiveUnit += OnCenterCameraOnUnit;

		var canvasControl = GetNode<Control>("/root/C7Game/CanvasLayer/Control");

		miniMap = new MiniMap(this);
		canvasControl.AddChild(miniMap);

		transportInfoBox = new TransportInfoBox(game);
		canvasControl.AddChild(transportInfoBox);
	}

	public override void _ExitTree() {
		lowerRightInfoBox.CenterCameraOnActiveUnit -= OnCenterCameraOnUnit;
	}

	public MapView(Game game, int mapWidth, int mapHeight, bool wrapHorizontally, bool wrapVertically) {
		this.game = game;
		this.mapWidth = mapWidth;
		this.mapHeight = mapHeight;
		this.wrapHorizontally = wrapHorizontally;
		this.wrapVertically = wrapVertically;

		// Set up our set of views, and the layers within each view.
		//
		// The drawing order within a view matches the order of `layers`, so
		// here borders will be drawn on top of the terrain.
		//
		// However, because cities and units use child nodes, we need separate
		// LooseView objects to get the ordering correct between textures and
		// nodes. Without this unit health bars (which are textures) would
		// be drawn behind cities (which are child nodes).
		LooseView terrainView = new(this);
		terrainView.layers.Add(new TerrainLayer());
		terrainView.layers.Add(new RiverLayer());
		terrainView.layers.Add(new ForestLayer());
		terrainView.layers.Add(new MarshLayer());
		terrainView.layers.Add(new HillsLayer());
		terrainView.layers.Add(new TntLayer());
		terrainView.layers.Add(new TileOverlayLayer());
		terrainView.layers.Add(new ResourceLayer());
		this.gridLayer = new GridLayer();
		terrainView.layers.Add(this.gridLayer);
		terrainView.layers.Add(new BuildingLayer());
		terrainView.layers.Add(new BorderLayer());

		LooseView cityView = new(this);
		this.cityLayer = new();
		cityView.layers.Add(this.cityLayer);

		LooseView tileAssignmentView = new(this);
		this.tileAssignmentLayer = new();
		tileAssignmentView.layers.Add(this.tileAssignmentLayer);

		LooseView fogOfWarView = new(this);
		fogOfWarView.layers.Add(new FogOfWarLayer());

		LooseView unitView = new(this);
		unitView.layers.Add(new GotoLayer());
		unitView.layers.Add(new BombardLayer());
		LooseView otherView = new(this);
		otherView.layers.Add(new UnitLayer());

		AddChild(terrainView);
		looseViews.Add(terrainView);

		AddChild(cityView);
		looseViews.Add(cityView);

		AddChild(tileAssignmentView);
		looseViews.Add(tileAssignmentView);

		AddChild(fogOfWarView);
		looseViews.Add(fogOfWarView);

		AddChild(unitView);
		looseViews.Add(unitView);
		AddChild(otherView);
		looseViews.Add(otherView);
	}

	public override void _Process(double delta) {
		// Redraw everything. This is necessary so that animations play. Maybe we could only update the unit layer but long term I think it's
		// better to redraw everything every frame like a typical modern video game.
		foreach (LooseView looseView in looseViews) {
			looseView.QueueRedraw();
		}
	}

	// Returns the size in pixels of the area in which the map will be drawn. This is the viewport size or, if that's null, the window size.
	public Vector2 getVisibleAreaSize() {
		return GetViewport() != null ? GetViewportRect().Size : DisplayServer.WindowGetSize();
	}

	public VisibleRegion getVisibleRegion() {
		(int X0, int Y0) = tileCoordsInMainCamera(new Vector2(0, 0));
		Vector2 mapViewSize = new Vector2(2, 4) + getVisibleAreaSize() / scaledCellSize;
		return new VisibleRegion {
			upperLeftX = X0 - 2,
			upperLeftY = Y0 - 2,
			lowerRightX = X0 + (int)mapViewSize.X,
			lowerRightY = Y0 + (int)mapViewSize.Y
		};
	}

	// "center" is the screen location around which the zoom is centered, e.g., if center is (0, 0) the tile in the top left corner will be the
	// same after the zoom level is changed, and if center is screenSize/2, the tile in the center of the window won't change.
	public void setCameraZoom(float newScale, Vector2 center) {
		newScale = Math.Clamp(newScale, MIN_SCALE, MAX_SCALE);

		Vector2 v2NewZoom = new Vector2(newScale, newScale);
		Vector2 v2OldZoom = new Vector2(cameraZoom, cameraZoom);
		if (v2NewZoom != v2OldZoom) {
			internalCameraZoom = newScale;
			foreach (LooseView looseView in looseViews) {
				looseView.Scale = v2NewZoom;
			}
			setCameraLocation((v2NewZoom / v2OldZoom) * (cameraLocation + center) - center);
		}
	}

	// Zooms in or out centered on the middle of the screen
	public void setCameraZoomFromMiddle(float newScale) {
		setCameraZoom(newScale, getVisibleAreaSize() / 2);
	}

	public void moveCamera(Vector2 offset) {
		setCameraLocation(cameraLocation + offset);
	}

	public void setCameraLocation(Vector2 location) {
		// Prevent the camera from moving beyond an unwrapped edge of the map. One complication here is that the viewport might actually be
		// larger than the map (if we're zoomed far out) so in that case we must apply the constraint the other way around, i.e. constrain the
		// map to the viewport rather than the viewport to the map.
		Vector2 visAreaSize = getVisibleAreaSize();
		Vector2 mapPixelSize = new Vector2(cameraZoom, cameraZoom) * (new Vector2(cellSize.X * (mapWidth + 1), cellSize.Y * (mapHeight + 1)));
		if (!wrapHorizontally) {
			float leftLim, rightLim;
			{
				if (mapPixelSize.X >= visAreaSize.X) {
					leftLim = 0;
					rightLim = mapPixelSize.X - visAreaSize.X;
				} else {
					leftLim = mapPixelSize.X - visAreaSize.X;
					rightLim = 0;
				}
			}
			if (location.X < leftLim)
				location.X = leftLim;
			else if (location.X > rightLim)
				location.X = rightLim;
		}
		if (!wrapVertically) {
			// These margins allow the player to move the camera that far off those map edges so that the UI controls don't cover up the
			// map. TODO: These values should be read from the sizes of the UI elements instead of hardcoded.
			float topMargin = 70, bottomMargin = 140;
			float topLim, bottomLim;
			{
				if (mapPixelSize.Y >= visAreaSize.Y) {
					topLim = -topMargin;
					bottomLim = mapPixelSize.Y - visAreaSize.Y + bottomMargin;
				} else {
					topLim = mapPixelSize.Y - visAreaSize.Y;
					bottomLim = 0;
				}
			}
			if (location.Y < topLim)
				location.Y = topLim;
			else if (location.Y > bottomLim)
				location.Y = bottomLim;
		}

		internalCameraLocation = location;
		foreach (LooseView looseView in looseViews) {
			looseView.Position = -location;
		}
	}

	public Vector2 screenLocationOfTileCoords(int X, int Y, bool center = true) {
		// Add one to X & Y to get the tile center b/c in Civ 3 the tile at (X, Y) is a diamond centered on (x+1, y+1).
		Vector2 centeringOffset = center ? new Vector2(1, 1) : new Vector2(0, 0);

		var mapLoc = (new Vector2(X, Y) + centeringOffset) * cellSize;
		return mapLoc * cameraZoom - cameraLocation;
	}

	// Returns the location of tile (X, Y) on the screen, if "center" is true returns the location of the tile center and otherwise returns the
	// upper left. Works even if (X, Y) is off screen or out of bounds.
	public Vector2 screenLocationOfTile(Tile tile, bool center = true) {
		return screenLocationOfTileCoords(tile.XCoordinate, tile.YCoordinate, center);
	}

	// Returns the virtual tile coordinates on screen at the given location. "Virtual" meaning the coordinates are unwrapped and there isn't
	// necessarily a tile there at all.
	public (int, int) tileCoordsInMainCamera(Vector2 screenLocation) {
		Vector2 mapLoc = (screenLocation + cameraLocation) / scaledCellSize;
		return tileCoordsForMapLocation(mapLoc);
	}

	public (int, int) tileCoordsForMapLocation(Vector2 mapLocation) {
		Vector2 intMapLoc = mapLocation.Floor();
		Vector2 fracMapLoc = mapLocation - intMapLoc;
		int X = (int)intMapLoc.X, Y = (int)intMapLoc.Y;
		bool evenColumn = X % 2 == 0, evenRow = Y % 2 == 0;
		if (evenColumn ^ evenRow) {
			if (fracMapLoc.Y > fracMapLoc.X)
				X -= 1;
			else
				Y -= 1;
		} else {
			if (fracMapLoc.Y < 1 - fracMapLoc.X) {
				X -= 1;
				Y -= 1;
			}
		}
		return (X, Y);
	}

	public Tile tileOnScreenAt(GameMap map, Vector2 screenLocation) {
		(int X, int Y) = tileCoordsInMainCamera(screenLocation);
		return map.tileAt(X, Y);
	}

	public void centerCameraOnTile(Tile t) {
		var tileCenter = new Vector2(t.XCoordinate + 1, t.YCoordinate + 1) * scaledCellSize;
		setCameraLocation(tileCenter - (float)0.5 * getVisibleAreaSize());
	}

	public void OnCenterCameraOnUnit() {
		MapUnit currentlySelectedUnit = game.CurrentlySelectedUnit;
		if (!MapUnit.IsMapUnitValid(currentlySelectedUnit))
			return;
		centerCameraOnTile(currentlySelectedUnit.location);
	}
}
