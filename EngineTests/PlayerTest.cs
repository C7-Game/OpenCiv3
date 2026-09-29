using C7Engine;
using C7GameData;
using C7GameData.Save;
using Xunit;

namespace EngineTests.GameData;

public class PlayerTest {
	private const int OptimalNumberOfCities = 12;

	private static Player CreatePlayerWithCities(int numCities) {
		C7GameData.GameData gameData = new() {
			gameDifficulty = new Difficulty(),
		};
		gameData.map.optimalNumberOfCities = OptimalNumberOfCities;
		EngineStorage.InitializeGameDataForTests(gameData);

		Player player = new() {
			isHuman = true,
			civilization = new Civilization(),
		};
		for (int i = 0; i < numCities; i++) {
			player.cities.Add(new City(Tile.NONE, player, $"City {i}", ID.None("city")));
		}
		return player;
	}

	private static Building CreateBuilding(int shieldCost, bool isCenterOfEmpire) {
		SaveBuilding saveBuilding = new() {
			name = isCenterOfEmpire ? "Palace" : "Library",
			shieldCost = shieldCost,
		};
		if (isCenterOfEmpire) {
			saveBuilding.flags.Add(SaveBuilding.Flag.IsCenterOfEmpire);
		}
		return new Building(saveBuilding, EngineStorage.gameData);
	}

	// Palace cost multiplier is 6 * cities / optimalCities (integer division),
	// clamped to [3, 10].
	[Theory]
	[InlineData(0, 300)]   // Clamped up to 3
	[InlineData(6, 300)]
	[InlineData(8, 400)]
	[InlineData(12, 600)]
	[InlineData(13, 600)]  // 78 / 12 = 6, integer division rounds down
	[InlineData(20, 1000)]
	[InlineData(30, 1000)] // Clamped down to 10
	public void PalaceCost_ScalesWithNumberOfCities(int numCities, int expectedCost) {
		Player player = CreatePlayerWithCities(numCities);
		Building palace = CreateBuilding(100, isCenterOfEmpire: true);

		Assert.Equal(expectedCost, player.ShieldCost(palace));
	}

	[Fact]
	public void NonPalaceBuildingCost_DoesNotScaleWithNumberOfCities() {
		Player player = CreatePlayerWithCities(30);
		Building library = CreateBuilding(80, isCenterOfEmpire: false);

		Assert.Equal(80, player.ShieldCost(library));
	}
}
