using C7GameData;
using Xunit;

namespace EngineTests.GameData;

public class CityPopulationTest {
	private static City MakeCity(int size, int foodStored) {
		Player player = new() { rules = new Rules { MaximumLevel1CitySize = 6, MaximumLevel2CitySize = 12 } };
		City city = new(Tile.NONE, player, "Testville", ID.None("city")) { foodStored = foodStored };
		for (int i = 0; i < size; i++) {
			city.residents.Add(new CityResident());
		}
		return city;
	}

	[Theory]
	[InlineData(1, 0, 10000)]
	[InlineData(1, 10, 15000)] // half of the 20 food box
	[InlineData(6, 0, 210000)]
	[InlineData(12, 0, 780000)]
	[InlineData(12, 20, 785000)] // size 12 uses the level 2 food box (40), so 20 food is half a box
	public void PopulationFollowsTheBaseGameFormula(int size, int foodStored, int expected) {
		Assert.Equal(expected, MakeCity(size, foodStored).Population);
	}
}
