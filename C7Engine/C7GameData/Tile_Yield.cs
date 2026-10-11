using System;
using System.Linq;
using C7Engine;

namespace C7GameData;

public partial class Tile {
	public enum YieldType {
		Commerce,
		Food,
		Production
	}

	public class Yield {
		public readonly Tile tile;
		public readonly YieldType type;
		public int penalty = 0;
		public int bonus = 0;
		public readonly int baseYield = 0;
		public int yield { get => baseYield + bonus - penalty; }

		public static Yield CalculateForCity(Tile tile, int yield, YieldType type, City city) {
			return new Yield(tile, yield, type)
				.ApplyTerrainImprovementModifiers(tile)
				.ApplyCityModifiers(city)
				.ApplyPlayerModifiers(city.owner);
		}

		public static Yield CalculateForPlayer(Tile tile, int yield, YieldType type, Player player) {
			return new Yield(tile, yield, type)
				.ApplyTerrainImprovementModifiers(tile)
				.ApplyPlayerModifiers(player);
		}

		public Yield(Tile tile, int baseYield, YieldType type) {
			this.tile = tile;
			this.baseYield = baseYield;
			this.type = type;
		}

		private Yield ApplyPlayerModifiers(Player player) {
			player.government.tileModifier?.Invoke(this);
			return this;
		}

		private Yield ApplyCityModifiers(City city) {
			// TODO: Skip obsolete buildings (see Building.IsObsolete).
			city.GetBuildings().ForEach(b => b.building.tileModifier?.Invoke(this));
			return this;
		}

		private Yield ApplyTerrainImprovementModifiers(Tile tile) {
			tile.overlays.GetImprovements().ToList().ForEach(ti => ti.tileModifier?.Invoke(this));
			return this;
		}
	}

	//Convenience method for printing the yield
	public string YieldString(Player player) {
		return $"{this.FoodYield(player).yield}/{this.ProductionYield(player).yield}/{this.CommerceYield(player).yield}";
	}

	// Food yield
	private int BaseFoodYield(Player player) {
		if (this.HasPollution()) return 0;
		int yield = overlayTerrainType.baseFoodProduction;
		if (this.Resource != Resource.NONE && player.KnowsAboutResource(Resource)) {
			yield += this.Resource.FoodBonus;
		}

		if (this.HasCity()) {
			// All city centers have a food yield of 2, regardless of bonus
			// food. See https://wiki.civforum.de/wiki/Stadtfeldertrag_(Civ3).
			yield = 2;

			// TODO: For agricultural civilizations, the city field produces
			// a food yield of three food, but this is reduced to two by the
			// despotism penalty, unless the city is located on a fresh
			// water source or has already reached city size (≥ 7)
		}

		yield += this.overlays.GetBaseYieldBonus(YieldType.Food);

		if (this.HasCraters())
			yield--;

		return yield;
	}
	public Yield FoodYield(Player player) {
		int yield = BaseFoodYield(player);
		return Yield.CalculateForPlayer(this, yield, YieldType.Food, player);
	}
	public Yield FoodYield(City city) {
		int yield = BaseFoodYield(city.owner);
		return Yield.CalculateForCity(this, yield, YieldType.Food, city);
	}

	// Production yield
	private int BaseProductionYield(Player player) {
		if (this.HasPollution()) return 0;
		int yield = overlayTerrainType.baseShieldProduction;
		if (overlayTerrainType.Key == "grassland" && this.isBonusShield) {
			yield++;
		}

		if (HasCity()) {
			// City centers always have 1 shield prior to any bonuses
			// resources, regardless of the terrain.
			// See https://wiki.civforum.de/wiki/Stadtfeldertrag_(Civ3).
			yield = 1;

			// There is a size bonus for larger cities.
			if (cityAtTile.residents.Count > EngineStorage.gameData.rules.MaximumLevel1CitySize
				&& cityAtTile.residents.Count <= EngineStorage.gameData.rules.MaximumLevel2CitySize) {
				yield += 1;
			} else if (cityAtTile.residents.Count > EngineStorage.gameData.rules.MaximumLevel2CitySize) {
				yield += 2;

				// Industrious civs get +1 production in metropolises
				if (cityAtTile.owner.civilization.traits.Contains(Civilization.Trait.Industrious)) {
					yield += 1;
				}
			}
		}

		// Bonus resources provide a boost in yield regardless of whether
		// there is a city.
		if (Resource != Resource.NONE && player.KnowsAboutResource(Resource)) {
			yield += this.Resource.ShieldsBonus;
		}

		yield += this.overlays.GetBaseYieldBonus(YieldType.Production);

		return yield;
	}
	public Yield ProductionYield(Player player) {
		int yield = BaseProductionYield(player);
		return Yield.CalculateForPlayer(this, yield, YieldType.Production, player);
	}
	public Yield ProductionYield(City city) {
		int yield = BaseProductionYield(city.owner);
		return Yield.CalculateForCity(this, yield, YieldType.Production, city);
	}

	// Commerce yield
	private int BaseCommerceYield(Player player) {
		if (this.HasPollution()) return 0;
		int yield = overlayTerrainType.baseCommerceProduction;
		if (this.Resource != Resource.NONE && player.KnowsAboutResource(Resource)) {
			yield += this.Resource.CommerceBonus;
		}

		bool borderRiver = this.BordersRiver();

		if (borderRiver) {
			yield += 1;
		}

		// See https://wiki.civforum.de/wiki/Stadtfeldertrag_(Civ3)
		if (HasCity()) {
			int regularCityYield;
			if (cityAtTile.residents.Count <= EngineStorage.gameData.rules.MaximumLevel1CitySize) {
				regularCityYield = 1;
			} else if (cityAtTile.residents.Count <= EngineStorage.gameData.rules.MaximumLevel2CitySize) {
				regularCityYield = 2;
			} else {
				regularCityYield = 3;
			}
			if (borderRiver) {
				regularCityYield += 1;
			}
			if (this.Resource != Resource.NONE && player.KnowsAboutResource(Resource)) {
				regularCityYield += this.Resource.CommerceBonus;
			}

			int capitalCityYield = 0;
			if (cityAtTile.IsCapital()) {
				capitalCityYield = 4;
			}

			yield = Math.Max(regularCityYield, capitalCityYield);
		}

		yield += this.overlays.GetBaseYieldBonus(YieldType.Commerce);

		return yield;
	}
	public Yield CommerceYield(Player player) {
		int yield = BaseCommerceYield(player);

		// TODO: handle the commerce bonus for costal cities+seafaring
		// TODO: handle the commerce bonus for commerial civs
		return Yield.CalculateForPlayer(this, yield, YieldType.Commerce, player);
	}
	public Yield CommerceYield(City city) {
		int yield = BaseCommerceYield(city.owner);

		return Yield.CalculateForCity(this, yield, YieldType.Commerce, city);
	}
}
