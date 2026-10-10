using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace QueryCiv3.Biq {
	// These two value-indexer helper structs help for getting repeating data
	// For instance, to see if a unit is available to civ 5, these structs allow `bool myBool = myPrto.AvailableTo[5]`
	// However, probably a good TODO would be generalizing these for multiple cases
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct PRTOTERR {
		// 1 byte for each section in TERR
		// In Biq files, this is fixed at 14, so we can treat this as static instead of dynamic
		private fixed byte Terr[14];
		public bool this[int index] { get => Util.GetFlag(Terr[index], 0); }
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct PRTORACE {
		private fixed byte Race[4];
		public bool this[int index] { get => Util.GetFlag(Race[index / 8], index % 8); }

		private const int MAX_CIV = 32;

		// Find an index of civilization to which a unit belongs an a unique unit.
		// Returns -1 if a unit is not unique.
		public int GetUniqueCivIndex() {
			int civIndex = -1;

			// Skip barbarians (index 0)
			for (int i = 1; i < MAX_CIV; i++) {
				if (this[i]) {
					if (civIndex != -1) {
						return -1; // More than one civilization found
					}

					civIndex = i;
				}
			}

			return civIndex;
		}

		public IEnumerable<int> GetAvailableCivIndexes() {
			for (int i = 0; i < MAX_CIV; i++) {
				if (this[i]) {
					yield return i;
				}
			}
		}

		public IEnumerable<int> GetUnavailableCivIndexes() {
			for (int i = 0; i < MAX_CIV; i++) {
				if (!this[i]) {
					yield return i;
				}
			}
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct PRTO {
		public int Length;
		public int ZoneOfControl;

		private fixed byte Text[64];
		public string Name { get => Util.GetString(ref this, 8, 32); }
		public string CivilopediaEntry { get => Util.GetString(ref this, 40, 32); }

		public int BombardStrength;
		public int BombardRange;
		public int Capacity;
		public int ShieldCost;
		public int Defense;
		public int IconIndex;
		public int Attack;
		public int OperationalRange;
		public int PopulationCost;
		public int RateOfFire;
		public int Movement;
		public int Required;
		public int UpgradeTo;
		public int RequiredResource1;
		public int RequiredResource2;
		public int RequiredResource3;

		private fixed byte Flags1[8];
		public bool Wheeled { get => Util.GetFlag(Flags1[0], 0); }
		public bool FootSoldier { get => Util.GetFlag(Flags1[0], 1); }
		public bool Blitz { get => Util.GetFlag(Flags1[0], 2); }
		public bool CruiseMissile { get => Util.GetFlag(Flags1[0], 3); }
		public bool AllTerrainAsRoads { get => Util.GetFlag(Flags1[0], 4); }
		public bool Radar { get => Util.GetFlag(Flags1[0], 5); }
		public bool Amphibious { get => Util.GetFlag(Flags1[0], 6); }
		public bool Submarine { get => Util.GetFlag(Flags1[0], 7); }

		public bool CanCarryAircraft { get => Util.GetFlag(Flags1[1], 0); }
		public bool Draft { get => Util.GetFlag(Flags1[1], 1); }
		public bool Immobile { get => Util.GetFlag(Flags1[1], 2); }
		public bool SinkInSea { get => Util.GetFlag(Flags1[1], 3); }
		public bool SinkInOcean { get => Util.GetFlag(Flags1[1], 4); }
		// unused
		public bool CanCarryFootUnitsOnly { get => Util.GetFlag(Flags1[1], 6); }
		public bool StartsGoldenAge { get => Util.GetFlag(Flags1[1], 7); }

		public bool NuclearWeapon { get => Util.GetFlag(Flags1[2], 0); }
		public bool HiddenNationality { get => Util.GetFlag(Flags1[2], 1); }
		public bool Army { get => Util.GetFlag(Flags1[2], 2); }
		public bool Leader { get => Util.GetFlag(Flags1[2], 3); }
		public bool ICBM { get => Util.GetFlag(Flags1[2], 4); }
		public bool Stealth { get => Util.GetFlag(Flags1[2], 5); }
		public bool CanSeeSubmarines { get => Util.GetFlag(Flags1[2], 6); }
		public bool TacticalMissile { get => Util.GetFlag(Flags1[2], 7); }

		public bool CanCarryTacticalMissiles { get => Util.GetFlag(Flags1[3], 0); }
		public bool RangedAttackAnimations { get => Util.GetFlag(Flags1[3], 1); }
		public bool TurnToAttack { get => Util.GetFlag(Flags1[3], 2); }
		public bool LethalLandBombardment { get => Util.GetFlag(Flags1[3], 3); }
		public bool LethalSeaBombardment { get => Util.GetFlag(Flags1[3], 4); }
		public bool King { get => Util.GetFlag(Flags1[3], 5); }
		public bool RequiresEscort { get => Util.GetFlag(Flags1[3], 6); }

		public bool AIOffense { get => Util.GetFlag(Flags1[4], 0); }
		public bool AIDefense { get => Util.GetFlag(Flags1[4], 1); }
		public bool AIArtillery { get => Util.GetFlag(Flags1[4], 2); }
		public bool AIExplore { get => Util.GetFlag(Flags1[4], 3); }
		public bool AIArmy { get => Util.GetFlag(Flags1[4], 4); }
		public bool AICruiseMissile { get => Util.GetFlag(Flags1[4], 5); }
		public bool AIAirBombard { get => Util.GetFlag(Flags1[4], 6); }
		public bool AIAirDefense { get => Util.GetFlag(Flags1[4], 7); }

		public bool AINavalPower { get => Util.GetFlag(Flags1[5], 0); }
		public bool AIAirTransport { get => Util.GetFlag(Flags1[5], 1); }
		public bool AINavalTransport { get => Util.GetFlag(Flags1[5], 2); }
		public bool AINavalCarrier { get => Util.GetFlag(Flags1[5], 3); }
		public bool AITerraform { get => Util.GetFlag(Flags1[5], 4); }
		public bool AISettle { get => Util.GetFlag(Flags1[5], 5); }
		public bool AILeader { get => Util.GetFlag(Flags1[5], 6); }
		public bool AITacticalNuke { get => Util.GetFlag(Flags1[5], 7); }

		public bool AIICBM { get => Util.GetFlag(Flags1[6], 0); }
		public bool AINavalMissileTransport { get => Util.GetFlag(Flags1[6], 1); }
		public bool AIFlag { get => Util.GetFlag(Flags1[6], 2); }
		public bool AIKing { get => Util.GetFlag(Flags1[6], 3); }

		public PRTORACE AvailableTo; // Binary flag for each civ 0-31
		private fixed byte Flags2[8];

		public int Type; // 0: land, 1: sea, 2: air
		public static readonly int TYPE_LAND = 0;
		public static readonly int TYPE_SEA = 1;
		public static readonly int TYPE_AIR = 2;

		public int OtherStrategy;
		public int HPBonus;

		private fixed byte Flags3[20];
		public bool SkipTurn { get => Util.GetFlag(Flags3[0], 0); }
		public bool Wait { get => Util.GetFlag(Flags3[0], 1); }
		public bool Fortify { get => Util.GetFlag(Flags3[0], 2); }
		public bool Disband { get => Util.GetFlag(Flags3[0], 3); }
		public bool GoTo { get => Util.GetFlag(Flags3[0], 4); }
		public bool Explore { get => Util.GetFlag(Flags3[0], 5); }
		public bool Sentry { get => Util.GetFlag(Flags3[0], 6); }

		public bool Load { get => Util.GetFlag(Flags3[4], 0); }
		public bool Unload { get => Util.GetFlag(Flags3[4], 1); }
		public bool Airlift { get => Util.GetFlag(Flags3[4], 2); }
		public bool Pillage { get => Util.GetFlag(Flags3[4], 3); }
		public bool Bombard { get => Util.GetFlag(Flags3[4], 4); }
		public bool Airdrop { get => Util.GetFlag(Flags3[4], 5); }
		public bool BuildArmy { get => Util.GetFlag(Flags3[4], 6); }
		public bool FinishImprovements { get => Util.GetFlag(Flags3[4], 7); }

		public bool UpgradeUnit { get => Util.GetFlag(Flags3[5], 0); }
		public bool Capture { get => Util.GetFlag(Flags3[5], 1); }
		//Four unused bits
		public bool Telepad { get => Util.GetFlag(Flags3[5], 6); }
		public bool Teleportable { get => Util.GetFlag(Flags3[5], 7); }
		public bool StealthAttack { get => Util.GetFlag(Flags3[6], 0); }
		public bool Charm { get => Util.GetFlag(Flags3[6], 1); }
		public bool ScienceAge { get => Util.GetFlag(Flags3[6], 5); }

		public bool BuildColony { get => Util.GetFlag(Flags3[8], 0); }
		public bool BuildCity { get => Util.GetFlag(Flags3[8], 1); }
		public bool BuildRoad { get => Util.GetFlag(Flags3[8], 2); }
		public bool BuildRailroad { get => Util.GetFlag(Flags3[8], 3); }
		public bool BuildFortress { get => Util.GetFlag(Flags3[8], 4); }
		public bool BuildMine { get => Util.GetFlag(Flags3[8], 5); }
		public bool Irrigate { get => Util.GetFlag(Flags3[8], 6); }
		public bool ClearForest { get => Util.GetFlag(Flags3[8], 7); }

		public bool ClearJungle { get => Util.GetFlag(Flags3[9], 0); }
		public bool PlantForest { get => Util.GetFlag(Flags3[9], 1); }
		public bool ClearPollution { get => Util.GetFlag(Flags3[9], 2); }
		public bool Automate { get => Util.GetFlag(Flags3[9], 3); }
		public bool JoinCity { get => Util.GetFlag(Flags3[9], 4); }
		public bool BuildAirfield { get => Util.GetFlag(Flags3[9], 5); }
		public bool BuildRadarTower { get => Util.GetFlag(Flags3[9], 6); }
		public bool BuildOutpost { get => Util.GetFlag(Flags3[9], 7); }

		public bool BuildBarricade { get => Util.GetFlag(Flags3[10], 0); }

		public bool Bombing { get => Util.GetFlag(Flags3[12], 0); }
		public bool Recon { get => Util.GetFlag(Flags3[12], 1); }
		public bool Intercept { get => Util.GetFlag(Flags3[12], 2); }
		public bool Rebase { get => Util.GetFlag(Flags3[12], 3); }
		public bool PrecisionBombing { get => Util.GetFlag(Flags3[12], 4); }

		public int BombardEffects;
		public PRTOTERR IgnoreMovementCost;
		public int RequireSupport;
		private fixed byte UnknownBuffer[16];
		public int EnslaveResults;
		private fixed byte UnknownBuffer2[4];
		public int NumberOfStealthTargets;

		/*
            Dynamic length gap
            In BIQ files, for each stealth target, there's an integer ID with its PRTO#
            Data is instead stored in 2d array PrtoPrto
        */

		private fixed byte UnknownBuffer3[8];
		public byte CreateCraters;
		public float WorkerStrength;
		private fixed byte UnknownBuffer4[4];
		public int AirDefense;
	}
}
