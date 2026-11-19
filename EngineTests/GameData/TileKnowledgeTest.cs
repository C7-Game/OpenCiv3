using C7GameData;
using C7Engine;
using EngineTests.Utils;
using System.Collections.Generic;
using Xunit;

namespace EngineTests.GameData;

public class TileKnowledgeTest {

	public TileKnowledgeTest() { EngineStorage.InitializeGameDataForTests(new C7GameData.GameData()); }

	[Fact]
	public void VisibleTilesAreKnown() {
		var player = new Player();
		var knowledge = new TileKnowledge(player);
		var map = new TestMap(3, 5);
		var tile = map.TileAt(1, 2);
		tile.unitsOnTile = [new MapUnit() { owner = player, unitType = new UnitPrototype() }];
		AssertKnownTiles(knowledge, tile, [
			". x . ",
			" x x .",
			"x x x ",
			" x x .",
			". x . "
		]);
	}

	[Fact]
	public void CityTilesAreKnown() {
		var player = new Player();
		var knowledge = new TileKnowledge(player);
		var map = new TestMap(3, 5);
		var tile = map.TileAt(1, 2);
		tile.owningCity = new City() { owner = player };
		AssertKnownTiles(knowledge, tile, [
			". x . ",
			" x x .",
			"x x x ",
			" x x .",
			". x . "
		]);
	}

	static readonly TestMapBuilder builder =
		new TestMapBuilder()
			.WithRule('_', tile => SetHeight(tile, 0))
			.WithRule('^', tile => SetHeight(tile, 2));

	static void SetHeight(Tile tile, int height) {
		tile.overlayTerrainType = new TerrainType() {
			height = height
		};
	}

	[Fact]
	public void NeighborsAreVisibleOnFlat() {
		var map = builder.Build([
			"_ _ _ _ _ ",
			" _ _ _ _ _",
			"_ _ _ _ _ ",
			" _ _ _ _ _",
			"_ _ _ _ _ ",
			" _ _ _ _ _",
			"_ _ _ _ _ ",
			" _ _ _ _ _",
			"_ _ _ _ _ "
		]);
		AssertVisibleTiles(map.TileAt(2, 4), [
			". . . . . ",
			" . . . . .",
			". . x . . ",
			" . x x . .",
			". x x x . ",
			" . x x . .",
			". . x . . ",
			" . . . . .",
			". . . . . "
		]);
	}

	[Fact]
	public void OuterTilesAreVisibleFromHill() {
		var map = builder.Build([
			"_ _ _ _ _ ",
			" _ _ _ _ _",
			"_ _ _ _ _ ",
			" _ _ _ _ _",
			"_ _ ^ _ _ ",
			" _ _ _ _ _",
			"_ _ _ _ _ ",
			" _ _ _ _ _",
			"_ _ _ _ _ "
		]);
		AssertVisibleTiles(map.TileAt(2, 4), [
			". . x . . ",
			" . x x . .",
			". x x x . ",
			" x x x x .",
			"x x x x x ",
			" x x x x .",
			". x x x . ",
			" . x x . .",
			". . x . . "
		]);
	}

	[Fact]
	public void NeighborHillsHideOuterTiles() {
		var map = builder.Build([
			"_ _ _ _ _ ",
			" _ _ _ _ _",
			"_ _ _ _ _ ",
			" _ ^ _ _ _",
			"_ _ ^ _ _ ",
			" _ _ _ _ _",
			"_ _ ^ _ _ ",
			" _ _ _ _ _",
			"_ _ _ _ _ "
		]);
		AssertVisibleTiles(map.TileAt(2, 4), [
			". . x . . ",
			" . x x . .",
			". . x x . ",
			" x x x x .",
			"x x x x x ",
			" x x x x .",
			". x x x . ",
			" . x x . .",
			". . . . . "
		]);
	}

	static void AssertKnownTiles(TileKnowledge knowledge, Tile tile, List<string> layout) {
		knowledge.AddTilesToKnown(tile);
		TestMap.AssertTiles(knowledge.knownTiles, layout);
	}

	static void AssertVisibleTiles(Tile tile, List<string> layout) {
		TestMap.AssertTiles(new TileKnowledge(new Player()).GetTilesVisibleToUnit(tile), layout);
	}
}
