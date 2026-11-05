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
}
