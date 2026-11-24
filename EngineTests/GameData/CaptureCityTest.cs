using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// End to end: capturing or razing a city in a real generated game hands it over
// (or removes it) cleanly for both the attacker and the previous owner.
public class CaptureCityTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player attacker;
	private readonly Player victim;

	public CaptureCityTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		// Engine message queue is static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.messagesToUI.Clear();
		attacker = gameData.players.First(p => p.isHuman);
		victim = gameData.players.First(p => !p.isHuman && !p.isBarbarians);
	}

	public void Dispose() {
		EngineStorage.messagesToUI.Clear();
	}

	// Founds a city on an empty land tile at least minDistance from the attacker's start.
	private City Found(Player owner, string name, int minDistance = 0) {
		Tile start = attacker.units[0].location;
		Tile site = gameData.map.tiles
			.Where(t => t.IsLand() && t.cityAtTile == null && t.DistanceTo(start) >= minDistance)
			.Where(t => gameData.cities.All(c => c.location.DistanceTo(t) >= 3))
			.OrderBy(t => t.DistanceTo(start))
			.First();
		return CityInteractions.BuildCity(site, owner, name);
	}

	private void GrowTo(City city, int size) {
		while (city.residents.Count < size) {
			CityResident resident = new CityResident {
				city = city,
				nationality = city.owner.civilization,
				citizenType = gameData.citizenTypes.Find(x => x.IsDefaultCitizen)
			};
			city.AddCitizen(resident);
			CityTileAssignmentAI.AssignNewCitizenToTile(gameData, resident);
		}
	}

	private Building Palace => gameData.Buildings.Find(b => b.isCenterOfEmpire);

	// The victim's capital plus a second city, so the victim survives losing either.
	private (City capital, City other) VictimWithTwoCities(int capitalSize = 4, int otherSize = 3) {
		City capital = Found(victim, "Capital");
		City other = Found(victim, "Other", 3);
		GrowTo(capital, capitalSize);
		GrowTo(other, otherSize);
		Assert.True(capital.IsCapital());
		return (capital, other);
	}

	[Fact]
	public void CapturedCityChangesHands() {
		var (_, other) = VictimWithTwoCities();

		CityInteractions.CaptureCity(attacker, other);

		Assert.Same(attacker, other.owner);
		Assert.Contains(other, attacker.cities);
		Assert.DoesNotContain(other, victim.cities);
		Assert.Contains(other, gameData.cities);
		Assert.Same(other, other.location.cityAtTile);
		Assert.Same(other, other.location.owningCity);
		// Culture is keyed by owner, so the new owner needs an entry.
		Assert.Equal(0, other.GetCulture());
	}

	[Fact]
	public void CapturingCostsACitizenAndResetsStores() {
		var (_, other) = VictimWithTwoCities();
		GrowTo(other, 3);
		other.foodStored = 10;
		other.SetStoredShields(20);

		CityInteractions.CaptureCity(attacker, other);

		Assert.Equal(2, other.residents.Count);
		Assert.Equal(0, other.foodStored);
		Assert.Equal(0, other.shieldsStored);
		Assert.NotNull(other.itemBeingProduced);
	}

	[Fact]
	public void CapturingTheCapitalDestroysItsPalaceAndMovesTheVictimsCapital() {
		var (capital, other) = VictimWithTwoCities();

		CityInteractions.CaptureCity(attacker, capital);

		Assert.False(capital.IsCapital());
		Assert.DoesNotContain(capital.constructed_buildings, b => b.building == Palace);
		Assert.True(other.IsCapital());
		Assert.Contains(other.constructed_buildings, b => b.building == Palace);
		Assert.Single(victim.cities, c => c.IsCapital());
	}

	[Fact]
	public void CapturingAnotherCityLeavesTheVictimsCapitalAlone() {
		var (capital, other) = VictimWithTwoCities();

		CityInteractions.CaptureCity(attacker, other);

		Assert.True(capital.IsCapital());
		Assert.Single(victim.cities, c => c.IsCapital());
	}

	[Fact]
	public void CaptureTransfersTheGoldTheCityRepresents() {
		var (_, other) = VictimWithTwoCities();
		GrowTo(other, 3);
		victim.gold = 100;
		attacker.gold = 10;
		int expected = other.goldOnDestruction;
		Assert.InRange(expected, 1, 100);

		CityInteractions.CaptureCity(attacker, other);

		Assert.Equal(100 - expected, victim.gold);
		Assert.Equal(10 + expected, attacker.gold);
	}

	[Fact]
	public void CapturingASizeOneCityDestroysIt() {
		var (_, other) = VictimWithTwoCities(otherSize: 1);
		Assert.Single(other.residents);
		Tile location = other.location;

		CityInteractions.CaptureCity(attacker, other);

		Assert.DoesNotContain(other, gameData.cities);
		Assert.DoesNotContain(other, victim.cities);
		Assert.DoesNotContain(other, attacker.cities);
		Assert.Null(location.cityAtTile);
	}

	[Fact]
	public void CapturingTheVictimsLastCityLeavesThemWithNone() {
		City only = Found(victim, "Only");
		GrowTo(only, 3);

		CityInteractions.CaptureCity(attacker, only);

		Assert.Empty(victim.cities);
		Assert.Contains(only, attacker.cities);
		Assert.Same(attacker, only.owner);
		Assert.False(only.IsCapital());
	}

	[Fact]
	public void CapturingYourOwnCityChangesNothing() {
		City city = Found(attacker, "Mine");
		GrowTo(city, 3);
		int citizens = city.residents.Count;

		CityInteractions.CaptureCity(attacker, city);

		Assert.Same(attacker, city.owner);
		Assert.Equal(citizens, city.residents.Count);
		Assert.Single(attacker.cities, c => c == city);
	}

	[Fact]
	public void RazingDestroysTheCityAndTransfersGold() {
		var (_, other) = VictimWithTwoCities();
		GrowTo(other, 3);
		victim.gold = 100;
		attacker.gold = 10;
		int expected = other.goldOnDestruction;
		Assert.InRange(expected, 1, 100);
		Tile location = other.location;

		CityInteractions.RazeCity(attacker, other);

		Assert.DoesNotContain(other, gameData.cities);
		Assert.DoesNotContain(other, victim.cities);
		Assert.Null(location.cityAtTile);
		Assert.Equal(100 - expected, victim.gold);
		Assert.Equal(10 + expected, attacker.gold);
	}
}
