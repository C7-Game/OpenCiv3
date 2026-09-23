using C7Engine;
using C7GameData;
using C7GameData.AIData;
using C7GameData.Save;
using EngineTests.Utils;
using System.Collections.Generic;
using Xunit;

namespace EngineTests.AI.UnitAI;

/// <summary>
/// Tests for https://github.com/C7-Game/OpenCiv3/issues/213: an AI settler
/// whose path destination becomes unreachable must not stay stuck forever.
/// The original crash ("Could not get next part of path") is gone, but
/// FindSettlerLocation still re-picks a destination the settler can no longer
/// reach (e.g. because a rival unit parked on it), so the settler re-evaluates
/// the same impossible task every turn.
/// </summary>
public sealed class SettlerDestinationBlockedTest : MapBase {
	// start = (50,50), destination = (52,50), one tile east
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
		AddNeighborsAndUpdateMap(start, destination, TileDirection.EAST);
		AddNeighborsAndUpdateMap(destination, start, TileDirection.WEST);

		aiPlayer = MakeAiPlayer();
		rival = MakeCivPlayer();

		foreach (Tile tile in new List<Tile> { start, destination }) {
			aiPlayer.tileKnowledge.knownTiles.Add(tile);
		}

		// The AI already has a home, so its settler goes looking for a spot.
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

	[Fact]
	private void SettlerDoesNotRetargetTheUnreachableDestination() {
		EngineStorage.InitializeGameDataForTests(new C7GameData.GameData(1));

		// Sanity check: with the destination clear, it is the chosen spot.
		Assert.Equal(destination, SettlerLocationAI.FindSettlerLocation(start, aiPlayer));

		// A rival unit parks on the destination (the #213 scenario).
		ParkRivalUnitOnDestination();

		// It must no longer be chosen, or the settler will re-evaluate this
		// same impossible task forever.
		Tile chosen = SettlerLocationAI.FindSettlerLocation(start, aiPlayer);
		Assert.NotEqual(destination, chosen);
	}

	[Fact]
	private void SettlerFailsGracefullyWhenDestinationBlockedMidJourney() {
		EngineStorage.InitializeGameDataForTests(new C7GameData.GameData(2));

		MapUnit settler = MakeSettlerOnStart();
		settler.movementPoints.reset(settler.unitType.movement);

		// The settler heads off while the destination is still clear.
		SettlerAIData data = SettlerAI.MakeAiData(settler, aiPlayer);
		Assert.Equal(SettlerAIData.SettlerGoal.BUILD_CITY, data.goal);
		Assert.Equal(destination, data.destination);
		Assert.NotEmpty(data.pathToDestination.path);

		// The destination gets blocked while en route.
		ParkRivalUnitOnDestination();

		SettlerAI settlerAi = new SettlerAI(data);
		C7GameData.UnitAI.MoveResult result = settlerAi.TryToMoveAlongPath(settler, ref data.pathToDestination);

		// The move fails gracefully (no exception) and the repath also cannot
		// reach the blocked destination.
		Assert.Equal(C7GameData.UnitAI.Result.Error, result.Result);
		Assert.Equal(Tile.NONE, data.pathToDestination.Next());
	}
}
