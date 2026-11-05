using System;
using System.Runtime.InteropServices;

namespace QueryCiv3.Sav {
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct TILE {
		private fixed byte HeaderText1[4];
		public int Length1;
		public byte RiverInfo;
		public bool RiverNorth { get => Util.GetFlag(RiverInfo, 0); }
		public bool RiverNortheast { get => Util.GetFlag(RiverInfo, 1); }
		public bool RiverEast { get => Util.GetFlag(RiverInfo, 2); }
		public bool RiverSoutheast { get => Util.GetFlag(RiverInfo, 3); }
		public bool RiverSouth { get => Util.GetFlag(RiverInfo, 4); }
		public bool RiverSouthwest { get => Util.GetFlag(RiverInfo, 5); }
		public bool RiverWest { get => Util.GetFlag(RiverInfo, 6); }
		public bool RiverNorthwest { get => Util.GetFlag(RiverInfo, 7); }
		public byte Owner;
		private fixed byte UnknownBuffer[2];
		public int ResourceID;
		public int TopUnitID;
		public byte TextureLocation; // Between 0 and 80 inclusive (9*9 grid)
		public byte TextureFile; // 0: xtgc, 1: xgpc, 2: xdgc, 3: xdpc, 4: xdgp, 5: xggc, 6: wcso, 7: wsss, 8: wooo
		private fixed byte Flags1[6];
		public short BarbarianTribe; // -1: there is no camp on the tile, >= 0: there is a barb camp on the tile, the number is the barb tribe
		public short CityID;
		public short ColonyID;
		public short Continent;
		private fixed byte UnknownBuffer3[4];
		public bool HasRuins;
		private fixed byte PaddingBuffer[3];

		// if civ 3 conquests file (for now we'll assume this is always true)
		private fixed byte HeaderText2[4];
		public int Length2;

		// TODO: Most flags need to be added
		private fixed byte Flags2[12];
		public bool Road { get => Util.GetFlag(Flags2[0], 0); }
		public bool Railroad { get => Util.GetFlag(Flags2[0], 1); }
		public bool Mine { get => Util.GetFlag(Flags2[0], 2); }
		public bool Irrigation { get => Util.GetFlag(Flags2[0], 3); }
		public bool Fortress { get => Util.GetFlag(Flags2[0], 4); }
		public bool GoodyHut { get => Util.GetFlag(Flags2[0], 5); }
		public bool Pollution { get => Util.GetFlag(Flags2[0], 6); }
		public bool BarbarianCamp { get => Util.GetFlag(Flags2[0], 7); }

		public bool Craters { get => Util.GetFlag(Flags2[1], 0); }

		public bool Barricade { get => Util.GetFlag(Flags2[3], 4); }
		public bool Airfield { get => Util.GetFlag(Flags2[3], 5); }
		public bool RadarTower { get => Util.GetFlag(Flags2[3], 6); }
		public bool Outpost { get => Util.GetFlag(Flags2[3], 7); }

		public int BaseTerrain { get => Flags2[5] & 0x0f; }
		public int OverlayTerrain { get => (Flags2[5] & 0xf0) >> 4; }

		public bool BonusShield { get => Util.GetFlag(Flags2[10], 0); }
		public bool SnowCapped { get => Util.GetFlag(Flags2[10], 4); }
		public bool PineForest { get => Util.GetFlag(Flags2[10], 5); }

		private fixed byte HeaderText3[4];
		public int Length3;
		private fixed byte UnknownBuffer4[4];
		// end if

		private fixed byte HeaderText4[4];
		public int Length4;
		public IntBitmap ExploredBy;
		public IntBitmap VisibleTo; // Visible right now to civ by units
		public IntBitmap VisibleTo2; // Visible right now to civ by ???
		public IntBitmap VisibleTo3; // Visible right now to civ by city
		private fixed byte UnknownBuffer5[4];
		public short CityIDOfCitizen;
		public fixed short TradeRoutes[32];
		public IMPROVEMENTS_KNOWLEDGE OverlayKnowledge;
		private fixed byte UnknownBuffer6[10];
	}

	// IMPROVEMENTS_KNOWLEDGE: This contains a list of 32 entries-indexes (Players),
	// and each one consists of a single byte that answers
	// "which improvements on this tile does this player know about?"

	// road		        .......1
	// railroad	        ......1.
	// mine		        .....1..
	// irrigation	    ....1...
	// fortress	        ...1....
	// goody hut	    ..1.....
	// pollution	    .1......
	// barbarian camp   1.......

	// does not include radar towers, barricades, craters
	// which seem to be "public" knowledge
	// These are also "Terrain Enhancements" not "Improvements",
	// which might be a factor in the game's logic
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct IMPROVEMENTS_KNOWLEDGE {
		private fixed byte Values[32];
		public bool this[int index, int i] { get => Util.GetFlag(Values[index], i); }
	}
}
