using System;
using System.Text.Json.Serialization;

namespace C7GameData {
	public class Government {
		public ID id;
		public string name;
		public string civilopediaEntry;

		// If non-null, the tech required to use this government.
		public ID prerequisiteTech;

		// If true, this is the starting government.
		public bool defaultType;

		// If true, this is the government used while switching governments.
		public bool transitionType;

		// The "despotism penalty" applies for this goverment; reduces all
		// commerce, production and food output done by citizen laborers by -1
		// when they produce more than 2.
		public bool hasTilePenalty {
			get => _hasTilePenalty;
			set {
				_hasTilePenalty = value;
				if (value) {
					tileModifier += TilePenalty;
				}
			}
		}
		private bool _hasTilePenalty;

		// +1 commerce any tile with at least 1 commerce.
		public bool hasTradeBonus {
			get => _hasTradeBonus;
			set {
				_hasTradeBonus = value;
				if (value) {
					tileModifier += TradeBonus;
				}
			}
		}
		private bool _hasTradeBonus;

		[JsonIgnore]
		public Action<Tile.Yield> tileModifier;

		// See https://codehappy.net/apolyton/threads/46801-1.htm and
		// https://forums.civfanatics.com/threads/everything-about-corruption-c3c-edition.76619/.
		public enum CorruptionType {
			Minimal,
			Nuisance,
			Problematic,
			Rampant,
			Catastrophic,
			Communal,
			Off
		};
		public CorruptionType corruptionType;

		public enum HurryProductionType {
			CannotHurry,
			ForcedLabor,
			PaidLabor,
		};
		public HurryProductionType hurryingType;

		public int draftLimit;
		public int militaryPoliceLimit;
		public int workerRate;

		public bool allUnitsFree;
		public int freeUnitsPerTown;
		public int freeUnitsPerCity;
		public int freeUnitsPerMetropolis;
		public int unitCost;

		private static void TradeBonus(Tile.Yield yield) {
			if (yield.type == Tile.YieldType.Commerce && yield.baseYield > 0) {
				yield.bonus += 1;
			}
		}

		private static void TilePenalty(Tile.Yield yield) {
			Tile t = yield.tile;

			// Agricultural civs keep the third food of their city center when the
			// city has fresh water access. Shields and commerce are still penalized.
			bool exempt = yield.type == Tile.YieldType.Food
				&& t.HasCity()
				&& t.cityAtTile.owner.IsAgricultural()
				&& t.cityAtTile.hasFreshwaterAccess;

			if (!exempt) {
				yield.penalty += yield.yield > 2 ? 1 : 0;
			}
		}
	}
}
