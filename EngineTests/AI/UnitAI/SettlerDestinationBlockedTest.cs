using C7Engine;
using C7GameData;
using C7GameData.AIData;
using C7GameData.Save;
using EngineTests.Utils;
using System.Collections.Generic;
using Xunit;

namespace EngineTests.AI.UnitAI;

/// <summary>
/// Tests for issue #213: a settler whose destination becomes unreachable while
/// en route (e.g. a rival unit parks on it) picks a new destination instead of
/// re-evaluating the same impossible task every turn.
/// Selection does not reject a tile just because a foreign unit sits on it.
/// The tile is excluded only when the settler actually fails to reach it
/// (SettlerAI.FindNewDestination). These tests call that seam directly, since
/// PlayTurn cannot move units in the reduced test map.
/// </summary>
public sealed class SettlerDestinationBlockedTest : MapBase {
	// start = (50,50), destination = (52,50), one tile east of start
	private readonly Player aiPlayer;
	private readonly Player rival;
	private readonly Tile start;
	private readonly Tile destination;

	public SettlerDestinationBlockedTest() {
		InitilizeStartTile(MakeDesertTile(), new TileLocation(50, 50));
		start = startTile;

		destination = MakeHillTile();
		destination.XCoordinate = 52;
		destination.YCoordinate = 50;
		// Extra production so the destination clearly beats the hill alternative.
		destination.overlayTerrainType.baseShieldProduction = 2;
		AddNeighborsAndUpdateMap(start, destination, TileDirection.EAST);
		AddNeighborsAndUpdateMap(destination, start, TileDirection.WEST);

		aiPlayer = MakeAiPlayer();
		rival = MakeCivPlayer();

		foreach (Tile tile in new List<Tile> { start, destination }) {
			aiPlayer.tileKnowledge.knownTiles.Add(tile);
		}

		// With a home city, the settler goes looking for a spot.
		Tile homeTile = MakePlainsTile();
		homeTile.XCoordinate = 10;
		homeTile.YCoordinate = 10;
		aiPlayer.cities.Add(new City(homeTile, aiPlayer, "Home", ID.None("")));
	}

	private static Player MakeCivPlayer() {
		Player player = new Player();
		player.id = ID.FromString("rival-1");
		player.civilization = new Civilization();
		return player;
	}

	private static Player MakeAiPlayer() {
		Player player = new Player();
		player.id = ID.FromString("ai-1");
		player.civilization = new Civilization();
		player.government = new Government();
		player.rules = MakeTestRules();
		return player;
	}

	private MapUnit MakeSettlerOnStart() {
		MapUnit settler = MakeLandUnit(1);
		settler.unitType.name = "Settler";
		settler.owner = aiPlayer;
		settler.nationality = aiPlayer.civilization;
		settler.location = start;
		start.unitsOnTile.Add(settler);
		aiPlayer.units.Add(settler);
		return settler;
	}

	private void ParkRivalUnitOnDestination() {
		MapUnit blocker = MakeLandUnit(1);
		blocker.unitType.attack = 1;
		blocker.owner = rival;
		blocker.location = destination;
		destination.unitsOnTile.Add(blocker);
	}

	// A second hills candidate at (46,50), three hops west of the destination, so it stays
	// reachable when the destination is excluded.
	private Tile AddAlternativeCandidateWestOfStart() {
		Tile midWest = MakePlainsTile();
		AddNeighborsAndUpdateMap(start, midWest, TileDirection.WEST);      // midWest = (48,50)
		AddNeighborsAndUpdateMap(midWest, start, TileDirection.EAST);

		Tile alternativeWest = MakeHillTile();
		AddNeighborsAndUpdateMap(midWest, alternativeWest, TileDirection.WEST);  // alternativeWest = (46,50)
		AddNeighborsAndUpdateMap(alternativeWest, midWest, TileDirection.EAST);

		aiPlayer.tileKnowledge.knownTiles.Add(midWest);
		aiPlayer.tileKnowledge.knownTiles.Add(alternativeWest);
		return alternativeWest;
	}

	[Fact]
	private void ForeignOccupiedTileIsNotRejectedUntilItIsUnreachable() {
		EngineStorage.InitializeGameDataForTests(new C7GameData.GameData(3));

		Assert.Equal(destination, SettlerLocationAI.FindSettlerLocation(start, aiPlayer));

		// Still chosen with a unit parked on it; the exclusion set is what filters.
		ParkRivalUnitOnDestination();
		Assert.Equal(destination, SettlerLocationAI.FindSettlerLocation(start, aiPlayer));

		HashSet<Tile> excluded = new HashSet<Tile> { destination };
		Tile chosen = SettlerLocationAI.FindSettlerLocation(start, aiPlayer, excluded);
		Assert.NotEqual(destination, chosen);
	}

	[Fact]
	private void SettlerRetargetsWhenDestinationBecomesUnreachable() {
		EngineStorage.InitializeGameDataForTests(new C7GameData.GameData(4));

		Tile alternative = AddAlternativeCandidateWestOfStart();

		MapUnit settler = MakeSettlerOnStart();
		settler.movementPoints.reset(settler.unitType.movement);

		SettlerAIData data = SettlerAI.MakeAiData(settler, aiPlayer);
		Assert.Equal(SettlerAIData.SettlerGoal.BUILD_CITY, data.goal);
		Assert.Equal(destination, data.destination);

		ParkRivalUnitOnDestination();

		SettlerAI settlerAi = new SettlerAI(data);
		settler.currentAI = settlerAi;
		C7GameData.UnitAI.MoveResult result = settlerAi.FindNewDestination(settler, aiPlayer);

		Assert.Equal(C7GameData.UnitAI.Result.InProgress, result.Result);
		Assert.Contains(destination, data.unreachableDestinations);
		Assert.Equal(alternative, data.destination);
		Assert.Equal(SettlerAIData.SettlerGoal.BUILD_CITY, data.goal);
		Assert.NotEmpty(data.pathToDestination.path);
		Assert.False(settler.movementPoints.canMove);
	}

	[Fact]
	private void SettlerGivesUpWhenNoReachableDestinationRemains() {
		EngineStorage.InitializeGameDataForTests(new C7GameData.GameData(5));

		MapUnit settler = MakeSettlerOnStart();
		settler.movementPoints.reset(settler.unitType.movement);

		SettlerAIData data = SettlerAI.MakeAiData(settler, aiPlayer);
		Assert.Equal(destination, data.destination);
		ParkRivalUnitOnDestination();

		// Only the blocked tile and the start (too close to it) are known.
		SettlerAI settlerAi = new SettlerAI(data);
		settler.currentAI = settlerAi;
		C7GameData.UnitAI.MoveResult result = settlerAi.FindNewDestination(settler, aiPlayer);

		Assert.Equal(C7GameData.UnitAI.Result.InProgress, result.Result);
		Assert.Contains(destination, data.unreachableDestinations);
		Assert.Equal(SettlerAIData.SettlerGoal.JOIN_CITY, data.goal);
		Assert.False(settler.movementPoints.canMove);
	}

	[Fact]
	private void SettlerFailsGracefullyWhenDestinationBlockedMidJourney() {
		EngineStorage.InitializeGameDataForTests(new C7GameData.GameData(2));

		MapUnit settler = MakeSettlerOnStart();
		settler.movementPoints.reset(settler.unitType.movement);

		SettlerAIData data = SettlerAI.MakeAiData(settler, aiPlayer);
		Assert.Equal(SettlerAIData.SettlerGoal.BUILD_CITY, data.goal);
		Assert.Equal(destination, data.destination);
		Assert.NotEmpty(data.pathToDestination.path);

		ParkRivalUnitOnDestination();

		SettlerAI settlerAi = new SettlerAI(data);
		C7GameData.UnitAI.MoveResult result = settlerAi.TryToMoveAlongPath(settler, ref data.pathToDestination);

		// Fails without exception; SettlerAI turns this Error into a retarget.
		Assert.Equal(C7GameData.UnitAI.Result.Error, result.Result);
		Assert.Equal(Tile.NONE, data.pathToDestination.Next());
	}
}
