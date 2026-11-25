using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// Reassigning citizens rebuilds every resident, which must not turn foreign
// citizens into the owner's nationality.
public class PlayerAITest : MapBase {
	private readonly CitizenType laborer = new() { IsDefaultCitizen = true };
	private readonly CitizenType entertainer = new() { Luxuries = 2 };
	private readonly Civilization foreigners = new() { name = "Foreigners" };

	private (C7GameData.GameData gameData, Player player) MakePlayer() {
		C7GameData.GameData gameData = new() {
			gameDifficulty = new Difficulty { NumberOfCitizensBornContent = 0 },
			rules = new Rules { MaximumLevel1CitySize = 6, MaximumLevel2CitySize = 12 },
		};
		gameData.citizenTypes.AddRange([laborer, entertainer]);
		EngineStorage.InitializeGameDataForTests(gameData);

		Player player = MakeTestPlayer([]);
		gameData.players.Add(player);
		player.rules = new Rules { MaxRankOfWorkableTiles = 1, MaximumLevel1CitySize = 6, MaximumLevel2CitySize = 12 };

		InitilizeStartTile(MakeDesertTile(), new TileLocation(10, 10));
		InitPartialGameMap(20, 20, SurroundTile(startTile, MakeDesertTile));
		return (gameData, player);
	}

	// Founds a city on the given tile, owns the tiles around it, and fills it
	// with citizens of the given nationalities.
	private City MakeCity(Player player, Tile center, string name, params Civilization[] nationalities) {
		City city = new City(center, player, name, ID.None("city"));
		center.cityAtTile = city;
		player.cities.Add(city);
		foreach (Tile t in center.neighbors.Values) {
			t.owningCity = city;
		}
		foreach (Civilization nationality in nationalities) {
			city.AddCitizen(new CityResident { citizenType = laborer, city = city, nationality = nationality });
		}
		return city;
	}

	[Fact]
	public void ReassignCitizens_PreservesCitizenNationalities() {
		(C7GameData.GameData gameData, Player player) = MakePlayer();
		Civilization[] before = [player.civilization, foreigners, foreigners, player.civilization];
		City city = MakeCity(player, startTile, "Ironton", before);

		PlayerAI.ReassignCitizens(gameData, city);

		Assert.Equal(before, city.residents.Select(r => r.nationality));
	}

	[Fact]
	public void FixRemainingUnhappyCities_PreservesCitizenNationalitiesInEveryCity() {
		(_, Player player) = MakePlayer();
		Tile otherCenter = startTile.neighbors[TileDirection.EAST];
		Civilization[] first = [foreigners, player.civilization, foreigners];
		Civilization[] second = [foreigners, foreigners];
		City city1 = MakeCity(player, startTile, "Ironton", first);
		City city2 = MakeCity(player, otherCenter, "Steelburg", second);

		PlayerAI.FixRemainingUnhappyCities(player);

		Assert.Equal(first, city1.residents.Select(r => r.nationality));
		Assert.Equal(second, city2.residents.Select(r => r.nationality));
	}
}
