using System.Collections.Generic;
using System.Linq;

namespace C7GameData.Save {
	public class SaveBuilding {
		public enum Flag {
			IsCenterOfEmpire,
			VeteranGroundUnits,
			VeteranSeaUnits,
			MustBeCoastal,
			MustBeNearRiver,
			IncreasesLuxuryTrade,
			ReducesCorruption,
			IncreasesShieldsInWater,
			IncreasesFoodInWater,
			IncreasesTradeInWater,
			AllowsCitySize2,
			AllowsCitySize3,
			DoublesCityGrowthRate,
			ProvidesWalls,
			CanOnlyBeBuiltInTowns,
			GoodsMustBeInCityRadius,
		}

		// Flags that apply to wonders, both small and great. Kept separate from
		// Flag so that non-wonder buildings don't carry them.
		public enum WonderFlag {
			IncreasesLeaderChance,
			AllowsBuildArmy,
			AllowsLargerArmies,
			TreasuryEarnsInterest,
			BuildSpaceshipParts,
			ForbiddenPalace,
			DecreasesMissileSuccess,
			AllowsSpyMissions,
			AllowsEnemyTerritoryHealing,
			RequiresVictoriousArmy,
			RequiresEliteShip,
			SafeSeaTravel,
			GainAnyTechKnownByTwoCivs,
			DoubleCombatVsBarbarians,
			IncreasedShipMovement,
			DoublesResearchOutput,
			IncreasedTrade,
			CheaperUpgrades,
			PaysTradeMaintenance,
			AllowsNuclearWeapons,
			DoubleCityGrowth,
			TwoFreeAdvances,
			ReducedWarWeariness,
			DoubleCityDefenses,
			AllowDiplomaticVictory,
			PlusTwoShipMovement,
			IncreasedArmyValue,
			TouristAttraction,
		}

		public class GreatWonderProperties {
			// The name of the building this building gives to every city in the
			// empire on on the continent (like the pyramids or the internet).
			public string buildingGainedInEveryCity;
			public string buildingGainedInEveryCityOnContinent;
		}

		public string name;
		public int shieldCost;
		public int populationCost;
		public ID requiredTech;
		public string requiredBuilding;
		public GreatWonderProperties? greatWonderProperties;
		public bool isSmallWonder;
		public int culturePerTurn;
		public int contentFacesInCity;
		public double combatDefenseBonus;
		public int maintenanceCost;
		public int iconRowIndex;
		public ID? renderedObsoleteBy;

		// Assorted boolean flags for the building. They're stored in this set
		// rather than as booleans to avoid bloating the json file.
		public HashSet<Flag> flags = new();

		// Flags specific to small and great wonders.
		public HashSet<WonderFlag> wonderFlags = [];

		// The set of traits this building has. Civilizations with a matching
		// trait get discounted production costs.
		public HashSet<Civilization.Trait> traits = new();

		public HashSet<string> requiredResources = [];

		// Paths to Lua functions 
		public SortedSet<string> onFinishedUnitProduction = [];
		public SortedSet<string> productionPrerequisites = [];
		public SortedSet<string> tileModifiers = [];

		public SaveBuilding() { }
	}
}
