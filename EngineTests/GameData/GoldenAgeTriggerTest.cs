using System.Collections.Generic;
using System.Linq;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class GoldenAgeTriggerTest : MapBase {
	private const int Turn = 10;
	private const int Duration = 20;

	public GoldenAgeTriggerTest() {
		InitializeTestGameData();
		C7Engine.EngineStorage.gameData.turn = Turn;
	}

	private Player MakeCivPlayer(bool barbarian = false) {
		return new Player() {
			civilization = new Civilization() { isBarbarian = barbarian },
			rules = new Rules() { GoldenAgeDuration = Duration },
		};
	}

	private MapUnit MakeUnit(Player owner, bool startsGoldenAge, int defense = 1) {
		MapUnit unit = new(ID.None("")) { unitType = new UnitPrototype() { defense = defense }, owner = owner };
		unit.unitType.startsGoldenAge = startsGoldenAge;
		return unit;
	}

	[Fact]
	public void WinningAgainstAnotherCivStartsGoldenAge() {
		Player winnerOwner = MakeCivPlayer();
		MapUnit.StartGoldenAgeIfEarned(MakeUnit(winnerOwner, true), MakeUnit(MakeCivPlayer(), false));

		Assert.True(winnerOwner.HasHadGA);
		Assert.True(winnerOwner.IsInGA);
		Assert.Equal(Turn + Duration, winnerOwner.goldenAgeEnds);
	}

	[Fact]
	public void WinningAgainstBarbariansDoesNotStartGoldenAge() {
		Player winnerOwner = MakeCivPlayer();
		MapUnit.StartGoldenAgeIfEarned(MakeUnit(winnerOwner, true), MakeUnit(MakeCivPlayer(barbarian: true), false));

		Assert.False(winnerOwner.HasHadGA);
	}

	[Fact]
	public void KillingNonCombatUnitDoesNotStartGoldenAge() {
		Player winnerOwner = MakeCivPlayer();
		MapUnit settler = MakeUnit(MakeCivPlayer(), false, defense: 0);
		MapUnit.StartGoldenAgeIfEarned(MakeUnit(winnerOwner, true), settler);

		Assert.False(winnerOwner.HasHadGA);
	}

	[Fact]
	public void WinningWithoutTheFlagDoesNotStartGoldenAge() {
		Player winnerOwner = MakeCivPlayer();
		MapUnit.StartGoldenAgeIfEarned(MakeUnit(winnerOwner, false), MakeUnit(MakeCivPlayer(), false));

		Assert.False(winnerOwner.HasHadGA);
	}

	[Fact]
	public void SecondVictoryDoesNotRestartGoldenAge() {
		Player winnerOwner = MakeCivPlayer();
		MapUnit winner = MakeUnit(winnerOwner, true);
		MapUnit loser = MakeUnit(MakeCivPlayer(), false);

		MapUnit.StartGoldenAgeIfEarned(winner, loser);
		C7Engine.EngineStorage.gameData.turn = Turn + 5;
		MapUnit.StartGoldenAgeIfEarned(winner, loser);

		Assert.Equal(Turn + Duration, winnerOwner.goldenAgeEnds);
	}

	[Fact]
	public void FlagRoundTripsThroughSavePrototype() {
		UnitPrototype proto = new() { name = "Samurai" };
		proto.startsGoldenAge = true;

		Assert.Contains(SaveUnitPrototype.Flag.StartsGoldenAge, new SaveUnitPrototype(proto).flags);
	}

	// ---- Great wonder trigger (City.HandleCityProduction) ----
	//
	// Completing a great wonder starts the owner's golden age if the traits of all great wonders the owner
	// currently has (including the new one) cover all of the owner's civ traits.

	private Player MakeWonderPlayer() {
		Player player = new Player() {
			isHuman = true,
			civilization = new Civilization() {
				traits = [Civilization.Trait.Militaristic, Civilization.Trait.Scientific],
			},
			government = new Government(),
			rules = new Rules() {
				MaxRankOfWorkableTiles = 2,
				GoldenAgeDuration = Duration,
			},
		};
		C7Engine.EngineStorage.gameData.players.Add(player);
		return player;
	}

	// Makes a city for the player on a small desert map, so that choosing what to build next after a completed
	// building works.
	private City MakeWonderCity(Player player) {
		C7Engine.EngineStorage.gameData.gameDifficulty = new Difficulty();
		C7Engine.EngineStorage.gameData.rules = player.rules;

		InitilizeStartTile(MakeDesertTile(), new TileLocation(10, 10));
		Tile center = startTile;
		List<Tile> tiles = SurroundTile(center, MakeDesertTile);
		InitPartialGameMap(20, 20, tiles);
		C7Engine.EngineStorage.gameData.map = gameMap;

		City city = new City(center, player, "Wonderville", ID.None("city"));
		center.cityAtTile = city;
		player.cities.Add(city);
		foreach (Tile t in tiles) {
			t.owningCity = city;
		}
		city.residents.Add(new CityResident { tileWorked = tiles.First(t => t != center) });
		return city;
	}

	private static Building MakeGreatWonder(string name, params Civilization.Trait[] traits) {
		return new Building(new SaveBuilding {
			name = name,
			greatWonderProperties = new SaveBuilding.GreatWonderProperties(),
			traits = traits.ToHashSet(),
		}, new C7GameData.GameData());
	}

	private static Building MakeSmallWonder(string name, params Civilization.Trait[] traits) {
		return new Building(new SaveBuilding {
			name = name,
			isSmallWonder = true,
			traits = traits.ToHashSet(),
		}, new C7GameData.GameData());
	}

	private static Building MakeRegularBuilding(string name, params Civilization.Trait[] traits) {
		return new Building(new SaveBuilding {
			name = name,
			traits = traits.ToHashSet(),
		}, new C7GameData.GameData());
	}

	// Has the city complete the given item this turn, using the real production code.
	private void Complete(City city, IProducible item) {
		city.itemBeingProduced = item;
		city.SetStoredShields(item.ShieldCost(city.owner, 1.0f));
		city.HandleCityProduction(C7Engine.EngineStorage.gameData);
	}

	[Fact]
	public void WonderWithAllPlayerTraits_StartsGoldenAge() {
		Player player = MakeWonderPlayer();
		City city = MakeWonderCity(player);
		Building wonder = MakeGreatWonder("Test Wonder", Civilization.Trait.Militaristic, Civilization.Trait.Scientific);

		Complete(city, wonder);

		Assert.Contains(wonder, city.constructed_buildings.Select(cb => cb.building));
		Assert.True(player.HasHadGA);
		Assert.True(player.IsInGA);
		Assert.Equal(Turn + Duration, player.goldenAgeEnds);
	}

	[Fact]
	public void WonderWithOnePlayerTrait_StartsGoldenAge_IfOwnedWondersCoverTheOtherTraits() {
		Player player = MakeWonderPlayer();
		City city = MakeWonderCity(player);
		city.AddBuilding(MakeGreatWonder("Owned Wonder", Civilization.Trait.Scientific));
		Assert.False(player.HasHadGA);

		Complete(city, MakeGreatWonder("New Wonder", Civilization.Trait.Militaristic));

		Assert.True(player.HasHadGA);
		Assert.Equal(Turn + Duration, player.goldenAgeEnds);
	}

	[Fact]
	public void WonderWithOnePlayerTrait_DoesNotStartGoldenAge_IfOtherTraitsAreNotCovered() {
		Player player = MakeWonderPlayer();
		City city = MakeWonderCity(player);
		city.AddBuilding(MakeGreatWonder("Owned Wonder", Civilization.Trait.Commercial));

		Complete(city, MakeGreatWonder("New Wonder", Civilization.Trait.Militaristic));

		Assert.False(player.HasHadGA);
	}

	[Fact]
	public void OwnedWondersInAnotherCityOfThePlayer_CountTowardsTheTraits() {
		Player player = MakeWonderPlayer();
		City city = MakeWonderCity(player);
		City otherCity = new City(Tile.NONE, player, "Elsewhere", ID.None("other city"));
		player.cities.Add(otherCity);
		otherCity.AddBuilding(MakeGreatWonder("Owned Wonder", Civilization.Trait.Scientific));

		Complete(city, MakeGreatWonder("New Wonder", Civilization.Trait.Militaristic));

		Assert.True(player.HasHadGA);
	}

	[Fact]
	public void RegularBuildingWithPlayerTraits_NeverStartsGoldenAge() {
		Player player = MakeWonderPlayer();
		City city = MakeWonderCity(player);
		// The building has all of the player's traits, and the player owns a great wonder covering one of them, so it
		// would trigger a golden age if it were treated like a great wonder.
		city.AddBuilding(MakeGreatWonder("Owned Wonder", Civilization.Trait.Scientific));

		Complete(city, MakeRegularBuilding("Test Building", Civilization.Trait.Militaristic, Civilization.Trait.Scientific));

		Assert.False(player.HasHadGA);
	}

	[Fact]
	public void SmallWonderWithPlayerTraits_NeverStartsGoldenAge() {
		Player player = MakeWonderPlayer();
		City city = MakeWonderCity(player);
		city.AddBuilding(MakeGreatWonder("Owned Wonder", Civilization.Trait.Scientific));

		Complete(city, MakeSmallWonder("Test Small Wonder", Civilization.Trait.Militaristic, Civilization.Trait.Scientific));

		Assert.False(player.HasHadGA);
	}

	[Fact]
	public void OwnedSmallWondersAndBuildings_DoNotCountTowardsTheTraits() {
		Player player = MakeWonderPlayer();
		City city = MakeWonderCity(player);
		city.AddBuilding(MakeSmallWonder("Owned Small Wonder", Civilization.Trait.Scientific));
		city.AddBuilding(MakeRegularBuilding("Owned Building", Civilization.Trait.Scientific));

		Complete(city, MakeGreatWonder("New Wonder", Civilization.Trait.Militaristic));

		Assert.False(player.HasHadGA);
	}

	[Fact]
	public void WonderWithPlayerTraits_DoesNotStartAnotherGoldenAge_IfPlayerAlreadyHadOne() {
		Player player = MakeWonderPlayer();
		City city = MakeWonderCity(player);
		const int originalEnd = 5;
		player.goldenAgeEnds = originalEnd;
		Assert.True(player.HasHadGA);

		Complete(city, MakeGreatWonder("Test Wonder", Civilization.Trait.Militaristic, Civilization.Trait.Scientific));

		Assert.Equal(originalEnd, player.goldenAgeEnds);
		Assert.False(player.IsInGA);
	}
}
