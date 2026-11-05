using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class GoodyHutPopTest : IClassFixture<SaveGameFixture> {
	private static readonly HashSet<string> AllOutcomes = [
		"gold", "maps", "city", "nothing", "settlers", "mercenaries", "tech", "barbarians",
	];

	private readonly C7GameData.GameData gameData;
	private readonly Player human;
	private readonly MapUnit unit;

	public GoodyHutPopTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);

		human = gameData.players.First(p => p.isHuman);
		unit = human.units.First();
	}

	private GoodyHutContext Context(Tile tile = null, MapUnit popper = null) {
		return new GoodyHutContext(gameData, popper ?? unit, tile ?? unit.location);
	}

	private bool CanApply(string outcome, GoodyHutContext context) {
		return gameData.luaBehaviorEngine.ImportFunc<Func<GoodyHutContext, bool>>($"goody_huts.outcomes.{outcome}.can_apply")(context);
	}

	private void Apply(string outcome, GoodyHutContext context) {
		gameData.luaBehaviorEngine.ImportFunc<Action<GoodyHutContext>>($"goody_huts.outcomes.{outcome}.apply")(context);
	}

	private City FoundCityNextTo(Tile tile, int distance) {
		Tile site = gameData.map.tiles.First(t => t.IsLand() && t.ManhattanDistanceTo(tile) == distance && t.IsAllowCities());
		return CityInteractions.BuildCity(site, human, human.GetNextCityName());
	}

	// The starting units (settler and worker) can't attack or defend.
	private MapUnit GiveCombatUnit() {
		return gameData.SpawnUnit(human, gameData.barbarianInfo.basicBarbarian, unit.location);
	}

	private void SetExpansionist(bool expansionist) {
		if (expansionist) {
			human.civilization.traits.Add(Civilization.Trait.Expansionist);
		} else {
			human.civilization.traits.Remove(Civilization.Trait.Expansionist);
		}
	}

	[Fact]
	public void EnteringAHutTilePopsIt() {
		Tile tile = unit.location;
		tile.hasGoodyHut = true;

		unit.OnEnterTile(tile);

		Assert.False(tile.hasGoodyHut);
	}

	[Fact]
	public void BarbariansDoNotPopHuts() {
		Player barbarians = gameData.players.First(p => p.isBarbarians);
		Tile tile = gameData.map.tiles.First(t => t.IsLand() && t.unitsOnTile.Count == 0);
		MapUnit barbarian = gameData.SpawnUnit(barbarians, gameData.barbarianInfo.basicBarbarian, tile);
		tile.hasGoodyHut = true;

		barbarian.OnEnterTile(tile);

		Assert.True(tile.hasGoodyHut);
	}

	[Fact]
	public void PoppingAlwaysGivesAKnownOutcome() {
		for (int i = 0; i < 100; ++i) {
			Tile tile = unit.location;
			tile.hasGoodyHut = true;

			string outcome = GoodyHutInteractions.PopHut(gameData, unit, tile);

			Assert.Contains(outcome, AllOutcomes);
			Assert.False(tile.hasGoodyHut);
		}
	}

	// With no cities, the city and barbarian outcomes can't happen, so the
	// roll must never pick them.
	[Fact]
	public void OutcomesWhosePreconditionsFailAreNeverRolled() {
		Assert.Empty(human.cities);

		for (int i = 0; i < 200; ++i) {
			string outcome = GoodyHutInteractions.PopHut(gameData, unit, unit.location);
			Assert.NotEqual("city", outcome);
			Assert.NotEqual("barbarians", outcome);
		}
	}

	[Fact]
	public void GoldIsSmallerEarlyInTheGame() {
		GoodyHutContext context = Context();

		gameData.turn = 10;
		int before = human.gold;
		Apply("gold", context);
		Assert.Equal(before + 25, human.gold);

		gameData.turn = 60;
		before = human.gold;
		Apply("gold", context);
		Assert.Equal(before + 50, human.gold);
	}

	[Fact]
	public void MapsRevealTilesAroundTheHut() {
		int before = human.tileKnowledge.knownTiles.Count;

		Apply("maps", Context());

		Assert.True(human.tileKnowledge.knownTiles.Count > before);
	}

	[Fact]
	public void SettlersAppearOnTheHut() {
		Tile tile = unit.location;
		int before = tile.unitsOnTile.Count;

		Apply("settlers", Context());

		Assert.Equal(before + 1, tile.unitsOnTile.Count);
		Assert.Contains(tile.unitsOnTile, u => u.unitType.name == "Settler" && u.owner == human);
	}

	[Fact]
	public void OneMercenaryAppearsOnTheHut() {
		Tile hut = gameData.map.tiles.First(t => t.IsLand() && t.unitsOnTile.Count == 0
			&& t.neighbors.Values.All(n => n.unitsOnTile.Count == 0));

		Apply("mercenaries", Context(hut));

		MapUnit mercenary = Assert.Single(hut.unitsOnTile);
		Assert.Equal(human, mercenary.owner);
		Assert.Equal(gameData.barbarianInfo.basicBarbarian, mercenary.unitType);
		Assert.All(hut.neighbors.Values, n => Assert.Empty(n.unitsOnTile));
	}

	[Fact]
	public void TechIsAnUnknownEarlyTechWithKnownPrerequisites() {
		HashSet<ID> knownBefore = human.knownTechs.ToHashSet();
		GoodyHutContext context = Context();
		Assert.True(CanApply("tech", context));

		Apply("tech", context);

		Tech granted = gameData.techs.Single(t => human.knownTechs.Contains(t.id) && !knownBefore.Contains(t.id));
		Assert.True(Player.EraIndex(granted.EraCivilopediaName) <= 1);
		Assert.All(granted.Prerequisites, p => Assert.Contains(p.id, knownBefore));
	}

	[Fact]
	public void NoTechsOnceThePlayerLeavesTheFirstEra() {
		human.eraCivilopediaName = "ERAS_Middle_Ages";

		Assert.False(CanApply("tech", Context()));
	}

	[Fact]
	public void BarbariansNeedACityThatIsNotTooClose() {
		SetExpansionist(false);
		GiveCombatUnit();
		Tile hut = gameData.map.tiles.First(t => t.IsLand() && t.unitsOnTile.Count == 0 && t.neighbors.Values.All(n => n.IsLand()));

		// No cities at all.
		Assert.False(CanApply("barbarians", Context(hut)));

		// A city right next to the hut is too close.
		City near = FoundCityNextTo(hut, 1);
		Assert.False(CanApply("barbarians", Context(hut)));

		// A city 2 tiles away is far enough.
		CityInteractions.DestroyCity(near);
		FoundCityNextTo(hut, 2);
		Assert.True(CanApply("barbarians", Context(hut)));
	}

	[Fact]
	public void NoBarbariansForExpansionistsOrExplorers() {
		GiveCombatUnit();
		Tile hut = gameData.map.tiles.First(t => t.IsLand() && t.unitsOnTile.Count == 0 && t.neighbors.Values.All(n => n.IsLand()));
		FoundCityNextTo(hut, 5);

		SetExpansionist(true);
		Assert.False(CanApply("barbarians", Context(hut)));

		SetExpansionist(false);
		MapUnit explorer = gameData.SpawnUnit(human, gameData.unitPrototypes.First(p => p.name == "Explorer"), hut);
		Assert.True(explorer.unitType.allTerrainAsRoads);
		Assert.False(CanApply("barbarians", Context(hut, explorer)));
	}

	[Fact]
	public void NoBarbariansForPlayersWithoutCombatUnits() {
		SetExpansionist(false);
		Tile hut = gameData.map.tiles.First(t => t.IsLand() && t.unitsOnTile.Count == 0 && t.neighbors.Values.All(n => n.IsLand()));
		FoundCityNextTo(hut, 5);

		// Only the starting settler and worker.
		Assert.DoesNotContain(human.units, u => u.IsCombatUnit());
		Assert.False(CanApply("barbarians", Context(hut)));

		GiveCombatUnit();
		Assert.True(CanApply("barbarians", Context(hut)));
	}

	[Fact]
	public void BarbariansAppearNextToTheHut() {
		Player barbarians = gameData.players.First(p => p.isBarbarians);
		Tile hut = gameData.map.tiles.First(t => t.IsLand() && t.unitsOnTile.Count == 0 && t.neighbors.Values.All(n => n.IsLand() && n.unitsOnTile.Count == 0));

		Apply("barbarians", Context(hut));

		int spawned = hut.neighbors.Values.Sum(n => n.unitsOnTile.Count(u => u.owner == barbarians));
		Assert.InRange(spawned, 1, 3);
	}

	// Civ3 withholds cities from players with more cities than the world
	// average (world cities / living civs).
	[Fact]
	public void NoCitiesForPlayersAheadOfTheAverage() {
		SetExpansionist(true);
		Tile hut = gameData.map.tiles.First(t => t.IsLand() && t.IsAllowCities() && t.unitsOnTile.Count == 0);
		FoundCityNextTo(hut, 6);

		// One city among 8 civs: the average rounds down to 0, so we're ahead.
		Assert.True(gameData.players.Count(p => !p.isBarbarians && !p.defeated) > 1);
		Assert.False(CanApply("city", Context(hut)));
	}

	[Fact]
	public void CitiesOnlyForExpansionistsFarFromTheirOtherCities() {
		// Leave the human as the only civ, so their one city is the average.
		foreach (Player p in gameData.players.Where(p => !p.isBarbarians && p != human)) {
			p.defeated = true;
		}
		Tile hut = gameData.map.tiles.First(t => t.IsLand() && t.IsAllowCities() && t.unitsOnTile.Count == 0);
		FoundCityNextTo(hut, 6);

		SetExpansionist(false);
		Assert.False(CanApply("city", Context(hut)));

		SetExpansionist(true);
		Assert.True(CanApply("city", Context(hut)));

		Apply("city", Context(hut));
		Assert.True(hut.HasCity());
		Assert.Equal(human, hut.cityAtTile.owner);
	}

	[Fact]
	public void ManhattanDistanceCountsCornerNeighborsAsTwo() {
		Tile tile = gameData.map.tiles.First(t => t.neighbors.Values.All(Tile.IsTileValid));

		Assert.Equal(1, tile.ManhattanDistanceTo(tile.neighbors[TileDirection.NORTHEAST]));
		Assert.Equal(2, tile.ManhattanDistanceTo(tile.neighbors[TileDirection.NORTH]));
		Assert.Equal(2, tile.ManhattanDistanceTo(tile.neighbors[TileDirection.EAST]));
	}
}
