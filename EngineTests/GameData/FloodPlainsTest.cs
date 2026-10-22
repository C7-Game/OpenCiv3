using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class FloodPlainsTest : MapBase, IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture saveGameFixture;

	public FloodPlainsTest(SaveGameFixture fix) {
		saveGameFixture = fix;
	}

	// MapBase.AddNeighborsAndUpdateMap only wires neighbors up; it doesn't
	// add the tiles to gameMap.tiles, so tests that iterate the map's tiles
	// (like AddFloodPlains does) must register them themselves.
	private void RegisterTiles(params Tile[] tiles) {
		foreach (Tile t in tiles) {
			if (!gameMap.tiles.Contains(t)) {
				gameMap.tiles.Add(t);
			}
		}
	}

	private static WorldCharacteristics MakeWorldCharacteristics(GameMap map) {
		return new WorldCharacteristics() {
			landform = WorldCharacteristics.Landform.Pangaea,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_70,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			worldSize = new WorldSize() { width = 100, height = 100 },
			terrainTypes = new List<TerrainType>() {
				new() { Key = "grassland" },
				new() { Key = "plains" },
				new() { Key = "desert" },
				new() { Key = "flood plain" },
				new() { Key = "tundra" },
				new() { Key = "coast" },
				new() { Key = "sea" },
				new() { Key = "ocean" },
				new() { Key = "forest" },
				new() { Key = "jungle" },
				new() { Key = "marsh" },
				new() { Key = "hills" },
				new() { Key = "volcano" },
				new() { Key = "mountains" },
			},
		};
	}

	[Fact]
	public void DesertWithRiverBecomesFloodPlain() {
		WorldCharacteristics wc = MakeWorldCharacteristics(gameMap);
		InitilizeStartTile(MakeDesertTile(), new TileLocation(50, 50));
		Tile neighbor = AddNeighborsAndUpdateMap(startTile, MakeDesertTile(), TileDirection.SOUTHEAST);
		RegisterTiles(startTile, neighbor);

		// The center tile has a river along its NE edge.
		startTile.riverNortheast = true;

		MapGenerator.AddFloodPlains(wc, gameMap);

		Assert.Equal("flood plain", startTile.baseTerrainType.Key);
		Assert.Equal("flood plain", startTile.overlayTerrainType.Key);

		// The neighbor has no river of its own, so it stays desert.
		Assert.Equal("desert", neighbor.baseTerrainType.Key);
	}

	[Fact]
	public void HillAndVegetationDesertDoesNotBecomeFloodPlain() {
		WorldCharacteristics wc = MakeWorldCharacteristics(gameMap);
		InitilizeStartTile(MakeDesertTile(), new TileLocation(50, 50));
		Tile hilly = AddNeighborsAndUpdateMap(startTile, new Tile(ID.None("")) {
			baseTerrainType = new TerrainType() { Key = "desert" },
			overlayTerrainType = new TerrainType() { Key = "hills" },
		}, TileDirection.SOUTHEAST);
		Tile forested = AddNeighborsAndUpdateMap(startTile, new Tile(ID.None("")) {
			baseTerrainType = new TerrainType() { Key = "desert" },
			overlayTerrainType = new TerrainType() { Key = "forest" },
		}, TileDirection.NORTHEAST);
		RegisterTiles(startTile, hilly, forested);

		startTile.riverNortheast = true;
		hilly.riverNortheast = true;
		forested.riverNortheast = true;

		MapGenerator.AddFloodPlains(wc, gameMap);

		// A desert with hills or forest on it is a different terrain, and
		// Civ3 only converts pure desert.
		Assert.Equal("desert", hilly.baseTerrainType.Key);
		Assert.Equal("hills", hilly.overlayTerrainType.Key);
		Assert.Equal("desert", forested.baseTerrainType.Key);
		Assert.Equal("forest", forested.overlayTerrainType.Key);
	}

	[Fact]
	public void DesertWithoutRiverStaysDesert() {
		WorldCharacteristics wc = MakeWorldCharacteristics(gameMap);
		InitilizeStartTile(MakeDesertTile(), new TileLocation(50, 50));
		Tile neighbor = AddNeighborsAndUpdateMap(startTile, MakeDesertTile(), TileDirection.SOUTHEAST);
		RegisterTiles(startTile, neighbor);

		MapGenerator.AddFloodPlains(wc, gameMap);

		Assert.Equal("desert", startTile.baseTerrainType.Key);
	}

	[Fact]
	public void RulesWithoutFloodPlainTerrainAreLeftAlone() {
		WorldCharacteristics wc = MakeWorldCharacteristics(gameMap);
		wc.terrainTypes.RemoveAll(x => x.Key == "flood plain");
		InitilizeStartTile(MakeDesertTile(), new TileLocation(50, 50));
		Tile neighbor = AddNeighborsAndUpdateMap(startTile, MakeDesertTile(), TileDirection.SOUTHEAST);
		RegisterTiles(startTile, neighbor);
		startTile.riverNortheast = true;

		MapGenerator.AddFloodPlains(wc, gameMap);

		Assert.Equal("desert", startTile.baseTerrainType.Key);
		Assert.Equal("desert", neighbor.baseTerrainType.Key);
	}

	// In a fully generated map, flood plains must track the final river
	// layout: a flood plain always has a river, and no pure desert tile
	// along a river is left unconverted.
	[Fact]
	public void GeneratedMapFloodPlainsTrackRivers() {
		WorldCharacteristics wc = new(saveGameFixture.saveGame) {
			landform = WorldCharacteristics.Landform.Pangaea,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_70,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Arid,
			temperature = WorldCharacteristics.Temperature.Warm,
			worldSize = new WorldSize() { width = 100, height = 100 },
			mapSeed = 123456,
		};

		GameMap m = MapGenerator.GenerateMap(wc);

		foreach (Tile t in m.tiles) {
			if (!t.IsLand()) {
				continue;
			}

			if (t.overlayTerrainType.Key == "flood plain") {
				Assert.True(t.BordersRiver(), $"Flood plain at ({t.XCoordinate},{t.YCoordinate}) has no river");
			} else if (t.baseTerrainType.Key == "desert" && t.overlayTerrainType.Key == "desert") {
				Assert.False(t.BordersRiver(), $"Desert at ({t.XCoordinate},{t.YCoordinate}) borders a river but was not converted");
			}
		}

		Assert.Contains(m.tiles, t => t.IsLand() && t.overlayTerrainType.Key == "flood plain");
	}
}
