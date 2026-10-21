using System.Linq;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// End to end: destroying a capital in a real generated game moves the palace.
// The choice of city itself is tested in CapitalRelocationTest.
public class LosingCapitalTest : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;
	private readonly Player player;

	public LosingCapitalTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		player = gameData.players.First(p => p.isHuman);
	}

	// Founds a city on an empty land tile a few tiles from the player's start.
	private City Found(string name, int minDistance) {
		Tile start = player.units[0].location;
		Tile site = gameData.map.tiles
			.Where(t => t.IsLand() && t.cityAtTile == null && t.DistanceTo(start) >= minDistance)
			.Where(t => gameData.cities.All(c => c.location.DistanceTo(t) >= 3))
			.OrderBy(t => t.DistanceTo(start))
			.First();
		return CityInteractions.BuildCity(site, player, name);
	}

	private Building Palace => gameData.Buildings.Find(b => b.isCenterOfEmpire);

	[Fact]
	public void DestroyingTheCapitalMovesThePalaceToTheBestCity() {
		City capital = Found("Capital", 0);
		City small = Found("Small", 3);
		City big = Found("Big", 3);
		for (int i = 0; i < 4; i++) {
			big.AddCitizen(new CityResident { city = big, citizenType = gameData.citizenTypes.Find(x => x.IsDefaultCitizen) });
		}
		Assert.True(capital.IsCapital());

		CityInteractions.DestroyCity(capital);

		Assert.DoesNotContain(capital, player.cities);
		Assert.True(big.IsCapital());
		Assert.False(small.IsCapital());
		Assert.Contains(big.constructed_buildings, b => b.building == Palace);
		Assert.Single(player.cities, c => c.IsCapital());
		// Corruption is worked out from the new capital.
		Assert.Contains(big, player.citiesWithCorruptionWonders);
	}

	[Fact]
	public void DestroyingAnotherCityLeavesTheCapitalAlone() {
		City capital = Found("Capital", 0);
		City other = Found("Other", 3);

		CityInteractions.DestroyCity(other);

		Assert.True(capital.IsCapital());
		Assert.Single(player.cities, c => c.IsCapital());
	}

	[Fact]
	public void DestroyingTheLastCityLeavesNoCapital() {
		City capital = Found("Capital", 0);

		CityInteractions.DestroyCity(capital);

		Assert.Empty(player.cities);
	}
}
