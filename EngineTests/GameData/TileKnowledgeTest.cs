using C7GameData;
using C7Engine;
using System.Collections.Generic;
using Xunit;

namespace EngineTests.GameData;

public class TileKnowledgeTest {

	public TileKnowledgeTest() { EngineStorage.gameData = new C7GameData.GameData(); }

	[Fact]
	public void VisibleTilesAreKnown() {
		var player = new Player();
		var knowledge = new TileKnowledge(player);
		var map = MakeTiles(["000", "000", "000", "000", "000"]);
		var tile = map.tiles[7];
		tile.unitsOnTile = [new MapUnit() { owner = player, unitType = new UnitPrototype() }];
		AssertKnownTiles(["20", "11", "31", "2", "22", "42", "13", "33", "24"], knowledge, tile);
	}

	[Fact]
	public void CityTilesAreKnown() {
		var player = new Player();
		var knowledge = new TileKnowledge(player);
		var map = MakeTiles(["000", "000", "000", "000", "000"]);
		var tile = map.tiles[7];
		tile.owningCity = new City() { owner = player };
		AssertKnownTiles(["20", "11", "31", "2", "22", "42", "13", "33", "24"], knowledge, tile);
	}

	[Fact]
	public void NeighborsAreVisibleOnFlat() {
		var map = MakeTiles(["00000", "00000", "00000", "00000", "00000", "00000", "00000", "00000", "00000"]);
		AssertVisibleTiles(["42","33", "53", "24", "44", "64", "35", "55", "46"], map.tiles[22]);
	}

	[Fact]
	public void OuterTilesAreVisibleFromHill() {
		var map = MakeTiles(["00000", "00000", "00000", "00000", "00200", "00000", "00000", "00000", "00000"]);
		AssertVisibleTiles(
			["40", "31", "51", "22", "42", "62", "13", "33", "53", "73", "4", "24", "44",
			 "64", "84", "15", "35", "55", "75", "26", "46", "66", "37", "57", "48"],
			map.tiles[22]);
	}

	[Fact]
	public void NeighborHillsHideOuterTiles() {
		var map = MakeTiles(["00000", "00000", "00000", "02000", "00200", "00000", "00200", "00000", "00000"]);
		AssertVisibleTiles(
			["40", "31", "51", "42", "62", "13", "33", "53", "73", "4", "24", "44",
			 "64", "84", "15", "35", "55", "75", "26", "46", "66", "37", "57"],
			map.tiles[22]);
	}

	static void AssertKnownTiles(List<string> ids, TileKnowledge knowledge, Tile tile) {
		knowledge.AddTilesToKnown(tile);
		AssertTiles(ids, knowledge.knownTiles);
	}

	static void AssertVisibleTiles(List<string> ids, Tile tile) {
		var tiles = new TileKnowledge(new Player()).GetTilesVisibleToUnit(tile);
		AssertTiles(ids, tiles);
	}

	static void AssertTiles(List<string> ids, ICollection<Tile> tiles) {
		Assert.Equal(ids.Count, tiles.Count);
		foreach (Tile tile in tiles) {
			Assert.Contains(tile.Id.ToString()[2..], ids);
		}
	}

	static GameMap MakeTiles(string[] terrain) {
		var map = new GameMap() {
			numTilesTall = terrain.Length,
			numTilesWide = terrain[0].Length * 2
		};
		for (int i = 0; i < terrain.Length; i++) {
			for (int j = 0; j < terrain[i].Length; j++) {
				var x = i % 2 + j * 2;
				map.tiles.Add(new Tile(ID.FromString($"a-{x:D1}{i:D1}")) {
					XCoordinate = x,
					YCoordinate = i,
					overlayTerrainType = new TerrainType() {
						height = int.Parse(terrain[i].Substring(j, 1))
					}
				});
			}
		}
		map.computeNeighbors();
		return map;
	}
}
