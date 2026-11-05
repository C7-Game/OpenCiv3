using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;

namespace C7GameData;

/// The context passed to the Lua script that pops a goody hut
/// (behaviors "goody_huts.pop").
///
/// It exposes the game state and the actions the hut outcomes need. Several
/// of those actions wrap engine methods that are not public, so Lua should
/// go through this class rather than calling into GameData directly.
///
/// Random numbers come from the global RNG(n), not from this context.
public class GoodyHutContext {
	public GameData gameData { get; }
	public MapUnit unit { get; }
	public Player player { get; }
	public Tile tile { get; }

	public GoodyHutContext(GameData gameData, MapUnit unit, Tile tile) {
		this.gameData = gameData;
		this.unit = unit;
		this.player = unit.owner;
		this.tile = tile;
	}

	public int turn => gameData.turn;

	// ---- Information ----

	/// The 0-based position of the game's difficulty in the difficulty list
	/// (0 = the easiest level).
	public int difficultyIndex
		=> gameData.difficulties.FindIndex(d => d.Name == gameData.gameDifficulty.Name);

	public bool isExpansionist => HasTrait(Civilization.Trait.Expansionist);


	/// The number of non-barbarian civs still in the game.
	public int livingCivCount => gameData.players.Count(p => !p.isBarbarians && !p.defeated);

	/// The number of cities in the whole world.
	public int worldCityCount => gameData.cities.Count;

	public int ownCityCount => player.cities.Count;

	public bool HasTrait(Civilization.Trait trait)
		=> player.civilization.traits.Contains(trait);

	/// The Manhattan distance from the hut to the nearest city of the player
	/// who popped it, or int.MaxValue if they have no cities.
	public int DistanceToNearestOwnCity() {
		int nearest = int.MaxValue;
		foreach (City city in player.cities) {
			nearest = Math.Min(nearest, tile.ManhattanDistanceTo(city.location));
		}
		return nearest;
	}

	/// Whether a new city could be founded on the hut's tile, using the same
	/// test as a settler's "build city" order.
	public bool CanFoundCity() {
		if (tile.HasCity() || !tile.IsAllowCities()) {
			return false;
		}
		return tile.neighbors.Values.All(t => !t.HasCity());
	}

	public bool KnowsTech(Tech tech)
		=> player.knownTechs.Contains(tech.id);

	/// The 0-based era the player is in (0 = ancient times).
	public int playerEraIndex => player.EraIndex();

	/// The player's full beaker cost for a tech, from the normal research
	/// formula. TechCostFor subtracts beakers already put into the current
	/// research, so they are added back here.
	public int TechCost(Tech tech) {
		int cost = gameData.TechCostFor(tech, player);
		return player.currentlyResearchedTech == tech.id ? cost + player.beakers : cost;
	}

	/// The 0-based era of a tech (0 = ancient times).
	public int TechEraIndex(Tech tech)
		=> Player.EraIndex(tech.EraCivilopediaName);

	/// The hut tile's neighbor in direction index % 8, going clockwise from
	/// north (0 = N, 1 = NE, ..., 7 = NW), or null (nil in Lua) when that
	/// neighbor is off the map.
	public Tile NeighborAt(int directionIndex) {
		TileDirection direction = TileDirectionExtensions.All[((directionIndex % 8) + 8) % 8];
		if (tile.neighbors.TryGetValue(direction, out Tile neighbor) && Tile.IsTileValid(neighbor)) {
			return neighbor;
		}
		return null;
	}

	public UnitPrototype FindUnitPrototype(string name)
		=> gameData.unitPrototypes.Find(p => p.name == name);

	public Player barbarianPlayer => gameData.players.Find(p => p.isBarbarians);

	// ---- Actions ----

	public void GiveGold(int amount)
		=> player.gold += amount;

	/// Adds a tile to the player's map, without revealing its surroundings.
	public void RevealTile(Tile target) {
		if (Tile.IsTileValid(target)) {
			player.tileKnowledge.AddTileToKnown(target);
		}
	}

	/// Founds a city for the player on the hut's tile.
	public City FoundCity()
		=> CityInteractions.BuildCity(tile, player, player.GetNextCityName());

	public void AddCitizens(City city, int count) {
		for (int _ = 0; _ < count; ++_) {
			city.AddDefaultCitizen(gameData);
		}
	}

	/// Creates a unit of the named type. Throws if no such unit type exists,
	/// so a typo in a script fails loudly.
	public MapUnit SpawnUnit(Player owner, string unitTypeName, Tile target) {
		UnitPrototype proto = FindUnitPrototype(unitTypeName)
			?? throw new ArgumentException($"Unknown unit type '{unitTypeName}'");
		return gameData.SpawnUnit(owner, proto, target);
	}

	public MapUnit SpawnUnitOfType(Player owner, UnitPrototype proto, Tile target)
		=> gameData.SpawnUnit(owner, proto, target);

	public void GrantTech(Tech tech)
		=> player.AcquireTechs(gameData, new List<Tech> { tech });

	/// Shows a message to the player who popped the hut, if they are human.
	public void Notify(string message) {
		if (player.isHuman) {
			new MsgShowTemporaryPopup(message, tile).send();
		}
	}
}
