using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// City center commerce, see https://wiki.civforum.de/wiki/Stadtfeldertrag_(Civ3).
public class TileYieldTest : MapBase {
	private const int Level1Max = 6;
	private const int Level2Max = 12;

	// Makes a desert city center with the given number of citizens, owned by a
	// civilization with the given traits. If neighbor is given it is placed to
	// the north of the city.
	private (Tile center, City city) MakeCityCenter(int size, bool capital = false, Tile neighbor = null, params Civilization.Trait[] traits) {
		EngineStorage.InitializeGameDataForTests(new C7GameData.GameData() {
			rules = new Rules { MaximumLevel1CitySize = Level1Max, MaximumLevel2CitySize = Level2Max },
		});

		Player player = MakeTestPlayer([]);
		foreach (Civilization.Trait trait in traits) {
			player.civilization.traits.Add(trait);
		}

		Tile center = MakeDesertTile();
		if (neighbor != null) {
			AddNeighborsAndUpdateMap(center, neighbor, TileDirection.NORTH);
		}
		City city = new City(center, player, "Testville", ID.None("city")) { capital = capital };
		center.cityAtTile = city;
		player.cities.Add(city);
		for (int i = 0; i < size; i++) {
			city.residents.Add(new CityResident { city = city });
		}
		return (center, city);
	}

	private int CenterCommerce(int size, bool capital = false, Tile neighbor = null, params Civilization.Trait[] traits) {
		(Tile center, City city) = MakeCityCenter(size, capital, neighbor, traits);
		return center.CommerceYield(city).yield;
	}

	[Theory]
	[InlineData(1, 1)]
	[InlineData(Level1Max, 1)]
	[InlineData(Level1Max + 1, 2)]
	[InlineData(Level2Max, 2)]
	[InlineData(Level2Max + 1, 3)]
	public void CityCenterCommerceGrowsWithCitySize(int size, int expected) {
		Assert.Equal(expected, CenterCommerce(size));
	}

	[Theory]
	[InlineData(1, 1)]                  // towns get nothing extra
	[InlineData(Level1Max, 1)]
	[InlineData(Level1Max + 1, 4)]      // cities get +2
	[InlineData(Level2Max, 4)]
	[InlineData(Level2Max + 1, 6)]      // metropolises get +3
	public void CommercialCivsGetExtraCityCenterCommerceInLargerCities(int size, int expected) {
		Assert.Equal(expected, CenterCommerce(size, traits: Civilization.Trait.Commercial));
	}

	[Fact]
	public void CommercialBonusAppliesToTheCapitalOnlyWhenItBeatsTheCapitalFloor() {
		// The capital floor is 4. A commercial city of size 7 also makes 4, so
		// the bonus isn't added on top of the floor.
		Assert.Equal(4, CenterCommerce(Level1Max + 1, capital: true, traits: Civilization.Trait.Commercial));
		// A commercial metropolis makes 6, which beats the floor.
		Assert.Equal(6, CenterCommerce(Level2Max + 1, capital: true, traits: Civilization.Trait.Commercial));
		// A small commercial capital still gets the floor.
		Assert.Equal(4, CenterCommerce(1, capital: true, traits: Civilization.Trait.Commercial));
	}

	[Fact]
	public void CommercialBonusStacksWithRiverBonus() {
		(Tile center, City city) = MakeCityCenter(Level1Max + 1, traits: Civilization.Trait.Commercial);
		center.riverNorth = true;
		// 2 base + 2 commercial + 1 river.
		Assert.Equal(5, center.CommerceYield(city).yield);
	}

	[Fact]
	public void SeafaringCoastalCityCenterGetsOneExtraCommerce() {
		Assert.Equal(2, CenterCommerce(1, neighbor: MakeOceanTile(), traits: Civilization.Trait.Seafaring));
		Assert.Equal(3, CenterCommerce(Level1Max + 1, neighbor: MakeOceanTile(), traits: Civilization.Trait.Seafaring));
	}

	[Fact]
	public void SeafaringBonusIsAddedAfterTheCapitalFloor() {
		// Floor of 4, then +1 for being coastal.
		Assert.Equal(5, CenterCommerce(1, capital: true, neighbor: MakeOceanTile(), traits: Civilization.Trait.Seafaring));
	}

	[Fact]
	public void SeafaringBonusIsNotGivenWithoutTheTraitOrACoast() {
		// Coastal, but not seafaring.
		Assert.Equal(1, CenterCommerce(1, neighbor: MakeOceanTile()));
		// Seafaring, but landlocked.
		Assert.Equal(1, CenterCommerce(1, neighbor: MakeDesertTile(), traits: Civilization.Trait.Seafaring));
		// Seafaring and next to a lake, which doesn't count as a coast.
		Assert.Equal(1, CenterCommerce(1, neighbor: MakeLakeTile(), traits: Civilization.Trait.Seafaring));
	}

	[Fact]
	public void CommercialAndSeafaringBonusesStack() {
		Assert.Equal(7, CenterCommerce(Level2Max + 1, neighbor: MakeOceanTile(),
			traits: [Civilization.Trait.Commercial, Civilization.Trait.Seafaring]));
	}

	// Agricultural civs have a 3 food city center. Despotism's tile penalty
	// takes one away from any yield above 2, unless it's the food of a city
	// with fresh water access.
	private int CenterFood(Civilization.Trait[] traits, bool despotism, Tile neighbor = null, bool river = false) {
		(Tile center, City city) = MakeCityCenter(1, neighbor: neighbor, traits: traits);
		city.owner.government = new Government { hasTilePenalty = despotism };
		center.riverNorth = river;
		return center.FoodYield(city).yield;
	}

	private static readonly Civilization.Trait[] Agricultural = [Civilization.Trait.Agricultural];
	private static readonly Civilization.Trait[] NoTraits = [];

	[Fact]
	public void CityCenterFoodIsTwoForOrdinaryCivs() {
		Assert.Equal(2, CenterFood(NoTraits, despotism: false));
		Assert.Equal(2, CenterFood(NoTraits, despotism: true));
		Assert.Equal(2, CenterFood(NoTraits, despotism: true, river: true));
	}

	[Fact]
	public void AgriculturalCivsGetThreeFoodInTheCityCenterWithoutDespotism() {
		Assert.Equal(3, CenterFood(Agricultural, despotism: false));
		Assert.Equal(3, CenterFood(Agricultural, despotism: false, neighbor: MakeDesertTile()));
	}

	[Fact]
	public void DespotismTakesTheThirdCityCenterFoodFromAgriculturalCivsWithoutFreshWater() {
		Assert.Equal(2, CenterFood(Agricultural, despotism: true));
		Assert.Equal(2, CenterFood(Agricultural, despotism: true, neighbor: MakeDesertTile()));
		// The ocean isn't fresh water.
		Assert.Equal(2, CenterFood(Agricultural, despotism: true, neighbor: MakeOceanTile()));
	}

	[Fact]
	public void AgriculturalCityCenterOnARiverKeepsThirdFoodUnderDespotism() {
		Assert.Equal(3, CenterFood(Agricultural, despotism: true, river: true));
	}

	[Fact]
	public void AgriculturalCityCenterNextToALakeKeepsThirdFoodUnderDespotism() {
		Assert.Equal(3, CenterFood(Agricultural, despotism: true, neighbor: MakeLakeTile()));
	}

	[Fact]
	public void FreshWaterExemptionOnlyCoversFood() {
		// A metropolis center makes 3 shields and 3 commerce, which despotism
		// still reduces, even for an agricultural civ beside a lake.
		(Tile center, City city) = MakeCityCenter(Level2Max + 1, neighbor: MakeLakeTile(), traits: Agricultural);
		city.owner.government = new Government { hasTilePenalty = true };

		Assert.Equal(3, center.FoodYield(city).yield);
		Assert.Equal(2, center.ProductionYield(city).yield);
		Assert.Equal(2, center.CommerceYield(city).yield);
	}

	[Fact]
	public void FreshWaterExemptionDoesNotApplyToOtherTiles() {
		(_, City city) = MakeCityCenter(1, neighbor: MakeLakeTile(), traits: Agricultural);
		city.owner.government = new Government { hasTilePenalty = true };

		Tile farm = MakeDesertTile();
		farm.overlayTerrainType.baseFoodProduction = 3;
		Assert.Equal(2, farm.FoodYield(city).yield);
	}

	[Fact]
	public void BonusesDoNotApplyToOrdinaryTiles() {
		(Tile center, City city) = MakeCityCenter(1, neighbor: MakeOceanTile(),
			traits: [Civilization.Trait.Commercial, Civilization.Trait.Seafaring]);
		Tile ordinary = MakeDesertTile();
		Assert.Equal(0, ordinary.CommerceYield(city).yield);
	}
}
