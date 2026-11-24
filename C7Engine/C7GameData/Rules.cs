namespace C7GameData {
	// Extra yield added to tiles for a player in a golden age.
	public struct GoldenAgeBonus {
		public int Shields;
		public int Commerce;
	}

	public class Rules {
		// read from the BIQ's RULE section when importing Civ3 data
		public int GoldenAgeDuration = 20;
		// Not in Civ3's BIQ, so this is also the default for Civ3 imports and anything that doesn't specify it
		public GoldenAgeBonus GoldenAgeBonuses = new() { Shields = 1, Commerce = 1 };
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
		public int CapitalNeighborDistance = 8; // for capital relocation calculations
		public float BuildingDiscountForCivTraits = .5f;
		public string StartUnitType1;
		public string StartUnitType2;
		public string ScoutUnitType;
		public int MaxRankOfWorkableTiles;
		public int MaxRankOfBarbarianCampTiles;
		public int BarbarianCampGold = 25; // gold for dispersing a barbarian camp; not in Civ3's BIQ, so this is also the default for Civ3 saves and older OpenCiv3 saves
		public int DefaultDealDuration;
		public float TreasuryInterestRate = .05f;
		public int MaxInterest = 50;
		public int ShieldCostPerGold;
		public float ShieldRateForDisbanding; // per cent
		public bool AllowLesserUnitProduction; // for example, allow building a Spearman/Pikeman when we can build a Musketman (simultaneously)
		public int RadarTileVisibility; // how many tiles, a unit with the Radar ability, can see ahead
	}
}
