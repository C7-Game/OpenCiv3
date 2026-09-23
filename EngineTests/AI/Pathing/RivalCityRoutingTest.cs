using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Pathing;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI.Pathing;

/// <summary>
/// Regression tests for https://github.com/C7-Game/OpenCiv3/issues/371:
/// units should not route through rival cities. A city tile owned by another
/// civ must be excluded from the middle of a path, while still remaining a
/// valid destination for a combat unit that wants to attack it.
/// </summary>
public sealed class LandUnitAvoidsRivalCityTest : MapBase {
	// A 3x4 patch of plains tiles arranged so that the direct line from the
	// start to the destination passes through the rival city, but the city
	// can be flanked along the top or bottom row:
	//      (48,48)  (50,48)  (52,48)  (54,48)
	//      (48,50)  (50,50)  (52,50)  (54,50)
	//      (48,52)  (50,52)  (52,52)  (54,52)
	// start = (50,50), rival city = (52,50), destination = (54,50)
	private readonly List<Tile> grid = new();
	private readonly Player human;
	private readonly Player rival;
	private readonly Tile cityTile;
	private readonly Tile destinationTile;

	public LandUnitAvoidsRivalCityTest() {
		int[,] coordinates = {
			{ 48, 48 }, { 50, 48 }, { 52, 48 }, { 54, 48 },
			{ 48, 50 }, { 50, 50 }, { 52, 50 }, { 54, 50 },
			{ 48, 52 }, { 50, 52 }, { 52, 52 }, { 54, 52 },
		};

		for (int i = 0; i < coordinates.GetLength(0); i++) {
			Tile tile = MakePlainsTile();
			tile.XCoordinate = coordinates[i, 0];
			tile.YCoordinate = coordinates[i, 1];
			grid.Add(tile);
		}

		startTile = grid.Single(t => t.XCoordinate == 50 && t.YCoordinate == 50);
		cityTile = grid.Single(t => t.XCoordinate == 52 && t.YCoordinate == 50);
		destinationTile = grid.Single(t => t.XCoordinate == 54 && t.YCoordinate == 50);

		ComputeAllNeighbors(grid.ToHashSet());

		human = MakeHumanPlayer("human-1");
		rival = MakeCivPlayer("rival-1");

		// The human has explored the whole patch, including the rival city.
		foreach (Tile tile in grid) {
			human.tileKnowledge.knownTiles.Add(tile);
		}

		// Belongs to a rival civ.
		cityTile.cityAtTile = new City(cityTile, rival, "Rival City", ID.None("city"));
	}

	private static Player MakeCivPlayer(string id) {
		Player player = new Player();
		player.id = ID.FromString(id);
		player.civilization = new Civilization();
		return player;
	}

	private static Player MakeHumanPlayer(string id) {
		Player player = MakeCivPlayer(id);
		player.isHuman = true;
		return player;
	}

	private MapUnit MakeLandUnitOnStart(Player owner, bool combat) {
		MapUnit unit = MakeLandUnit(2);
		if (combat) {
			unit.unitType.attack = 1;
		}
		unit.owner = owner;
		unit.location = startTile;
		startTile.unitsOnTile.Add(unit);
		return unit;
	}

	private static TilePath ComputePath(MapUnit unit, Tile destination) {
		return PathingAlgorithmChooser.GetAlgorithm(unit).PathFrom(unit.location, destination, unit);
	}

	[Fact]
	private void TestHumanCombatUnitDoesNotPathThroughRivalCityAtPeace() {
		EngineStorage.InitializeGameDataForTests(new C7GameData.GameData(1234));
		MapUnit unit = MakeLandUnitOnStart(human, combat: true);

		TilePath path = ComputePath(unit, destinationTile);

		// It can still reach the destination...
		Assert.NotEmpty(path.path);
		Assert.Contains(destinationTile, path.path);
		// ...but must not route through the rival city.
		Assert.DoesNotContain(cityTile, path.path);
	}

	[Fact]
	private void TestHumanCombatUnitDoesNotPathThroughRivalCityAtWar() {
		EngineStorage.InitializeGameDataForTests(new C7GameData.GameData(1235));
		human.DeclareWarOn(rival, currentTurn: 1);
		MapUnit unit = MakeLandUnitOnStart(human, combat: true);

		TilePath path = ComputePath(unit, destinationTile);

		Assert.NotEmpty(path.path);
		Assert.Contains(destinationTile, path.path);
		Assert.DoesNotContain(cityTile, path.path);
	}

	[Fact]
	private void TestAiCombatUnitDoesNotPathThroughRivalCityAtPeace() {
		EngineStorage.InitializeGameDataForTests(new C7GameData.GameData(1236));
		Player aiPlayer = MakeCivPlayer("ai-1");
		MapUnit unit = MakeLandUnitOnStart(aiPlayer, combat: true);

		TilePath path = ComputePath(unit, destinationTile);

		Assert.NotEmpty(path.path);
		Assert.Contains(destinationTile, path.path);
		Assert.DoesNotContain(cityTile, path.path);
	}

	[Fact]
	private void TestAiCombatUnitDoesNotPathThroughRivalCityAtWar() {
		EngineStorage.InitializeGameDataForTests(new C7GameData.GameData(1237));
		Player aiPlayer = MakeCivPlayer("ai-1");
		aiPlayer.DeclareWarOn(rival, currentTurn: 1);
		MapUnit unit = MakeLandUnitOnStart(aiPlayer, combat: true);

		TilePath path = ComputePath(unit, destinationTile);

		Assert.NotEmpty(path.path);
		Assert.Contains(destinationTile, path.path);
		Assert.DoesNotContain(cityTile, path.path);
	}

	[Fact]
	private void TestAiSettlerDoesNotPathThroughRivalCity() {
		// The scenario described in #371: an AI settler routes through Athens.
		EngineStorage.InitializeGameDataForTests(new C7GameData.GameData(1240));
		Player aiPlayer = MakeCivPlayer("ai-1");
		MapUnit unit = MakeLandUnitOnStart(aiPlayer, combat: false);

		TilePath path = ComputePath(unit, destinationTile);

		// A non-combat unit cannot enter the rival city at all, so no path
		// through it (or to tiles beyond it) is possible.
		Assert.DoesNotContain(cityTile, path.path);
	}

	[Fact]
	private void TestCombatUnitCanTargetRivalCityAsDestination() {
		// Attacking a rival city is legitimate - only traversal is banned.
		EngineStorage.InitializeGameDataForTests(new C7GameData.GameData(1238));
		Player aiPlayer = MakeCivPlayer("ai-1");
		aiPlayer.DeclareWarOn(rival, currentTurn: 1);
		MapUnit unit = MakeLandUnitOnStart(aiPlayer, combat: true);

		TilePath path = ComputePath(unit, cityTile);

		// A combat unit should be able to move onto (attack) a rival city.
		Assert.NotEmpty(path.path);
		Assert.Contains(cityTile, path.path);
	}

	[Fact]
	private void TestNonCombatUnitCannotEnterRivalCityEvenAsDestination() {
		EngineStorage.InitializeGameDataForTests(new C7GameData.GameData(1239));
		Player aiPlayer = MakeCivPlayer("ai-1");
		MapUnit unit = MakeLandUnitOnStart(aiPlayer, combat: false);

		TilePath path = ComputePath(unit, cityTile);

		// A settler or worker cannot be ordered into a rival city at all.
		Assert.Empty(path.path);
	}
}
