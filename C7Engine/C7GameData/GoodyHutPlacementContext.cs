using System;

namespace C7GameData;

/// The context passed to the Lua goody hut placement script
/// (behaviors "goody_huts.placement.place") during map generation.
///
/// Map generation runs before GameData exists, so this context carries its
/// own seeded RNG instead of using GameData.rng. Lua must draw from
/// Random() so that hut placement is reproducible from the map seed.
public class GoodyHutPlacementContext {
	private readonly GameMap map;
	private readonly Random random;

	public BarbarianActivity barbarianActivity { get; }

	public GoodyHutPlacementContext(GameMap map, BarbarianActivity barbarianActivity, Random random) {
		this.map = map;
		this.barbarianActivity = barbarianActivity;
		this.random = random;
	}

	public int tileCount => map.tiles.Count;

	/// Returns the tile at a 0-based index into the map's tile list.
	///
	/// Use this instead of map.tiles from Lua: Moonsharp copies a C# list
	/// into a new Lua table every time it is read.
	public Tile TileAtIndex(int index)
		=> map.tiles[index];

	/// Returns an integer in [0, maxExclusive) from the placement RNG.
	public int Random(int maxExclusive)
		=> random.Next(maxExclusive);

	public bool IsStartingLocation(Tile tile)
		=> map.startingLocations.Contains(tile);

	/// Puts a hut on the tile. The ruleset's terrain improvements aren't
	/// loaded during map generation, so this is a stand-in with the same key;
	/// saving keeps only the key, and loading swaps in the ruleset's version.
	public void PlaceHut(Tile tile)
		=> tile.overlays.Add(hut);

	private static readonly TerrainImprovement hut = new(Tile.TileOverlays.GOODY_HUT, TerrainImprovement.Layer.GoodyHut);
}
