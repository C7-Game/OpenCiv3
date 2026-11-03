using System.Collections.Generic;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

public class CityTileAssignmentAITest : MapBase {
	private readonly CitizenType laborer = new() { IsDefaultCitizen = true };
	private readonly CitizenType entertainer = new() { Luxuries = 2 };

	private (C7GameData.GameData gameData, City city, List<Tile> surroundings) MakeCity() {
		C7GameData.GameData gameData = new() {
			gameDifficulty = new Difficulty { NumberOfCitizensBornContent = 0 },
			rules = new Rules { MaximumLevel1CitySize = 6, MaximumLevel2CitySize = 12 },
		};
		gameData.citizenTypes.AddRange([laborer, entertainer]);
		EngineStorage.InitializeGameDataForTests(gameData);

		Player player = MakeTestPlayer([]);
		// The mood calculation looks up the player's cities in the trade network.
		gameData.players.Add(player);
		player.rules = new Rules { MaxRankOfWorkableTiles = 1, MaximumLevel1CitySize = 6, MaximumLevel2CitySize = 12 };

		InitilizeStartTile(MakeDesertTile(), new TileLocation(10, 10));
		Tile center = startTile;
		List<Tile> tiles = SurroundTile(center, MakeDesertTile);
		InitPartialGameMap(20, 20, tiles);

		City city = new City(center, player, "Ironton", ID.None("city"));
		center.cityAtTile = city;
		player.cities.Add(city);
		foreach (Tile t in tiles) {
			t.owningCity = city;
		}
		tiles.Remove(center);
		return (gameData, city, tiles);
	}

	private CityResident AddWorker(City city, Tile tile = null) {
		CityResident resident = new() { citizenType = laborer, city = city, nationality = city.owner.civilization };
		city.AddCitizen(resident);
		if (tile != null) {
			resident.tileWorked = tile;
			tile.personWorkingTile = resident;
		}
		return resident;
	}

	[Fact]
	public void FoodTargetPlansForCitizensNotYetPlaced() {
		(_, City city, _) = MakeCity();
		AddWorker(city);

		// The city centre makes 2 food. One citizen needs 2 food plus the
		// desired surplus, so the unplaced citizen has to cover the rest.
		int surplus = CityTileAssignmentAI.DesiredFoodSurplusPerTurn;
		Assert.Equal(2 + surplus - 2, CityTileAssignmentAI.TargetTileFoodAmount(city, 0));

		// While rebuilding a 4 citizen city, the target must account for all
		// four rather than just the one added so far.
		Assert.Equal(4 * 2 + surplus - 2, CityTileAssignmentAI.TargetTileFoodAmount(city, 4));

		// Passing fewer citizens than the city already has can't lower the target.
		Assert.Equal(2 + surplus - 2, CityTileAssignmentAI.TargetTileFoodAmount(city, 1));
	}

	// An unhappy city needs an entertainer. It should come from the worker on
	// the least valuable tile, not whoever happened to be placed last, or the
	// citizen placed to cover the city's food gets undone while a worker on a
	// food-less hill stays and the city starves.
	[Fact]
	public void UnhappyCity_MakesEntertainerOfWorkerOnLeastValuableTile() {
		(C7GameData.GameData gameData, City city, List<Tile> surroundings) = MakeCity();
		Tile hill = surroundings[0];
		hill.overlayTerrainType.baseShieldProduction = 3;
		Tile grassland = surroundings[1];
		grassland.overlayTerrainType.baseFoodProduction = 2;

		CityResident hillWorker = AddWorker(city, hill);
		CityResident newResident = AddWorker(city);

		CityTileAssignmentAI.AssignNewCitizenToTile(gameData, newResident, manageMoods: true, totalResidents: 2);

		// The new citizen takes the grassland and keeps it.
		Assert.Same(grassland, newResident.tileWorked);
		Assert.Same(newResident, grassland.personWorkingTile);
		Assert.True(newResident.citizenType.IsDefaultCitizen);

		// The worker on the hill becomes the entertainer and frees the tile.
		Assert.Same(Tile.NONE, hillWorker.tileWorked);
		Assert.Null(hill.personWorkingTile);
		Assert.Same(entertainer, hillWorker.citizenType);
	}

	[Fact]
	public void UnhappyCity_MakesNewCitizenAnEntertainerWhenThereIsNoTileForThem() {
		(C7GameData.GameData gameData, City city, List<Tile> surroundings) = MakeCity();
		// Every surrounding tile yields nothing, so the new citizen finds no
		// tile worth working and is already a specialist before the mood check.
		CityResident newResident = AddWorker(city);

		CityTileAssignmentAI.AssignNewCitizenToTile(gameData, newResident, manageMoods: true, totalResidents: 1);

		Assert.Same(Tile.NONE, newResident.tileWorked);
		Assert.Same(entertainer, newResident.citizenType);
	}
}
