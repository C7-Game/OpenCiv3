using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using Xunit;

namespace EngineTests.GameData;

public class CityBuildingYieldsTest {
	private static City MakeCity() {
		C7GameData.GameData gameData = new() {
			gameDifficulty = new Difficulty(),
		};
		EngineStorage.InitializeGameDataForTests(gameData);
		Player player = new() {
			isHuman = true,
			civilization = new Civilization(),
		};
		City city = new City(Tile.NONE, player, "Yield City", ID.None("city"));
		player.cities.Add(city);
		return city;
	}

	private static Building MakeBuilding(string name, int culturePerTurn, int maintenanceCost) {
		SaveBuilding save = new() {
			name = name,
			culturePerTurn = culturePerTurn,
			maintenanceCost = maintenanceCost,
		};
		return new Building(save, EngineStorage.gameData);
	}

	[Fact]
	public void PerBuildingYields_NegativeGoldWhenBuildingCostsMaintenance() {
		City city = MakeCity();
		Building library = MakeBuilding("Library", culturePerTurn: 4, maintenanceCost: 1);
		city.AddBuilding(library);

		(Building building, int commerce, int culture) = city.GetPerBuildingYields().Single();

		Assert.Same(library, building);
		Assert.Equal(-1, commerce);
		Assert.Equal(4, culture);
	}

	[Fact]
	public void PerBuildingYields_PositiveGoldWhenBuildingGeneratesCommerce() {
		City city = MakeCity();
		Building wonder = MakeBuilding("Wonder", culturePerTurn: 0, maintenanceCost: -2);
		city.AddBuilding(wonder);

		(Building building, int commerce, int culture) = city.GetPerBuildingYields().Single();

		Assert.Same(wonder, building);
		Assert.Equal(2, commerce);
		Assert.Equal(0, culture);
	}

	[Fact]
	public void PerBuildingYields_ReturnsOneEntryPerBuilding() {
		City city = MakeCity();
		city.AddBuilding(MakeBuilding("A", 1, 0));
		city.AddBuilding(MakeBuilding("B", 0, 3));

		Assert.Equal(2, city.GetPerBuildingYields().Count());
	}
}
