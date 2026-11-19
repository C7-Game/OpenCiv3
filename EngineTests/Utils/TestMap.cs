using C7GameData;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace EngineTests.Utils;

/*
These classes use a list of strings to create a visual layout of tile locations in a game map.
Each item in the layout list represents a row in the map and the characters in the row represent the columns.
The column characters are separated with spaces.  In the odd rows, the spaces are trailing after each column,
in the even rows the spaces are before each column.  This helps create a visual representation of the
"rhombus" layout of the game map.

For example, a 3 column x 5 row layout looks like this:
[
	"x x x ",
	" x x x",
	"x x x ",
	" x x x",
	"x x x "
]

The column characters have different values, depending on how the layout is being used, as described below.
*/

public class TestMapBuilder {
	/*
	This class builds a map based on a layout.  The column characters used in the layout are customized by adding rules to the class.
	Each rule specifies a character and a action to be performed on a the tile with that character.

	For example: we can build a map with different terrain hegihts by defining a character for flat terrain (height 0)
	and hilly terrain (height 2)

	static readonly TestMapBuilder builder =
		new TestMapBuilder()
			.WithRule('_', tile => SetHeight(tile, 0))
			.WithRule('^', tile => SetHeight(tile, 2));

	static void SetHeight(Tile tile, int height) {
		tile.overlayTerrainType = new TerrainType() {
			height = height
		};
	}

	Then we can build a map:

	var map = builder.Build([
		"_ _ _ ",
		" _ ^ _",
		"_ ^ ^ ",
		" _ ^ ^",
		"_ _ _ "
	]);

	*/
	readonly Dictionary<char, Action<Tile>> rules = [];

	// add a rule to the builder
	public TestMapBuilder WithRule(char character, Action<Tile> action) {
		rules.Add(character, action);
		return this;
	}

	// build a map based on a layout
	public TestMap Build(List<string> layout) {
		var map = new TestMap(layout[0].Length / 2, layout.Count);
		for (int i = 0; i < layout.Count; i++) {
			for (int j = 0; j < layout[i].Length / 2; j++) {
				var tile = map.TileAt(j, i);
				rules[layout[tile.YCoordinate][tile.XCoordinate]](tile);
			}
		}
		return map;
	}
}

public class TestMap {
	readonly GameMap map;

	// create a game map of a given width and height
	public TestMap(int width, int height) {
		map = new GameMap() {
			numTilesTall = height,
			numTilesWide = width * 2
		};
		for (int i = 0; i < height; i++) {
			for (int j = 0; j < width; j++) {
				map.tiles.Add(new Tile(ID.FromString("test-1")) {
					XCoordinate = i % 2 + j * 2,
					YCoordinate = i
				});
			}
		}
		map.computeNeighbors();
	}

	// return the tile at a given column and row in the map
	public Tile TileAt(int column, int row) {
		return map.tiles[row * map.numTilesWide / 2 + column];
	}

	/*
	This method verifies tile locations in a map, based on a layout. The column characters are 'x' to indicate the presence
	of a tile at that location, and '.' to indicate the absence of a tile at that location.  This is used in xUnit tests,
	any discrepancy results in an xUnit exception.

	For example: we can validate the presence of a neighbourhood of tiles in a 3 x 5 map:

	TestMap.AssertTiles(tile_list, [
		". x . ",
		" x x .",
		"x x x ",
		" x x .",
		". x . "
	]);

	*/
	public static void AssertTiles(ICollection<Tile> tiles, List<string> layout) {
		Assert.Equal(layout.Sum(row => row.Count(c => c == 'x')), tiles.Count);
		foreach (Tile tile in tiles) {
			Assert.Equal('x', layout[tile.YCoordinate][tile.XCoordinate]);
		}
	}
}
