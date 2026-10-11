using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData.Save;
using C7Engine;
using C7Engine.Lua;
using Serilog.Debugging;

namespace C7GameData {
	public class Building : IProducible {
		public class GreatWonderProperties {
			// The building this building gives to every city in the empire on
			// on the continent (like the pyramids or the internet), if any.
			// Implemented in City.GetBuildings.
			public Building buildingGainedInEveryCity;
			public Building buildingGainedInEveryCityOnContinent;

			// The building whose happiness effect this wonder doubles (like the
			// Temple for the Oracle), if any.
			// TODO: Wire up to happiness calculation
			public Building buildingWithDoubledHappiness;
		}

		public string name { get; set; }
		public int shieldCost { get; set; }
		public int populationCost { get; set; } // Will always be equal to 0 in the Civ3 rule set

		// Filled in in SaveGame::ConvertBuildings
		public Tech requiredTech { get; set; }
		public Tech? renderedObsoleteBy;

		// Implemented in CanProduce, along with numberOfRequiredBuildings.
		public Building requiredBuilding;

		// If more than one, the number of required buildings the player must
		// have across their empire. Otherwise the required building must be in
		// the same city.
		public int numberOfRequiredBuildings;

		// Filled in in SaveGame::ConvertBuildings. If non-null, only players
		// with this government can build this. Implemented in CanProduce.
		public Government? requiredGovernment;

		// The spaceship part index of this building, if it is a spaceship part.
		// The restrictions on building one (once per player, not while another
		// city builds it, and needing a building that allows spaceship parts)
		// are implemented in CanProduce.
		// TODO: Wire up to victory conditions when applicable.
		public int? spaceshipPart;

		// The number of armies the player must have to build this. Implemented
		// in CanProduce, see the TODO there on how armies are counted.
		public int numberOfArmiesRequired;

		// TODO: Maybe import the BIQ building "Flavors". They have minimal tangible
		// effect in the base game, so decide whether we want to implement them.

		// The unit this building produces, if any, and the frequency of that
		// production. The frequency is only meaningful if there is a unit
		// produced. Filled in in SaveGame::ConvertUnits.
		// TODO: Wire up so the building produces its unit.
		public UnitPrototype? unitProduced;
		public int unitFrequency;

		List<Func<City, bool>> productionPrerequisites = [];
		public Action<MapUnit> onFinishedUnitProduction;
		public Action<Tile.Yield> tileModifier;

		// Filled in in SaveGame::ConvertBuildings. Non-null for great wonders.
		public GreatWonderProperties? greatWonderProperties;

		public bool isSmallWonder;
		public bool isCenterOfEmpire;
		public bool increasesLuxuryTrade;
		public bool reducesCorruption;
		public bool allowsCitySize2;
		public bool allowsCitySize3;
		public bool doublesCityGrowthRate;
		public bool providesWalls;
		public bool onlyUsefulInTowns;

		// Implemented in CanProduce: the required resources must be in the
		// city's radius.
		public bool goodsMustBeInCityRadius;

		// TODO: Wire up the logic for replacing other buildings.
		public bool replacesOtherBuildings;

		// TODO: Wire up the effects of these flags.
		public bool canMeltdown;
		public bool removesPopulationPollution;
		public bool reducesBuildingPollution;
		public bool resistantToBribery;

		// TODO: Wire up water and air trade to the trade network (see the
		// harbor and airport TODO in TradeNetwork), and war weariness to the
		// city's happiness (see the war weariness TODO in City).
		public bool allowsWaterTrade;
		public bool allowsAirTrade;
		public bool reducesWarWeariness;

		// TODO: Wire up the effect of this flag.
		public bool doublesSacrifice;

		// If true, the all-cities mood effects only apply to cities on the same
		// continent as the city with this building. Implemented in
		// Player.GetNetContentFacesFromOtherCities.
		public bool continentalMoodEffects;
		public StrengthBonus? combatDefenseBonus;

		// Like combatDefenseBonus, but against naval / air units.
		// TODO: Wire these up to actual combat mechanics
		public StrengthBonus? navalDefenseBonus;
		public int navalBombardDefense;
		public int navalPower;
		public int airPower;

		// The fraction of additional shields this building yields in its city,
		// e.g. 0.5 for +50%.
		// TODO: Wire this up to the shields yielded by the city, where that is
		// calculated (see City.CurrentProductionYield).
		public double productionBonus;

		// The fraction of additional research, luxury and commerce this
		// building yields in its city, e.g. 0.5 for +50%. Implemented in
		// City.CurrentCommerceYieldRaw, where commerceBonus applies to taxes.
		public double researchBonus;
		public double luxuryBonus;
		public double commerceBonus;

		// The amount of pollution this building adds to its city.
		// TODO: Find a citation for the Civ 3 pollution mechanics based on a
		// city's total pollution value, and implement them.
		public int pollution;

		// Only used by the AI when scoring what to build (ChooseProducible). The
		// veteran unit effect itself is implemented through
		// onFinishedUnitProduction, which City.AddUnit applies to new units.
		public bool providesVeteranGroundUnits;

		// Wonder flags, shared by small and great wonders.
		//
		// Implemented flags:
		// - treasuryEarnsInterest: the treasury interest in Player.
		// - isForbiddenPalace: the corruption calculations in City and Player.
		// - buildSpaceshipParts: the spaceship part check in CanProduce.
		public bool treasuryEarnsInterest;
		public bool isForbiddenPalace;
		public bool buildSpaceshipParts;

		// This field is purely for matching the BIQ format. The actual science
		// bonus is captured in researchBonus.
		public bool doublesResearchOutput;

		// TODO: These are not tracked yet, so CanProduce currently refuses to
		// build anything with either flag. Update that check when armies and
		// elite ships are tracked on the player.
		public bool requiresVictoriousArmy;
		public bool requiresEliteShip;

		// TODO: Wire up the remaining flags to actual in-game logic and bonuses.
		// They are inert for now.
		public bool increasesLeaderChance;
		public bool allowsBuildArmy;
		public bool allowsLargerArmies;
		public bool decreasesMissileSuccess;
		public bool allowsSpyMissions;
		public bool allowsEnemyTerritoryHealing;
		public bool safeSeaTravel;
		public bool gainAnyTechKnownByTwoCivs;
		public bool doubleCombatVsBarbarians;
		public bool increasedShipMovement;
		public bool increasedTrade;
		public bool cheaperUpgrades;
		public bool paysTradeMaintenance;
		public bool allowsNuclearWeapons;
		public bool doubleCityGrowth;
		public bool twoFreeAdvances;
		public bool reducedWarWeariness;
		public bool doubleCityDefenses;
		public bool allowDiplomaticVictory;
		public bool plusTwoShipMovement;
		public bool increasedArmyValue;
		public bool touristAttraction;

		public int culturePerTurn = 0;
		public int maintenanceCost = 0;

		// The number of unhappy faces that become content in the city with this
		// building.
		public int contentFacesInCity = 0;

		// The number of happy faces that become content in the city with this
		// building. Note that this is less powerful than other sources of
		// unhappiness, like drafting or poprushing, which converts happy faces
		// to sad faces.
		public int unhappyFacesInCity = 0;

		// Like contentFacesInCity and unhappyFacesInCity, but applied to every
		// other city of the owning player. Implemented in
		// Player.GetNetContentFacesFromOtherCities.
		public int contentFacesInAllCities = 0;
		public int unhappyFacesInAllCities = 0;

		public HashSet<Resource> requiredResources { get; set; } = [];

		public int iconRowIndex = 0;

		SaveBuilding dataSource;

		public Building(SaveBuilding building, GameData gameData) {
			dataSource = building;

			name = building.name;
			shieldCost = building.shieldCost;
			populationCost = building.populationCost;
			isSmallWonder = building.isSmallWonder;
			spaceshipPart = building.spaceshipPart;
			numberOfArmiesRequired = building.numberOfArmiesRequired;
			numberOfRequiredBuildings = building.numberOfRequiredBuildings;
			unitFrequency = building.unitFrequency;
			culturePerTurn = building.culturePerTurn;
			maintenanceCost = building.maintenanceCost;
			iconRowIndex = building.iconRowIndex;

			if (building.contentFacesInCity < 0) {
				unhappyFacesInCity = -building.contentFacesInCity;
			} else {
				contentFacesInCity = building.contentFacesInCity;
			}

			if (building.contentFacesInAllCities < 0) {
				unhappyFacesInAllCities = -building.contentFacesInAllCities;
			} else {
				contentFacesInAllCities = building.contentFacesInAllCities;
			}

			if (building.combatDefenseBonus > 0) {
				combatDefenseBonus = new(name, building.combatDefenseBonus);
			}

			if (building.navalDefenseBonus > 0) {
				navalDefenseBonus = new(name, building.navalDefenseBonus);
			}
			navalBombardDefense = building.navalBombardDefense;
			navalPower = building.navalPower;
			airPower = building.airPower;
			productionBonus = building.productionBonus;
			researchBonus = building.researchBonus;
			luxuryBonus = building.luxuryBonus;
			commerceBonus = building.commerceBonus;
			pollution = building.pollution;

			isCenterOfEmpire = building.flags.Contains(SaveBuilding.Flag.IsCenterOfEmpire);
			increasesLuxuryTrade = building.flags.Contains(SaveBuilding.Flag.IncreasesLuxuryTrade);
			reducesCorruption = building.flags.Contains(SaveBuilding.Flag.ReducesCorruption);
			allowsCitySize2 = building.flags.Contains(SaveBuilding.Flag.AllowsCitySize2);
			allowsCitySize3 = building.flags.Contains(SaveBuilding.Flag.AllowsCitySize3);
			doublesCityGrowthRate = building.flags.Contains(SaveBuilding.Flag.DoublesCityGrowthRate);
			providesWalls = building.flags.Contains(SaveBuilding.Flag.ProvidesWalls);
			onlyUsefulInTowns = building.flags.Contains(SaveBuilding.Flag.CanOnlyBeBuiltInTowns);
			goodsMustBeInCityRadius = building.flags.Contains(SaveBuilding.Flag.GoodsMustBeInCityRadius);
			replacesOtherBuildings = building.flags.Contains(SaveBuilding.Flag.ReplacesOtherBuildings);
			canMeltdown = building.flags.Contains(SaveBuilding.Flag.CanMeltdown);
			removesPopulationPollution = building.flags.Contains(SaveBuilding.Flag.RemovesPopulationPollution);
			reducesBuildingPollution = building.flags.Contains(SaveBuilding.Flag.ReducesBuildingPollution);
			resistantToBribery = building.flags.Contains(SaveBuilding.Flag.ResistantToBribery);
			allowsWaterTrade = building.flags.Contains(SaveBuilding.Flag.AllowsWaterTrade);
			allowsAirTrade = building.flags.Contains(SaveBuilding.Flag.AllowsAirTrade);
			reducesWarWeariness = building.flags.Contains(SaveBuilding.Flag.ReducesWarWeariness);
			doublesSacrifice = building.flags.Contains(SaveBuilding.Flag.DoublesSacrifice);
			continentalMoodEffects = building.flags.Contains(SaveBuilding.Flag.ContinentalMoodEffects);
			providesVeteranGroundUnits = building.flags.Contains(SaveBuilding.Flag.VeteranGroundUnits);

			var wonderFlags = building.wonderFlags;
			increasesLeaderChance = wonderFlags.Contains(SaveBuilding.WonderFlag.IncreasesLeaderChance);
			allowsBuildArmy = wonderFlags.Contains(SaveBuilding.WonderFlag.AllowsBuildArmy);
			allowsLargerArmies = wonderFlags.Contains(SaveBuilding.WonderFlag.AllowsLargerArmies);
			treasuryEarnsInterest = wonderFlags.Contains(SaveBuilding.WonderFlag.TreasuryEarnsInterest);
			buildSpaceshipParts = wonderFlags.Contains(SaveBuilding.WonderFlag.BuildSpaceshipParts);
			isForbiddenPalace = wonderFlags.Contains(SaveBuilding.WonderFlag.ForbiddenPalace);
			decreasesMissileSuccess = wonderFlags.Contains(SaveBuilding.WonderFlag.DecreasesMissileSuccess);
			allowsSpyMissions = wonderFlags.Contains(SaveBuilding.WonderFlag.AllowsSpyMissions);
			allowsEnemyTerritoryHealing = wonderFlags.Contains(SaveBuilding.WonderFlag.AllowsEnemyTerritoryHealing);
			requiresVictoriousArmy = wonderFlags.Contains(SaveBuilding.WonderFlag.RequiresVictoriousArmy);
			requiresEliteShip = wonderFlags.Contains(SaveBuilding.WonderFlag.RequiresEliteShip);
			safeSeaTravel = wonderFlags.Contains(SaveBuilding.WonderFlag.SafeSeaTravel);
			gainAnyTechKnownByTwoCivs = wonderFlags.Contains(SaveBuilding.WonderFlag.GainAnyTechKnownByTwoCivs);
			doubleCombatVsBarbarians = wonderFlags.Contains(SaveBuilding.WonderFlag.DoubleCombatVsBarbarians);
			increasedShipMovement = wonderFlags.Contains(SaveBuilding.WonderFlag.IncreasedShipMovement);
			doublesResearchOutput = wonderFlags.Contains(SaveBuilding.WonderFlag.DoublesResearchOutput);
			increasedTrade = wonderFlags.Contains(SaveBuilding.WonderFlag.IncreasedTrade);
			cheaperUpgrades = wonderFlags.Contains(SaveBuilding.WonderFlag.CheaperUpgrades);
			paysTradeMaintenance = wonderFlags.Contains(SaveBuilding.WonderFlag.PaysTradeMaintenance);
			allowsNuclearWeapons = wonderFlags.Contains(SaveBuilding.WonderFlag.AllowsNuclearWeapons);
			doubleCityGrowth = wonderFlags.Contains(SaveBuilding.WonderFlag.DoubleCityGrowth);
			twoFreeAdvances = wonderFlags.Contains(SaveBuilding.WonderFlag.TwoFreeAdvances);
			reducedWarWeariness = wonderFlags.Contains(SaveBuilding.WonderFlag.ReducedWarWeariness);
			doubleCityDefenses = wonderFlags.Contains(SaveBuilding.WonderFlag.DoubleCityDefenses);
			allowDiplomaticVictory = wonderFlags.Contains(SaveBuilding.WonderFlag.AllowDiplomaticVictory);
			plusTwoShipMovement = wonderFlags.Contains(SaveBuilding.WonderFlag.PlusTwoShipMovement);
			increasedArmyValue = wonderFlags.Contains(SaveBuilding.WonderFlag.IncreasedArmyValue);
			touristAttraction = wonderFlags.Contains(SaveBuilding.WonderFlag.TouristAttraction);

			if (building.greatWonderProperties != null) {
				greatWonderProperties = new();
			}

			LoadLuaFunctions(gameData);
		}

		[LuaMethod]
		public bool IsGreatWonder() {
			return this.greatWonderProperties != null;
		}

		public bool CanProduce(City city, HashSet<Resource> accessibleResources) {
			if (!city.owner.HasRequiredTechnology(this)) {
				return false;
			}

			if (renderedObsoleteBy != null && city.owner.knownTechs.Contains(renderedObsoleteBy.id)) {
				return false;
			}

			if (city.GetBuildings().Exists(cityBuilding => cityBuilding.building == this)) {
				return false;
			}

			if (greatWonderProperties != null) {
				// We can't build a great wonder if it was already built.
				if (EngineStorage.gameData.GreatWondersBuilt.Contains(name)) {
					return false;
				}

				// We can't build a great wonder if another one of our cities is
				// building it. This city building it is fine, so that it stays a
				// valid choice while it is in production.
				if (city.owner.IsProducing(this, excludedCity: city)) {
					return false;
				}
			}

			if (isSmallWonder || spaceshipPart != null) {
				// We can only have one of each small wonder or spaceship part,
				// and we can't build one if another one of our cities is building
				// it. This city building it is fine, so that it stays a valid
				// choice while it is in production.
				if (city.owner.OwnsBuilding(this) || city.owner.IsProducing(this, excludedCity: city)) {
					return false;
				}
			}

			// Spaceship parts can only be built once we have built the small
			// wonder that allows building them.
			if (spaceshipPart != null && !city.owner.OwnsBuilding(b => b.buildSpaceshipParts)) {
				return false;
			}

			if (goodsMustBeInCityRadius) {
				// GetWorkableTiles excludes the city tile itself, but a resource under the city counts.
				HashSet<Resource> resourcesInRadius = city.GetWorkableTiles()
					.Append(city.location)
					.Select(t => t.Resource)
					.Where(r => r != Resource.NONE)
					.ToHashSet();
				if (!requiredResources.All(resourcesInRadius.Contains)) {
					return false;
				}
			}

			// TODO: This is fragile. MapUnit has no "is an army" flag, so we count
			// the player's land units that can transport as armies. Update this
			// when MapUnit has a more robust interface for armies.
			if (numberOfArmiesRequired > 0 &&
				city.owner.units.Count(u => u.CanTransport() && u.IsLandUnit()) < numberOfArmiesRequired) {
				return false;
			}

			// TODO: Victorious armies and elite ships are not yet tracked on the
			// player, so we refuse to build these. Update this logic when they are.
			if (requiresEliteShip || requiresVictoriousArmy) {
				return false;
			}

			if (isCenterOfEmpire && city.IsCapital()) {
				return false;
			}

			if (requiredBuilding != null) {
				if (numberOfRequiredBuildings > 1) {
					// The required buildings can be anywhere in the empire.
					if (city.owner.CountBuilding(requiredBuilding) < numberOfRequiredBuildings) {
						return false;
					}
				} else if (!city.GetBuildings().Exists(cityBuilding => cityBuilding.building == requiredBuilding)) {
					return false;
				}
			}

			if (requiredGovernment != null && city.owner.government.id != requiredGovernment.id) {
				return false;
			}

			if (!requiredResources.All(accessibleResources.Contains)) {
				return false;
			}

			if (!productionPrerequisites.All(func => func(city))) {
				return false;
			}

			return true;
		}

		public int ShieldCost(Player player, float costFactor) {

			float costAdj = 1.0f;

			// Adjust cost if civilization has building trait exactly once
			foreach (Civilization.Trait trait in dataSource.traits) {
				if (player.civilization.traits.Contains(trait)) {
					costAdj *= EngineStorage.gameData.rules.BuildingDiscountForCivTraits;
					break;
				}
			}

			// Special case building cost formula for center-of-empire buildings
			// Hardcoded to match Civ 3 Conquests values
			// TODO: Make configurable and expose via Lua instead of hardcoding values
			if (this.isCenterOfEmpire) {
				int centerOfEmpireFactor = 6 * player.cities.Count / EngineStorage.gameData.map.optimalNumberOfCities;
				centerOfEmpireFactor = Math.Clamp(centerOfEmpireFactor, 3, 10);
				costAdj *= centerOfEmpireFactor;
			}

			// Round final cost in case of floating point drift causing truncation
			return (int)Math.Round(shieldCost * costFactor * costAdj);
		}

		// Whether the owner knows the tech that makes this building obsolete.
		// Obsolete buildings no longer provide their effects.
		public bool IsObsolete(Player owner) {
			return renderedObsoleteBy != null && owner.knownTechs.Contains(renderedObsoleteBy.id);
		}

		public bool isGreatWonderObsolete(Player owner) {
			return greatWonderProperties != null && IsObsolete(owner);
		}

		public SaveBuilding ToSaveBuilding() {
			return dataSource;
		}

		public override string ToString() {
			return name;
		}

		private void LoadLuaFunctions(GameData gameData) {
			BehaviorEngine luaEngine = gameData.luaBehaviorEngine;

			foreach (var path in dataSource.productionPrerequisites) {
				var rule = luaEngine.ImportFunc<Func<City, bool>>(path);
				productionPrerequisites.Add(rule);
			}

			foreach (var path in dataSource.onFinishedUnitProduction) {
				var handler = luaEngine.ImportFunc<Action<MapUnit>>(path);
				onFinishedUnitProduction += handler;
			}

			foreach (var path in dataSource.tileModifiers) {
				var modifier = luaEngine.ImportFunc<Action<Tile.Yield>>(path);
				tileModifier += modifier;
			}
		}
	}
}
