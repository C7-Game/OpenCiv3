using System;
using System.Runtime.InteropServices;

namespace QueryCiv3.Biq {
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct BLDG {
		public int Length;

		private fixed byte Text[128];
		public string Description { get => Util.GetString(ref this, 4, 64); }
		public string Name { get => Util.GetString(ref this, 68, 32); }
		public string CivilopediaEntry { get => Util.GetString(ref this, 100, 32); }

		public int DoublesHappiness;
		public int GainInEveryCity;
		public int GainInEveryCityOnContinent;
		public int RequiredBuilding;
		public int Cost;
		public int Culture;
		public int BombardDefense;
		public int NavalBombardDefense;
		public int DefenseBonus;
		public int NavalDefenseBonus;
		public int MaintenanceCost;
		public int ContentFacesAllCities;
		public int ContentFaces;
		public int UnhappyFacesAllCities;
		public int UnhappyFaces;
		public int NumberOfRequiredBuildings;
		public int AirPower;
		public int NavalPower;
		public int Pollution;
		public int Production;
		public int RequiredGovernment;
		public int SpaceshipPart;
		public int RequiredAdvance;
		public int RenderedObsoleteBy;
		public int RequiredResource1;
		public int RequiredResource2;

		private fixed byte Flags[16];
		public bool CenterOfEmpire { get => Util.GetFlag(Flags[0], 0); }
		public bool VeteranGroundUnits { get => Util.GetFlag(Flags[0], 1); }
		public bool Plus50PercentResearch { get => Util.GetFlag(Flags[0], 2); }
		public bool Plus50PercentLuxury { get => Util.GetFlag(Flags[0], 3); }
		public bool Plus50PercentCommerce { get => Util.GetFlag(Flags[0], 4); }
		public bool RemovesPopulationPollution { get => Util.GetFlag(Flags[0], 5); }
		public bool ReducesBuildingPollution { get => Util.GetFlag(Flags[0], 6); }
		public bool ResistantToBribery { get => Util.GetFlag(Flags[0], 7); }
		public bool ReducesCorruption { get => Util.GetFlag(Flags[1], 0); }
		public bool DoublesCityGrowthRate { get => Util.GetFlag(Flags[1], 1); }
		public bool IncreasesLuxuryTrade { get => Util.GetFlag(Flags[1], 2); }
		public bool AllowsCitySize2 { get => Util.GetFlag(Flags[1], 3); }
		public bool AllowsCitySize3 { get => Util.GetFlag(Flags[1], 4); }
		public bool ReplacesOtherBuildings { get => Util.GetFlag(Flags[1], 5); }
		public bool MustBeNearWater { get => Util.GetFlag(Flags[1], 6); }
		public bool MustBeNearRiver { get => Util.GetFlag(Flags[1], 7); }
		public bool CanMeltdown { get => Util.GetFlag(Flags[2], 0); }
		public bool VeteranSeaUnits { get => Util.GetFlag(Flags[2], 1); }
		public bool VeteranAirUnits { get => Util.GetFlag(Flags[2], 2); }
		public bool Capitalization { get => Util.GetFlag(Flags[2], 3); }
		public bool AllowsWaterTrade { get => Util.GetFlag(Flags[2], 4); }
		public bool AllowsAirTrade { get => Util.GetFlag(Flags[2], 5); }
		public bool ReducesWarWeariness { get => Util.GetFlag(Flags[2], 6); }
		public bool IncreasesShieldsInWater { get => Util.GetFlag(Flags[2], 7); }
		public bool IncreasesFoodInWater { get => Util.GetFlag(Flags[3], 0); }
		public bool IncreasesTradeInWater { get => Util.GetFlag(Flags[3], 1); }
		public bool CharmBarrier { get => Util.GetFlag(Flags[3], 2); }
		public bool StealthAttackBarrier { get => Util.GetFlag(Flags[3], 3); }
		public bool ActsAsGeneralTelepad { get => Util.GetFlag(Flags[3], 4); }
		public bool DoublesSacrifice { get => Util.GetFlag(Flags[3], 5); }
		public bool CanBuildUnits { get => Util.GetFlag(Flags[3], 6); }
		public bool GoodsMustBeInCityRadius { get => Util.GetFlag(Flags[3], 7); }
		public bool CoastalInstallation { get => Util.GetFlag(Flags[4], 0); }
		public bool Militaristic { get => Util.GetFlag(Flags[4], 1); }
		public bool Wonder { get => Util.GetFlag(Flags[4], 2); }
		public bool SmallWonder { get => Util.GetFlag(Flags[4], 3); }
		public bool ContinentalMoodEffects { get => Util.GetFlag(Flags[4], 4); }
		public bool Scientific { get => Util.GetFlag(Flags[4], 5); }
		public bool Commercial { get => Util.GetFlag(Flags[4], 6); }
		public bool Expansionist { get => Util.GetFlag(Flags[4], 7); }
		public bool Religious { get => Util.GetFlag(Flags[5], 0); }
		public bool Industrious { get => Util.GetFlag(Flags[5], 1); }
		public bool Agricultural { get => Util.GetFlag(Flags[5], 2); }
		public bool Seafaring { get => Util.GetFlag(Flags[5], 3); }
		// small wonder characteristics:
		public bool IncreasesLeaderChance { get => Util.GetFlag(Flags[8], 0); }
		public bool AllowsBuildArmy { get => Util.GetFlag(Flags[8], 1); }
		public bool AllowsLargerArmies { get => Util.GetFlag(Flags[8], 2); }
		public bool TreasuryEarnsInterest { get => Util.GetFlag(Flags[8], 3); }
		public bool BuildSpaceshipParts { get => Util.GetFlag(Flags[8], 4); }
		public bool ForbiddenPalace { get => Util.GetFlag(Flags[8], 5); }
		public bool DecreasesMissileSuccess { get => Util.GetFlag(Flags[8], 6); }
		public bool AllowsSpyMissions { get => Util.GetFlag(Flags[8], 7); }
		public bool AllowsEnemyTerritoryHealing { get => Util.GetFlag(Flags[9], 0); }
		public bool RequiresVictoriousArmy { get => Util.GetFlag(Flags[9], 2); }
		public bool RequiresEliteShip { get => Util.GetFlag(Flags[9], 3); }
		// great wonder characteristics:
		public bool SafeSeaTravel { get => Util.GetFlag(Flags[12], 0); }
		public bool GainAnyTechKnownByTwoCivs { get => Util.GetFlag(Flags[12], 1); }
		public bool DoubleCombatVsBarbarians { get => Util.GetFlag(Flags[12], 2); }
		public bool IncreasedShipMovement { get => Util.GetFlag(Flags[12], 3); }
		public bool DoublesResearchOutput { get => Util.GetFlag(Flags[12], 4); }
		public bool IncreasedTrade { get => Util.GetFlag(Flags[12], 5); }
		public bool CheaperUpgrades { get => Util.GetFlag(Flags[12], 6); }
		public bool PaysTradeMaintenance { get => Util.GetFlag(Flags[12], 7); }
		public bool AllowsNuclearWeapons { get => Util.GetFlag(Flags[13], 0); }
		public bool DoubleCityGrowth { get => Util.GetFlag(Flags[13], 1); }
		public bool TwoFreeAdvances { get => Util.GetFlag(Flags[13], 2); }
		public bool ReducedWarWeariness { get => Util.GetFlag(Flags[13], 3); }
		public bool DoubleCityDefenses { get => Util.GetFlag(Flags[13], 4); }
		public bool AllowDiplomaticVictory { get => Util.GetFlag(Flags[13], 5); }
		public bool PlusTwoShipMovement { get => Util.GetFlag(Flags[13], 6); }
		// Unknown flag?
		public bool IncreasedArmyValue { get => Util.GetFlag(Flags[14], 0); }
		public bool TouristAttraction { get => Util.GetFlag(Flags[14], 1); }

		public int NumberOfArmiesRequired;
		public int Flavors;
		private fixed byte UnknownBuffer[4];
		public int UnitProduced;
		public int UnitFrequency;
	}
}
