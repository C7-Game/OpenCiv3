using System.Collections.Generic;
using C7GameData;
using Xunit;

namespace EngineTests.GameData.Victory;

public class DominationVictoryTest {

	[Theory]
	[InlineData(60f, 60f, 60f, 60f, true)]   // exactly at both limits
	[InlineData(61f, 61f, 60f, 60f, true)]   // above both limits
	[InlineData(59f, 61f, 60f, 60f, false)]  // area just below limit
	[InlineData(61f, 59f, 60f, 60f, false)]  // population just below limit
	[InlineData(0f, 0f, 60f, 60f, false)]    // nowhere close
	public void HasVictory_RequiresBothAreaAndPopulationThresholds(
		float area, float population, float areaLimit, float populationLimit, bool expected) {

		var victory = new DominationVictory(areaLimit, populationLimit);
		var status = new VictoryStatus { DominationArea = area, DominationPopulation = population };

		Assert.Equal(expected, victory.HasVictory(status));
	}

	[Fact]
	public void Evaluate_ComputesAreaAndPopulationPercentagesForPlayer() {
		C7Engine.EngineStorage.InitializeGameDataForTests(new C7GameData.GameData());

		Player player = new() { civilization = new Civilization("Rome") };
		Player rival = new() { civilization = new Civilization("Greece") };

		// Player owns 1 city with 3 residents, world has 4 residents total.
		City playerCity = new City(Tile.NONE, player, "Roma", ID.None("city"));
		playerCity.residents.Add(new CityResident());
		playerCity.residents.Add(new CityResident());
		playerCity.residents.Add(new CityResident());
		player.cities.Add(playerCity);

		City rivalCity = new City(Tile.NONE, rival, "Athens", ID.None("city"));
		rivalCity.residents.Add(new CityResident());
		rival.cities.Add(rivalCity);

		// 2 of 4 map tiles counted for domination are owned by the player.
		TerrainType land = new TerrainType { Key = "grassland" };
		TerrainType ocean = new TerrainType { Key = "ocean" };

		Tile ownedTile1 = new Tile(ID.None("tile")) { baseTerrainType = land, owningCity = playerCity };
		Tile ownedTile2 = new Tile(ID.None("tile")) { baseTerrainType = land, owningCity = playerCity };
		Tile unownedLandTile = new Tile(ID.None("tile")) { baseTerrainType = land };
		Tile oceanTile = new Tile(ID.None("tile")) { baseTerrainType = ocean }; // not counted for domination

		// Add directly to knownTiles to avoid pulling in unrelated
		// visibility/exploration machinery that needs a full map.
		player.tileKnowledge.knownTiles.Add(ownedTile1);
		player.tileKnowledge.knownTiles.Add(ownedTile2);

		C7GameData.GameData gameData = new() {
			map = new GameMap()
		};
		gameData.map.tiles.AddRange(new List<Tile> { ownedTile1, ownedTile2, unownedLandTile, oceanTile });
		gameData.players.Add(player);
		gameData.players.Add(rival);
		gameData.cities.Add(playerCity);
		gameData.cities.Add(rivalCity);

		var victory = new DominationVictory(dominationAreaLimit: 50f, dominationPopulationLimit: 50f);
		VictoryStatus status = victory.Evaluate(player, gameData);

		// 2 owned out of 3 land tiles (ocean excluded) = 66% floored.
		Assert.Equal(66f, status.DominationArea);
		// 3 out of 4 total residents = 75%.
		Assert.Equal(75f, status.DominationPopulation);
	}

	[Fact]
	public void GenerateStatusRows_WithNoRivals_LeavesRivalColumnsBlank() {
		var victory = new DominationVictory(50f, 50f);
		var status = new VictoryStatus { DominationArea = 10f, DominationPopulation = 20f };

		var rows = new List<string[]>(victory.GenerateStatusRows(status, new List<VictoryStatus>()));

		Assert.Equal(2, rows.Count);
		// Area row: [label, limit, label, myValue, topRivalName, topRivalValue]
		Assert.Equal("", rows[0][4]);
		Assert.Equal("", rows[0][5]);
		Assert.Equal("", rows[1][4]);
		Assert.Equal("", rows[1][5]);
	}

	[Fact]
	public void GenerateStatusRows_PicksTheStrongestRivalPerRow() {
		var victory = new DominationVictory(50f, 50f);
		var status = new VictoryStatus { DominationArea = 10f, DominationPopulation = 10f };

		var weakRival = new VictoryStatus {
			Player = new Player { civilization = new Civilization("Weak") },
			DominationArea = 5f,
			DominationPopulation = 40f
		};
		var strongRival = new VictoryStatus {
			Player = new Player { civilization = new Civilization("Strong") },
			DominationArea = 30f,
			DominationPopulation = 5f
		};

		var rows = new List<string[]>(
			victory.GenerateStatusRows(status, new List<VictoryStatus> { weakRival, strongRival }));

		Assert.Equal("Strong", rows[0][4]); // top rival by area
		Assert.Equal("30", rows[0][5]);
		Assert.Equal("Weak", rows[1][4]);   // top rival by population
		Assert.Equal("40", rows[1][5]);
	}
}
