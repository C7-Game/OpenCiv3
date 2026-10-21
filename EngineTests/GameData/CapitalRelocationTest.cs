using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using Xunit;

namespace EngineTests.GameData;

// When a civ loses its capital, the palace moves to the city scoring highest by
// Civ3's formula: https://civfanatics.com/civ3/strategy/game-mechanics/free-palace-jump/
public class CapitalRelocationTest {
	private readonly GameMap map = new() { numTilesWide = 200, numTilesTall = 100 };
	private readonly Player player;
	private readonly Civilization foreigners = new() { name = "Foreigners" };

	public CapitalRelocationTest() {
		EngineStorage.InitializeGameDataForTests(new C7GameData.GameData() { gameDifficulty = new Difficulty() });
		player = new Player {
			civilization = new Civilization { name = "Us" },
			rules = new Rules { MaximumLevel1CitySize = 6, MaximumLevel2CitySize = 12 },
		};
	}

	// Cities are placed along one row, so a city `steps` tiles east is at
	// walking distance `steps` (x advances by 2 per tile in Civ3 coordinates).
	private City AddCity(string name, int steps, int citizens, int foreignCitizens = 0) {
		Tile tile = new(ID.None("tile")) { map = map, XCoordinate = 2 * steps, YCoordinate = 10 };
		City city = new(tile, player, name, ID.None("city"));
		for (int i = 0; i < citizens + foreignCitizens; i++) {
			city.residents.Add(new CityResident {
				city = city,
				nationality = i < citizens ? player.civilization : foreigners,
			});
		}
		player.cities.Add(city);
		return city;
	}

	private MapUnit AddUnit(City city, int attack, int defense, Player owner = null) {
		MapUnit unit = new(ID.None("unit")) {
			owner = owner ?? player,
			unitType = new UnitPrototype { attack = attack, defense = defense },
		};
		city.location.unitsOnTile.Add(unit);
		return unit;
	}

	private static Building MakeBuilding(string name, bool isCenterOfEmpire) {
		SaveBuilding save = new() { name = name };
		if (isCenterOfEmpire) {
			save.flags.Add(SaveBuilding.Flag.IsCenterOfEmpire);
		}
		return new Building(save, EngineStorage.gameData);
	}

	[Fact]
	public void OwnCitizensScoreThreeAndForeignCitizensOne() {
		City city = AddCity("Mixed", 0, citizens: 4, foreignCitizens: 2);
		Assert.Equal(4 * 3 + 2 * 1, player.CapitalScore(city));
	}

	[Fact]
	public void CitizensWithoutANationalityCountAsOurs() {
		// A city's first citizen is created without a nationality.
		City city = AddCity("Founded", 0, citizens: 0);
		city.residents.Add(new CityResident { city = city });
		Assert.Equal(3, player.CapitalScore(city));
	}

	[Fact]
	public void NeighborsScoreByTheirSize() {
		City city = AddCity("Center", 0, citizens: 1);
		AddCity("Town", 2, citizens: 6);         // 1 point (sizes 1-6)
		AddCity("City", 4, citizens: 7);         // 2 points (sizes 7-12)
		AddCity("Metropolis", 6, citizens: 13);  // 3 points (sizes 13+)
		Assert.Equal(3 + 1 + 2 + 3, player.CapitalScore(city));
	}

	[Fact]
	public void NeighborsCountUpToEightTilesAway() {
		City city = AddCity("Center", 0, citizens: 1);
		AddCity("Eight away", 8, citizens: 1);
		AddCity("Nine away", 9, citizens: 1);
		Assert.Equal(3 + 1, player.CapitalScore(city));
	}

	[Fact]
	public void NeighborsCountAcrossTheWrapSeam() {
		map.wrapHorizontally = true;
		City west = AddCity("West edge", 0, citizens: 1);
		AddCity("East edge", 97, citizens: 1);  // 3 tiles away across the seam
		Assert.Equal(3 + 1, player.CapitalScore(west));
	}

	[Fact]
	public void OnlyOurMilitaryUnitsInTheCityCount() {
		City city = AddCity("Garrison", 0, citizens: 1);
		AddUnit(city, attack: 1, defense: 1);
		AddUnit(city, attack: 0, defense: 2);
		AddUnit(city, attack: 0, defense: 0);  // a worker
		AddUnit(city, attack: 2, defense: 1, owner: new Player());
		Assert.Equal(3 + 2, player.CapitalScore(city));
	}

	// The article's example (in the article's own 1/3-point scale): Kyoto,
	// size 5 with three towns nearby, beats Neapolis, size 4 with five towns
	// nearby. Here that is 15 + 3 = 18 against 12 + 5 = 17.
	[Fact]
	public void BiggerCityBeatsOneWithMoreNeighbors() {
		City neapolis = AddCity("Neapolis", 60, citizens: 4);
		City kyoto = AddCity("Kyoto", 0, citizens: 5);
		foreach (int steps in new[] { 2, 3, 5 }) {
			AddCity($"Near Kyoto {steps}", steps, citizens: 1);
		}
		foreach (int steps in new[] { 57, 58, 59, 61, 62 }) {
			AddCity($"Near Neapolis {steps}", steps, citizens: 1);
		}
		AddCity("Too far from Neapolis", 69, citizens: 1);

		Assert.Equal(18, player.CapitalScore(kyoto));
		Assert.Equal(17, player.CapitalScore(neapolis));
		Assert.Same(kyoto, player.ChooseNewCapital());
	}

	[Fact]
	public void TiesGoToTheFirstCityFounded() {
		City older = AddCity("Older", 0, citizens: 3);
		City newer = AddCity("Newer", 20, citizens: 3);
		Assert.Equal(player.CapitalScore(older), player.CapitalScore(newer));
		Assert.Same(older, player.ChooseNewCapital());
	}

	[Fact]
	public void LosingTheCapitalMovesThePalace() {
		Building palace = MakeBuilding("Palace", isCenterOfEmpire: true);
		City small = AddCity("Small", 0, citizens: 2);
		City big = AddCity("Big", 20, citizens: 5);

		City newCapital = player.RelocateCapitalIfLost(palace);

		Assert.Same(big, newCapital);
		Assert.True(big.IsCapital());
		Assert.False(small.IsCapital());
		Assert.Contains(big.constructed_buildings, b => b.building == palace);
	}

	[Fact]
	public void ForbiddenPalaceStaysInTheNewCapital() {
		Building palace = MakeBuilding("Palace", isCenterOfEmpire: true);
		Building forbiddenPalace = MakeBuilding("Forbidden Palace", isCenterOfEmpire: false);
		City city = AddCity("Second city", 0, citizens: 5);
		city.AddBuilding(forbiddenPalace);

		player.RelocateCapitalIfLost(palace);

		Assert.True(city.IsCapital());
		Assert.Contains(city.constructed_buildings, b => b.building == forbiddenPalace);
		Assert.Contains(city.constructed_buildings, b => b.building == palace);
	}

	[Fact]
	public void NothingMovesWhileWeStillHaveACapital() {
		Building palace = MakeBuilding("Palace", isCenterOfEmpire: true);
		City capital = AddCity("Capital", 0, citizens: 1);
		capital.capital = true;
		City bigger = AddCity("Bigger", 20, citizens: 10);

		Assert.Null(player.RelocateCapitalIfLost(palace));
		Assert.True(capital.IsCapital());
		Assert.False(bigger.IsCapital());
		Assert.Empty(bigger.constructed_buildings);
	}

	[Fact]
	public void NothingHappensWithNoCitiesLeft() {
		Assert.Null(player.RelocateCapitalIfLost(MakeBuilding("Palace", isCenterOfEmpire: true)));
	}

	[Fact]
	public void CapitalStillMovesWhenTheRulesetHasNoPalace() {
		City city = AddCity("Only city", 0, citizens: 1);
		Assert.Same(city, player.RelocateCapitalIfLost(null));
		Assert.True(city.IsCapital());
		Assert.Empty(city.constructed_buildings);
	}
}
