using System.Collections.Generic;
using System.Linq;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class CityTest : MapBase {
	[Fact]
	public void CityWith2ProductionPerTurn_ShouldReturn1TurnIf9_of_10ProductionDone() {
		C7Engine.EngineStorage.InitializeGameDataForTests(new C7GameData.GameData() {
			gameDifficulty = new Difficulty(),
		});
		Player player = new() {
			isHuman = true,
		};
		player.civilization = new Civilization();
		player.government = new Government();
		UnitPrototype warrior = new UnitPrototype();
		warrior.shieldCost = 10;

		City city = new City(Tile.NONE, player, "Fighter Town, USA", ID.None("city"));
		city.itemBeingProduced = warrior;
		city.SetStoredShields(9);

		TerrainType oneShield = new TerrainType();
		oneShield.baseShieldProduction = 1;

		Tile workedTile = new Tile(ID.None("tile"));
		workedTile.overlayTerrainType = oneShield;

		CityResident maverick = new CityResident();
		maverick.tileWorked = workedTile;
		city.residents.Add(maverick);

		int turnsUntilFinished = city.TurnsUntilProductionFinished();
		Assert.Equal(1, turnsUntilFinished);
	}

	[Fact]
	public void CityWith2ProductionPerTurn_ShouldReturn1TurnIf19_of_20FoodDone() {
		Player player = new();
		player.government = new Government();
		player.rules = new() { MaximumLevel1CitySize = 6 };
		TerrainType oneShield = new TerrainType();
		oneShield.baseShieldProduction = 1;
		Tile tile = new Tile(ID.None("tile"));

		City city = new City(tile, player, "Gotham", ID.None("city"));
		city.foodStored = 19;
		tile.cityAtTile = city;

		TerrainType grassland = new TerrainType();
		grassland.baseFoodProduction = 2;

		Tile workedTile = new Tile(ID.None("test-tile"));
		workedTile.overlayTerrainType = grassland;

		CityResident robin = new CityResident();
		robin.tileWorked = workedTile;
		city.residents.Add(robin);

		int turnsUntilGrowth = city.TurnsUntilGrowth();
		Assert.Equal(1, turnsUntilGrowth);
	}

	[Fact]
	public void CityShouldShrinkWhenItRunsOutOfFood() {
		C7GameData.GameData gameData = new();
		Player player = new();
		player.government = new Government();
		player.rules = new() { MaximumLevel1CitySize = 6 };
		TerrainType oneShield = new TerrainType();
		oneShield.baseShieldProduction = 1;
		Tile tile = new Tile(ID.None("tile"));

		City city = new City(tile, player, "Gotham", ID.None("city"));
		city.foodStored = 0;
		tile.cityAtTile = city;

		CityResident resident1 = new();
		city.residents.Add(resident1);

		CityResident resident2 = new();
		city.residents.Add(resident2);

		// Confirm we lose population without enough food.
		Assert.Equal(2, city.residents.Count);
		city.HandleCityGrowth(gameData);
		Assert.Equal(1, city.residents.Count);
	}

	private static Building MakeBuilding(SaveBuilding save, params Resource[] requiredResources) {
		Building building = new(save, new C7GameData.GameData());
		building.requiredResources = requiredResources.ToHashSet();
		return building;
	}

	private static Building MakeSmallWonder(params Resource[] requiredResources) {
		return MakeBuilding(new SaveBuilding { name = "Test Small Wonder", isSmallWonder = true }, requiredResources);
	}

	private static Building MakeGreatWonder() {
		return MakeBuilding(new SaveBuilding {
			name = "Test Great Wonder",
			greatWonderProperties = new SaveBuilding.GreatWonderProperties(),
		});
	}

	private static Player MakePlayerForProduction() {
		return new Player {
			government = new Government(),
			rules = new Rules { MaxRankOfWorkableTiles = 1 },
		};
	}

	private static City MakeCity(Player player, string name = "Testville") {
		Tile tile = new Tile(ID.None("tile"));
		City city = new City(tile, player, name, ID.None("city"));
		tile.cityAtTile = city;
		player.cities.Add(city);
		return city;
	}

	// Makes a city on a desert tile surrounded by eight desert tiles, all
	// within its borders. Returns the surrounding tiles, excluding the city tile.
	private (City city, List<Tile> surroundings) MakeCityWithSurroundings(Player player) {
		InitilizeStartTile(MakeDesertTile(), new TileLocation(10, 10));
		Tile center = startTile;
		List<Tile> tiles = SurroundTile(center, MakeDesertTile);
		InitPartialGameMap(20, 20, tiles);

		City city = new City(center, player, "Ironton", ID.None("city"));
		center.cityAtTile = city;
		player.cities.Add(city);
		foreach (Tile t in tiles) {
			t.owningCity = city;
		}
		return (city, tiles.Where(t => t != center).ToList());
	}

	private static HashSet<Resource> NoResources() {
		return new HashSet<Resource>();
	}

	[Fact]
	public void SmallWonder_CanBeBuiltWhenNoCityHasOrIsBuildingIt() {
		Player player = MakePlayerForProduction();
		City city = MakeCity(player);
		MakeCity(player, "Other");

		Assert.True(MakeSmallWonder().CanProduce(city, NoResources()));
	}

	[Fact]
	public void SmallWonder_CannotBeBuiltIfAnotherCityOfThePlayerHasIt() {
		Player player = MakePlayerForProduction();
		City city = MakeCity(player);
		City other = MakeCity(player, "Other");

		Building wonder = MakeSmallWonder();
		other.constructed_buildings.Add(new CityBuilding { building = wonder });

		Assert.False(wonder.CanProduce(city, NoResources()));
	}

	[Fact]
	public void SmallWonder_CannotBeBuiltIfAnotherCityOfThePlayerIsBuildingIt() {
		Player player = MakePlayerForProduction();
		City city = MakeCity(player);
		City other = MakeCity(player, "Other");

		Building wonder = MakeSmallWonder();
		other.itemBeingProduced = wonder;

		Assert.False(wonder.CanProduce(city, NoResources()));
	}

	[Fact]
	public void SmallWonder_IsNotBlockedByAnotherPlayersCity() {
		Player player = MakePlayerForProduction();
		City city = MakeCity(player);
		Player rival = MakePlayerForProduction();
		City rivalCity = MakeCity(rival, "Rivalton");

		Building wonder = MakeSmallWonder();
		rivalCity.constructed_buildings.Add(new CityBuilding { building = wonder });

		Assert.True(wonder.CanProduce(city, NoResources()));

		rivalCity.constructed_buildings.Clear();
		rivalCity.itemBeingProduced = wonder;

		Assert.True(wonder.CanProduce(city, NoResources()));
	}

	[Fact]
	public void GreatWonder_CannotBeBuiltIfAnotherCityOfThePlayerIsBuildingIt() {
		C7Engine.EngineStorage.InitializeGameDataForTests(new C7GameData.GameData());
		Player player = MakePlayerForProduction();
		City city = MakeCity(player);
		City other = MakeCity(player, "Other");

		Building wonder = MakeGreatWonder();
		Assert.True(wonder.CanProduce(city, NoResources()));

		other.itemBeingProduced = wonder;
		Assert.False(wonder.CanProduce(city, NoResources()));
	}

	[Fact]
	public void GreatWonder_CannotBeBuiltIfAlreadyBuiltAnywhere() {
		C7GameData.GameData gameData = new();
		C7Engine.EngineStorage.InitializeGameDataForTests(gameData);
		Player player = MakePlayerForProduction();
		City city = MakeCity(player);

		Building wonder = MakeGreatWonder();
		gameData.GreatWondersBuilt.Add(wonder.name);

		Assert.False(wonder.CanProduce(city, NoResources()));
	}

	[Fact]
	public void WonderFlags_AreMappedToBuildingBools() {
		Building building = MakeBuilding(new SaveBuilding {
			name = "Test Wonder",
			isSmallWonder = true,
			wonderFlags = [
				SaveBuilding.WonderFlag.TreasuryEarnsInterest,
				SaveBuilding.WonderFlag.ForbiddenPalace,
				SaveBuilding.WonderFlag.SafeSeaTravel,
			],
		});

		Assert.True(building.treasuryEarnsInterest);
		Assert.True(building.isForbiddenPalace);
		Assert.True(building.safeSeaTravel);

		Assert.False(building.doublesResearchOutput);
		Assert.False(building.allowsBuildArmy);
		Assert.False(building.goodsMustBeInCityRadius);
	}

	[Fact]
	public void FacesInAllCities_AreSplitBySign() {
		Building content = MakeBuilding(new SaveBuilding { name = "Content", contentFacesInCity = 3, contentFacesInAllCities = 2 });
		Assert.Equal(3, content.contentFacesInCity);
		Assert.Equal(2, content.contentFacesInAllCities);
		Assert.Equal(0, content.unhappyFacesInAllCities);

		Building unhappy = MakeBuilding(new SaveBuilding { name = "Unhappy", contentFacesInAllCities = -1 });
		Assert.Equal(0, unhappy.contentFacesInAllCities);
		Assert.Equal(1, unhappy.unhappyFacesInAllCities);
		Assert.Equal(0, unhappy.contentFacesInCity);
	}

	[Fact]
	public void GlobalNetContentFaces_SumsAllCitiesBuildingsOnce() {
		Player player = MakePlayerForProduction();
		City first = MakeCity(player);
		City second = MakeCity(player, "Second");
		City third = MakeCity(player, "Third");

		Building gardens = MakeBuilding(new SaveBuilding { name = "Gardens", contentFacesInCity = 3, contentFacesInAllCities = 1 });
		Building bach = MakeBuilding(new SaveBuilding { name = "Bach", contentFacesInAllCities = 2 });
		Building plague = MakeBuilding(new SaveBuilding { name = "Plague", contentFacesInAllCities = -1 });
		Building temple = MakeBuilding(new SaveBuilding { name = "Temple", contentFacesInCity = 1 });

		first.constructed_buildings.Add(new CityBuilding { building = gardens });
		first.constructed_buildings.Add(new CityBuilding { building = temple });
		second.constructed_buildings.Add(new CityBuilding { building = bach });
		second.constructed_buildings.Add(new CityBuilding { building = plague });
		third.constructed_buildings.Add(new CityBuilding { building = temple });

		// Each city gets the effects of the other cities' buildings only.
		// Per-city faces and repeated buildings don't matter.
		Assert.Equal(1, player.GetNetContentFacesFromOtherCities(first));   // 2 - 1
		Assert.Equal(1, player.GetNetContentFacesFromOtherCities(second));  // 1
		Assert.Equal(2, player.GetNetContentFacesFromOtherCities(third));   // 1 + 2 - 1
	}

	[Fact]
	public void GlobalNetContentFaces_DoNotStackWithTheBuildingsOwnCity() {
		Player player = MakePlayerForProduction();
		City withGardens = MakeCity(player);
		City other = MakeCity(player, "Other");
		withGardens.constructed_buildings.Add(new CityBuilding {
			building = MakeBuilding(new SaveBuilding { name = "Gardens", contentFacesInCity = 3, contentFacesInAllCities = 1 }),
		});

		Assert.Equal(0, player.GetNetContentFacesFromOtherCities(withGardens));
		Assert.Equal(1, player.GetNetContentFacesFromOtherCities(other));
	}

	[Fact]
	public void GlobalNetContentFaces_IsZeroWithoutGlobalBuildings() {
		Player player = MakePlayerForProduction();
		City city = MakeCity(player);
		City other = MakeCity(player, "Other");
		other.constructed_buildings.Add(new CityBuilding {
			building = MakeBuilding(new SaveBuilding { name = "Temple", contentFacesInCity = 1 }),
		});

		Assert.Equal(0, player.GetNetContentFacesFromOtherCities(city));
	}

	[Fact]
	public void GlobalNetContentFaces_IgnoresOtherPlayersBuildings() {
		Player player = MakePlayerForProduction();
		City city = MakeCity(player);
		Player rival = MakePlayerForProduction();
		City rivalCity = MakeCity(rival, "Rivalton");
		rivalCity.constructed_buildings.Add(new CityBuilding {
			building = MakeBuilding(new SaveBuilding { name = "Gardens", contentFacesInAllCities = 1 }),
		});

		Assert.Equal(0, player.GetNetContentFacesFromOtherCities(city));
	}

	[Fact]
	public void ObsoleteBuildings_HaveNoGlobalEffect() {
		ID obsoleteTechId = ID.None("tech-electricity");
		Tech electricity = new() { id = obsoleteTechId };
		Player player = MakePlayerForProduction();
		City city = MakeCity(player);
		City other = MakeCity(player, "Other");

		Building gardens = MakeBuilding(new SaveBuilding { name = "Gardens", contentFacesInAllCities = 1 });
		gardens.renderedObsoleteBy = electricity;
		other.constructed_buildings.Add(new CityBuilding { building = gardens });

		Assert.False(gardens.IsObsolete(player));
		Assert.Equal(1, player.GetNetContentFacesFromOtherCities(city));

		player.knownTechs.Add(obsoleteTechId);

		Assert.True(gardens.IsObsolete(player));
		Assert.Equal(0, player.GetNetContentFacesFromOtherCities(city));
	}

	[Fact]
	public void GoodsMustBeInCityRadius_IsReadFromRegularFlags() {
		Building building = MakeBuilding(new SaveBuilding {
			name = "Test Works",
			flags = [SaveBuilding.Flag.GoodsMustBeInCityRadius],
		});

		Assert.True(building.goodsMustBeInCityRadius);
		Assert.False(building.treasuryEarnsInterest);
	}

	private static Building MakeRadiusBuilding(params Resource[] required) {
		return MakeBuilding(new SaveBuilding {
			name = "Test Works",
			isSmallWonder = true,
			flags = [SaveBuilding.Flag.GoodsMustBeInCityRadius],
		}, required);
	}

	[Fact]
	public void GoodsMustBeInCityRadius_CanBeBuiltWithResourceOnAWorkableTile() {
		Resource iron = new() { Key = "iron" };
		(City city, List<Tile> surroundings) = MakeCityWithSurroundings(MakePlayerForProduction());
		surroundings[0].Resource = iron;

		Assert.True(MakeRadiusBuilding(iron).CanProduce(city, new() { iron }));
	}

	[Fact]
	public void GoodsMustBeInCityRadius_CanBeBuiltWithResourceUnderTheCity() {
		Resource iron = new() { Key = "iron" };
		(City city, _) = MakeCityWithSurroundings(MakePlayerForProduction());
		city.location.Resource = iron;

		Assert.True(MakeRadiusBuilding(iron).CanProduce(city, new() { iron }));
	}

	[Fact]
	public void GoodsMustBeInCityRadius_CannotBeBuiltIfResourceIsOnlyAccessibleElsewhere() {
		Resource iron = new() { Key = "iron" };
		(City city, _) = MakeCityWithSurroundings(MakePlayerForProduction());

		// The player has access to iron, but none of it is near this city.
		Assert.False(MakeRadiusBuilding(iron).CanProduce(city, new() { iron }));
	}

	[Fact]
	public void GoodsMustBeInCityRadius_CannotBeBuiltIfResourceTileIsNotOwnedByThePlayer() {
		Resource iron = new() { Key = "iron" };
		(City city, List<Tile> surroundings) = MakeCityWithSurroundings(MakePlayerForProduction());
		surroundings[0].Resource = iron;
		surroundings[0].owningCity = null;

		Assert.False(MakeRadiusBuilding(iron).CanProduce(city, new() { iron }));
	}

	[Fact]
	public void GoodsMustBeInCityRadius_RequiresEveryResource() {
		Resource iron = new() { Key = "iron" };
		Resource coal = new() { Key = "coal" };
		(City city, List<Tile> surroundings) = MakeCityWithSurroundings(MakePlayerForProduction());
		surroundings[0].Resource = iron;

		Building works = MakeRadiusBuilding(iron, coal);
		HashSet<Resource> accessible = new() { iron, coal };

		Assert.False(works.CanProduce(city, accessible));

		surroundings[1].Resource = coal;
		Assert.True(works.CanProduce(city, accessible));
	}

	[Fact]
	public void RequiredBuilding_MustBePresentInTheCity() {
		Player player = MakePlayerForProduction();
		City city = MakeCity(player);
		City other = MakeCity(player, "Other");

		Building marketplace = MakeBuilding(new SaveBuilding { name = "Marketplace" });
		Building bank = MakeBuilding(new SaveBuilding { name = "Bank" });
		bank.requiredBuilding = marketplace;

		Assert.False(bank.CanProduce(city, NoResources()));

		// A marketplace in a different city doesn't count.
		other.constructed_buildings.Add(new CityBuilding { building = marketplace });
		Assert.False(bank.CanProduce(city, NoResources()));

		city.constructed_buildings.Add(new CityBuilding { building = marketplace });
		Assert.True(bank.CanProduce(city, NoResources()));

		// Once the bank is built, it can't be built again.
		city.constructed_buildings.Add(new CityBuilding { building = bank });
		Assert.False(bank.CanProduce(city, NoResources()));
	}

	[Fact]
	public void WithoutGoodsMustBeInCityRadius_AccessibleResourcesAreEnough() {
		Resource iron = new() { Key = "iron" };
		(City city, _) = MakeCityWithSurroundings(MakePlayerForProduction());

		Building building = MakeBuilding(new SaveBuilding { name = "Test Building" }, iron);

		Assert.True(building.CanProduce(city, new() { iron }));
	}
}
