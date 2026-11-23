using System.Collections.Generic;
using System.Linq;
using C7GameData;
using Xunit;

namespace EngineTests.GameData.Victory;

public class CultureVictoryTest {
	private const int OneCityLimit = 20000;
	private const int AllCitiesLimit = 100000;

	[Theory]
	[InlineData(19999, 0, 0, false, false)]
	[InlineData(20000, 0, 0, false, true)]
	[InlineData(0, 99999, 0, false, false)]
	[InlineData(0, 100000, 50000, true, true)]
	[InlineData(0, 100000, 50001, true, false)]
	[InlineData(0, 100000, 50001, false, true)]
	[InlineData(0, 100000, 200000, false, true)]
	public void HasVictory_UsesBothCulturePathsAndOptionalRivalLead(
		int largestCity, int totalCulture, int topRivalCulture, bool requireDoubleLead, bool expected) {

		CultureVictory victory = new(OneCityLimit, AllCitiesLimit, requireDoubleLead);
		VictoryStatus status = new() {
			LargestCityCulture = largestCity,
			TotalCulture = totalCulture,
			TopRivalCulture = topRivalCulture,
		};

		Assert.Equal(expected, victory.HasVictory(status));
	}

	[Theory]
	[InlineData(20000, 0, 0, "city")]
	[InlineData(0, 100000, 0, "empire")]
	[InlineData(20000, 100000, 0, "city and empire")]
	[InlineData(0, 100000, 50001, "")]
	public void Detail_NamesTheCulturePathThatWasMet(
		int largestCity, int totalCulture, int topRivalCulture, string expected) {

		CultureVictory victory = new(OneCityLimit, AllCitiesLimit, true);
		VictoryStatus status = new() {
			LargestCityCulture = largestCity,
			TotalCulture = totalCulture,
			TopRivalCulture = topRivalCulture,
		};

		Assert.Equal(expected, victory.Detail(status));
	}

	[Fact]
	public void Evaluate_ComputesBestCityTotalAndTopRivalCulture() {
		Player player = new() { civilization = new Civilization("Rome") };
		Player weakRival = new() { civilization = new Civilization("Greece") };
		Player strongRival = new() { civilization = new Civilization("Egypt") };

		MakeCity(player, 25000);
		MakeCity(player, 30000);
		MakeCity(weakRival, 10000);
		MakeCity(strongRival, 40000);
		MakeCity(strongRival, 20000);

		C7GameData.GameData gameData = new();
		gameData.players.AddRange(new List<Player> { player, weakRival, strongRival });

		CultureVictory victory = new(OneCityLimit, AllCitiesLimit, true);
		VictoryStatus status = victory.Evaluate(player, gameData);

		Assert.Equal(30000, status.LargestCityCulture);
		Assert.Equal(55000, status.TotalCulture);
		Assert.Equal(60000, status.TopRivalCulture);
	}

	[Fact]
	public void GenerateStatusRows_ShowsTopRivalPerRow() {
		CultureVictory victory = new(OneCityLimit, AllCitiesLimit, true);
		VictoryStatus status = new() { LargestCityCulture = 1000, TotalCulture = 2000 };
		VictoryStatus rivalWithBestCity = new() {
			Player = new Player { civilization = new Civilization("City Civ") },
			LargestCityCulture = 5000,
			TotalCulture = 6000,
		};
		VictoryStatus rivalWithMostCulture = new() {
			Player = new Player { civilization = new Civilization("Empire Civ") },
			LargestCityCulture = 3000,
			TotalCulture = 9000,
		};

		List<string[]> rows = new(victory.GenerateStatusRows(status, new List<VictoryStatus> { rivalWithBestCity, rivalWithMostCulture }));

		Assert.Equal(2, rows.Count);
		Assert.Equal("City Civ", rows[0][4]);
		Assert.Equal("5000", rows[0][5]);
		Assert.Equal("Empire Civ", rows[1][4]);
		Assert.Equal("9000", rows[1][5]);
		Assert.Contains("(2x)", rows[1][1]);
	}

	[Fact]
	public void GenerateStatusRows_WithNoRivals_LeavesRivalColumnsBlank() {
		CultureVictory victory = new(OneCityLimit, AllCitiesLimit, false);
		VictoryStatus status = new() { LargestCityCulture = 1000, TotalCulture = 2000 };

		List<string[]> rows = new(victory.GenerateStatusRows(status, new List<VictoryStatus>()));

		Assert.All(rows, row => Assert.Equal("", row[4]));
		Assert.All(rows, row => Assert.Equal("", row[5]));
		Assert.DoesNotContain("(2x)", rows[1][1]);
	}

	[Theory]
	[InlineData("Tiny", 20000, 60000)]
	[InlineData("Small", 20000, 80000)]
	[InlineData("Standard", 20000, 100000)]
	[InlineData("Large", 20000, 130000)]
	[InlineData("Huge", 20000, 160000)]
	[InlineData("Custom", 20000, 100000)]
	public void EffectiveLimits_FallBackToC3CDefaultsForWorldSize(string worldSize, int expectedOneCity, int expectedAllCities) {
		(int oneCity, int allCities) = CultureVictoryDefaults.EffectiveLimits(0, 0, worldSize);

		Assert.Equal(expectedOneCity, oneCity);
		Assert.Equal(expectedAllCities, allCities);
	}

	[Theory]
	[InlineData("Tiny", 25000, 200000, 25000, 120000)]
	[InlineData("Huge", 25000, 200000, 25000, 320000)]
	public void EffectiveLimits_ScaleImportedAllCitiesValueAndKeepOneCityValue(
		string worldSize, int importedOneCity, int importedAllCities, int expectedOneCity, int expectedAllCities) {

		(int oneCity, int allCities) = CultureVictoryDefaults.EffectiveLimits(importedOneCity, importedAllCities, worldSize);

		Assert.Equal(expectedOneCity, oneCity);
		Assert.Equal(expectedAllCities, allCities);
	}

	private static City MakeCity(Player owner, int culture) {
		City city = new(Tile.NONE, owner, owner.civilization.name, ID.None("city"));
		city.perPlayerCulture[owner] = culture;
		owner.cities.Add(city);
		return city;
	}
}
