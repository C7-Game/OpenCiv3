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
			public Building buildingGainedInEveryCity;
			public Building buildingGainedInEveryCityOnContinent;
		}

		public string name { get; set; }
		public int shieldCost { get; set; }
		public int populationCost { get; set; } // Will always be equal to 0 in the Civ3 rule set

		// Filled in in SaveGame::ConvertBuildings
		public Tech requiredTech { get; set; }
		public Tech? renderedObsoleteBy;

		public Building requiredBuilding;

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
		public bool goodsMustBeInCityRadius;
		public StrengthBonus? combatDefenseBonus;
		public bool providesVeteranGroundUnits;

		// Wonder flags, shared by small and great wonders.
		// TODO: Wire up to actual in-game logic and bonuses.
		// These are just inert flags right now except for isForbiddenPalace and treasuryEarnsInterest
		public bool increasesLeaderChance;
		public bool allowsBuildArmy;
		public bool allowsLargerArmies;
		public bool treasuryEarnsInterest;
		public bool buildSpaceshipParts;
		public bool isForbiddenPalace;
		public bool decreasesMissileSuccess;
		public bool allowsSpyMissions;
		public bool allowsEnemyTerritoryHealing;
		public bool requiresVictoriousArmy;
		public bool requiresEliteShip;
		public bool safeSeaTravel;
		public bool gainAnyTechKnownByTwoCivs;
		public bool doubleCombatVsBarbarians;
		public bool increasedShipMovement;
		public bool doublesResearchOutput;
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
		// city of the owning player.
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

			isCenterOfEmpire = building.flags.Contains(SaveBuilding.Flag.IsCenterOfEmpire);
			increasesLuxuryTrade = building.flags.Contains(SaveBuilding.Flag.IncreasesLuxuryTrade);
			reducesCorruption = building.flags.Contains(SaveBuilding.Flag.ReducesCorruption);
			allowsCitySize2 = building.flags.Contains(SaveBuilding.Flag.AllowsCitySize2);
			allowsCitySize3 = building.flags.Contains(SaveBuilding.Flag.AllowsCitySize3);
			doublesCityGrowthRate = building.flags.Contains(SaveBuilding.Flag.DoublesCityGrowthRate);
			providesWalls = building.flags.Contains(SaveBuilding.Flag.ProvidesWalls);
			onlyUsefulInTowns = building.flags.Contains(SaveBuilding.Flag.CanOnlyBeBuiltInTowns);
			goodsMustBeInCityRadius = building.flags.Contains(SaveBuilding.Flag.GoodsMustBeInCityRadius);
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
				// building it.
				if (city.owner.IsProducing(this)) {
					return false;
				}
			}

			if (isSmallWonder) {
				// We can only have one of each small wonder, and we can't build
				// one if another one of our cities is building it.
				if (city.owner.OwnsBuilding(this) || city.owner.IsProducing(this)) {
					return false;
				}
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

			// TODO: Armies and elite ship checks are not yet implemented in player class
			// When they are, update this logic
			if (requiresEliteShip || requiresVictoriousArmy) {
				return false;
			}

			if (isCenterOfEmpire && city.IsCapital()) {
				return false;
			}

			if (requiredBuilding != null &&
				!city.GetBuildings().Exists(cityBuilding => cityBuilding.building == requiredBuilding)) {
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

		public int ShieldCost(HashSet<Civilization.Trait> civTraits, float costFactor) {
			foreach (Civilization.Trait trait in dataSource.traits) {
				if (civTraits.Contains(trait)) {
					return (int)(shieldCost * EngineStorage.gameData.rules.BuildingDiscountForCivTraits * costFactor);
				}
			}
			return (int)(shieldCost * costFactor);
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
