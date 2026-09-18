using System.Collections.Generic;
using System.Linq;
using C7GameData;
using C7GameData.Save;

namespace EngineTests.Utils;

public class MapBase {
	protected GameMap gameMap = new GameMap() { tiles = new List<Tile>()};
	protected Tile startTile;

	protected static MapUnit MakeWaterUnit(int movementPoints = 2) {
		MapUnit result = new(ID.None("")) {
			unitType = new UnitPrototype() {
				movement = movementPoints,
			},
			owner = new Player()
		};
		result.unitType.categories.Add("Sea");
		return result;
	}

	protected static MapUnit MakeLandUnit(int movementPoints = 1) {
		MapUnit result = new(ID.None("")) {
			unitType = new UnitPrototype() {
				movement = movementPoints,
			},
			owner = new Player(),
		};
		result.unitType.categories.Add("Land");
		return result;
	}

	protected void InitilizeStartTile(Tile start, TileLocation tileLocation) {
		startTile = start;
		startTile.XCoordinate = tileLocation.X;
		startTile.YCoordinate = tileLocation.Y;
	}

	protected Player MakePlayer(bool isHuman = false) {
		return new Player() { isHuman = isHuman };
	}

	protected Player MakeTestPlayer(List<Tile> knownTiles) {
		Player player = MakePlayer(true);
		player.civilization = new Civilization();
		player.government = new Government();
		player.rules = MakeTestRules();
		foreach (Tile tile in knownTiles) { player.tileKnowledge.knownTiles.Add(tile); }
		return player;
	}

	protected static Rules MakeTestRules() {
		return new Rules() {
			MaxRankOfWorkableTiles = 2
		};
	}

	private TileDirection[] directions = {
		TileDirection.NORTH,
		TileDirection.NORTHEAST,
		TileDirection.NORTHWEST,
		TileDirection.SOUTH,
		TileDirection.SOUTHEAST,
		TileDirection.SOUTHWEST,
		TileDirection.EAST,
		TileDirection.WEST,
	};

	protected List<Tile> SurroundTile(Tile center, System.Func<Tile> neighbors) {
		List<Tile> map = new();
		map.Add(center);
		map.Add(AddNeighborsAndUpdateMap(startTile, neighbors(), TileDirection.NORTH));
		map.Add(AddNeighborsAndUpdateMap(startTile, neighbors(), TileDirection.NORTHEAST));
		map.Add(AddNeighborsAndUpdateMap(startTile, neighbors(), TileDirection.EAST));
		map.Add(AddNeighborsAndUpdateMap(startTile, neighbors(), TileDirection.SOUTHEAST));
		map.Add(AddNeighborsAndUpdateMap(startTile, neighbors(), TileDirection.SOUTH));
		map.Add(AddNeighborsAndUpdateMap(startTile, neighbors(), TileDirection.SOUTHWEST));
		map.Add(AddNeighborsAndUpdateMap(startTile, neighbors(), TileDirection.WEST));
		map.Add(AddNeighborsAndUpdateMap(startTile, neighbors(), TileDirection.NORTHWEST));
		return map;
	}

	// Probably not the best way to update all he neighbors,
	// but it's only meant to be used in the unit tests
	//
	/// <summary>
	///This is needed because the unit tests don't have any knowledge of the tile neighbors,
	/// apart from exactly what we give it ourselves. <br/><br/>
	/// We might intuitively say that Tile [50,50] neighbors [52,50] in the east,
	/// but unless we specifically update this relationship in the unit test, it won't take effect,
	/// and the A* won't work as we would expect
	/// </summary>
	/// <param name="tiles"></param>
	protected void ComputeAllNeighbors(HashSet<Tile> tiles) {
		foreach (var tile in tiles) {
			foreach (TileDirection direction in directions) {
				List<Tile> eligible = tiles.Where(t =>
					Tile.NeighborCoordinate(new TileLocation(tile.XCoordinate, tile.YCoordinate), direction)
						.Equals(new TileLocation(t.XCoordinate, t.YCoordinate)) && tiles.Contains(t)).ToList();
				if (eligible.Count <= 0) continue;
				foreach (Tile eligibleTile in eligible) {
					AddNeighborsAndUpdateMap(tile, eligibleTile, direction);
				}
			}
		}
	}

	protected Tile AddNeighborsAndUpdateMap(Tile tile, Tile neighbor, TileDirection tileDirection) {
		tile.neighbors[tileDirection] = neighbor;
		if (gameMap.tiles.Contains(tile)) {
			gameMap.tiles.Add(tile);
		}
		if (gameMap.tiles.Contains(neighbor)) {
			gameMap.tiles.Add(neighbor);
		}
		if (tile.map == null) {
			tile.map = gameMap;
		}
		if (neighbor.map == null) {
			neighbor.map = gameMap;
		}
		if (tileDirection == TileDirection.NORTH) {
			neighbor.XCoordinate = tile.XCoordinate;
			neighbor.YCoordinate = tile.YCoordinate - 2;
		}
		if (tileDirection == TileDirection.EAST) {
			neighbor.XCoordinate = tile.XCoordinate + 2;
			neighbor.YCoordinate = tile.YCoordinate;
		}
		if (tileDirection == TileDirection.SOUTH) {
			neighbor.XCoordinate = tile.XCoordinate;
			neighbor.YCoordinate = tile.YCoordinate + 2;
		}
		if (tileDirection == TileDirection.WEST) {
			neighbor.XCoordinate = tile.XCoordinate - 2;
			neighbor.YCoordinate = tile.YCoordinate;
		}
		if (tileDirection == TileDirection.NORTHEAST) {
			neighbor.XCoordinate = tile.XCoordinate + 1;
			neighbor.YCoordinate = tile.YCoordinate - 1;
		}
		if (tileDirection == TileDirection.SOUTHEAST) {
			neighbor.XCoordinate = tile.XCoordinate + 1;
			neighbor.YCoordinate = tile.YCoordinate + 1;
		}
		if (tileDirection == TileDirection.NORTHWEST) {
			neighbor.XCoordinate = tile.XCoordinate - 1;
			neighbor.YCoordinate = tile.YCoordinate - 1;
		}
		if (tileDirection == TileDirection.SOUTHWEST) {
			neighbor.XCoordinate = tile.XCoordinate - 1;
			neighbor.YCoordinate = tile.YCoordinate + 1;
		}

		return neighbor;
	}

	protected TerrainImprovement road = new("road", TerrainImprovement.Layer.Roads, movementCost: 1.0f / 3);

	protected Tile MakeMountainTile() {
		return new(ID.None("")) {
			baseTerrainType = new() { Key = "mountains" },
			overlayTerrainType = new() { Key = "mountains", movementCost = 3 }
		};
	}
	protected Tile MakeHillTile() {
		return new(ID.None("")) {
			baseTerrainType = new() { Key = "hills" },
			overlayTerrainType = new() { Key = "hills", movementCost = 2 }
		};
	}
	protected Tile MakePlainsTile() {
		return new(ID.None("")) {
			baseTerrainType = new() { Key = "plains" },
			overlayTerrainType = new() { Key = "plains", movementCost = 1 }
		};
	}
	protected Tile MakeDesertTile() {
		return new(ID.None("")) {
			baseTerrainType = new() { Key = "desert" },
			overlayTerrainType = new() { Key = "desert", movementCost = 1 }
		};
	}
	protected Tile MakeImpassableDesertTile() {
		return new(ID.None("")) {
			baseTerrainType = new() { Key = "desert" },
			overlayTerrainType = new() { Key = "desert", movementCost = 1, impassable = true }
		};
	}
	protected Tile MakeFloodPlainTile() {
		return new(ID.None("")) {
			baseTerrainType = new() { Key = "flood plain" },
			overlayTerrainType = new() { Key = "flood plain", movementCost = 1 }
		};
	}
	protected Tile MakeCoastTile() {
		return new(ID.None("")) {
			baseTerrainType = new() { Key = "coast" },
			overlayTerrainType = new() { Key = "coast", movementCost = 1 }
		};
	}
	protected Tile MakeLakeTile() {
		Tile lakeTile = new(ID.None("")) {
			baseTerrainType = new() { Key = "coast" },
			overlayTerrainType = new() { Key = "coast", movementCost = 1 }
		};
		lakeTile.isFreshWater = true;
		return lakeTile;
	}
	protected Tile MakeSeaTile() {
		return new(ID.None("")) {
			baseTerrainType = new() { Key = "sea" },
			overlayTerrainType = new() { Key = "sea", movementCost = 1 }
		};
	}
	protected Tile MakeOceanTile() {
		return new(ID.None("")) {
			baseTerrainType = new() { Key = "ocean" },
			overlayTerrainType = new() { Key = "ocean", movementCost = 1 }
		};
	}

	/// <summary>
	/// Given game map dimensions in tiles and an incomplete set of tiles, fill the
	/// game map with NONE tiles, and then overlay the known tiles.
	/// </summary>
	/// <param name="tilesWidth"></param>
	/// <param name="tilesHeight"></param>
	/// <param name="tiles"></param>
	protected void InitPartialGameMap(int tilesWidth, int tilesHeight, List<Tile> tiles) {
		gameMap.numTilesWide = tilesWidth;
		gameMap.numTilesTall = tilesHeight;
		gameMap.tiles = Enumerable.Range(0, tilesWidth * tilesHeight).Select(x => Tile.NONE).ToList();
		foreach (var tile in tiles) {
			var idx = gameMap.tileCoordsToIndex(tile.XCoordinate, tile.YCoordinate);
			gameMap.tiles[idx] = tile;
		}
	}
}
