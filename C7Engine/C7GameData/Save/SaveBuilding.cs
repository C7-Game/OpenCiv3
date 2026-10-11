using System.Collections.Generic;
using System.Linq;

namespace C7GameData.Save {
	public class SaveBuilding {
		public enum Flag {
			IsCenterOfEmpire,
			VeteranGroundUnits,
			VeteranSeaUnits,
			VeteranAirUnits,
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
			ReplacesOtherBuildings,
			MustBeNearWater,
			CanMeltdown,
			RemovesPopulationPollution,
			ReducesBuildingPollution,
			ResistantToBribery,
			AllowsWaterTrade,
			AllowsAirTrade,
			ReducesWarWeariness,
			DoublesSacrifice,
			ContinentalMoodEffects,
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

			// The name of the building whose happiness effect this wonder
			// doubles (like the Temple for the Oracle), if any.
			public string buildingWithDoubledHappiness;
		}

		public string name;
		public int shieldCost;
		public int populationCost;
		public ID requiredTech;
		public string requiredBuilding;

		// If more than one, the number of required buildings the player must
		// have across their empire. Otherwise the required building must be in
		// the same city.
		public int numberOfRequiredBuildings;
		public ID? requiredGovernment;

		// The spaceship part index of this building, if it is a spaceship part.
		public int? spaceshipPart;

		// The number of armies the player must have to build this.
		public int numberOfArmiesRequired;

		// The name of the unit prototype this building produces, if any, and
		// the frequency of that production. The frequency is only meaningful
		// if there is a unit produced.
		public string unitProduced;
		public int unitFrequency;
		public GreatWonderProperties? greatWonderProperties;
		public bool isSmallWonder;
		public int culturePerTurn;
		public int contentFacesInCity;
		public int contentFacesInAllCities;
		public double combatDefenseBonus;
		public double navalDefenseBonus;
		public int navalBombardDefense;
		public int navalPower;
		public int airPower;

		// The fraction of additional shields this building yields in its city,
		// e.g. 0.5 for +50%.
		public double productionBonus;

		// The fraction of additional research, luxury and commerce this
		// building yields in its city, e.g. 0.5 for +50%.
		public double researchBonus;
		public double luxuryBonus;
		public double commerceBonus;

		// The amount of pollution this building adds to its city.
		public int pollution;
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
