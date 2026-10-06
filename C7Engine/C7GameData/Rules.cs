using System.Collections.Generic;
using C7GameData.Save;

namespace C7GameData {
	public class Rules {
		public int MaximumResearchTime;
		public int MinimumResearchTime;
		public int ShieldValueInGold;
		public int ForestValueInShields;
		public int CitizenValueInShields;
		public int TurnPenaltyForEachHurrySacrifice;
		public int MaximumLevel1CitySize;
		public int MaximumLevel2CitySize;
		public int FoodNeededToGrowForLevel1Cities = 20;
		public int FoodNeededToGrowForLevel2Cities = 40;
		public int FoodNeededToGrowForLevel3Cities = 60;
		public float BuildingDiscountForCivTraits = .5f;
		public string StartUnitType1;
		public string StartUnitType2;
		public string ScoutUnitType;
		public int MaxRankOfWorkableTiles;
		public int MaxRankOfBarbarianCampTiles;
		public int DefaultDealDuration;
		public float TreasuryInterestRate = .05f;
		public int MaxInterest = 50;
		public int ShieldCostPerGold;
		public float ShieldRateForDisbanding; // per cent
		public bool AllowLesserUnitProduction; // for example, allow building a Spearman/Pikeman when we can build a Musketman (simultaneously)
		public int RadarTileVisibility; // how many tiles, a unit with the Radar ability, can see ahead

		// Terrain that is impassable to some unit types but not others. The outer
		// key is a unit ability, the inner key a terrain key, and the value the
		// improvements that grant a unit with that ability passage. In Civ3 a
		// wheeled unit may enter mountains, jungle, marsh and volcano, but only
		// once the tile is roaded or railed.
		//
		// Terrain that is impassable to every unit is not listed here - that is
		// the `impassable` flag on the terrain itself, and no improvement on the
		// tile lifts it.
		public Dictionary<SaveUnitPrototype.Flag, Dictionary<string, string[]>> passability = [];
	}
}
