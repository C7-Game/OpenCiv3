using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using Serilog;

namespace C7GameData {
	using System;
	using System.Linq;
	using C7Engine;
	using Save;

	public enum UnitAction {
		BuildCity,
		Bombard,
		Hold,
		Wait,
		Fortify,
		Disband,
		Goto,
		Explore,
		Automate,
		Load,
		Unload
	}

	public struct ItemContext(UnitPrototype proto, Player player) {
		public UnitPrototype proto = proto;
		public Player player = player;
	}

	// A container for all the art for this unit
	public struct Art {
		public MainArt mainArt;
		public ThumbNailArt thumbnailArt;
		public PediaArt pediaArt;
	}

	// the main art contains the Animation art folder path for the unit
	public struct MainArt {
		public string defaultName;
		public Dictionary<string, string> variations;
	}
	// the thumbnail art contains the index to the small unit icons used in city screen production, etc
	public struct ThumbNailArt {
		public int defaultIndex;
		public Dictionary<string, int> variations;
	}
	// the icons being used by the civilopedia and also other places like the Science Advisor
	public struct PediaArt {
		public string small;
		public string large;
	}

	/**
	 * The prototype for a unit, which defines the characteristics of a unit.
	 * For example, a Spearman might have 1 attack, 2 defense, and 1 movement.
	 **/
	public class UnitPrototype : IProducible {
		public string name { get; set; }
		public string civilopediaEntry { get; set; }
		public Art art { get; set; }
		public int shieldCost { get; set; }
		public int populationCost { get; set; }
		public Tech requiredTech { get; set; }
		public int attack { get; set; }
		public int defense { get; set; }
		public int bombard { get; set; }
		public int bombardRange { get; set; }
		public int rateOfFire { get; set; }
		public int movement { get; set; }
		public int capacity { get; set; }
		public int hpBonus { get; set; }
		public HashSet<Civilization> producibleBy { get; set; } = [];
		public List<UnitPrototype> upgradesTo = [];
		public bool unproducible;
		public HashSet<SaveUnitPrototype.Flag> flags = [];
		public bool rotateBeforeAttack {
			get => flags.Contains(SaveUnitPrototype.Flag.RotateBeforeAttack);
			set {
				if (value) {
					flags.Add(SaveUnitPrototype.Flag.RotateBeforeAttack);
				} else {
					flags.Remove(SaveUnitPrototype.Flag.RotateBeforeAttack);
				}
			}
		}

		public bool isLandBombardmentLethal {
			get => flags.Contains(SaveUnitPrototype.Flag.LethalLandBombardment);
			set {
				if (value) {
					flags.Add(SaveUnitPrototype.Flag.LethalLandBombardment);
				} else {
					flags.Remove(SaveUnitPrototype.Flag.LethalLandBombardment);
				}
			}
		}
		public bool isSeaBombardmentLethal {
			get => flags.Contains(SaveUnitPrototype.Flag.LethalSeaBombardment);
			set {
				if (value) {
					flags.Add(SaveUnitPrototype.Flag.LethalSeaBombardment);
				} else {
					flags.Remove(SaveUnitPrototype.Flag.LethalSeaBombardment);
				}
			}
		}
		public bool hasRadar {
			get => flags.Contains(SaveUnitPrototype.Flag.Radar);
			set {
				if (value) {
					flags.Add(SaveUnitPrototype.Flag.Radar);
				} else {
					flags.Remove(SaveUnitPrototype.Flag.Radar);
				}
			}
		}

		public HashSet<string> categories = new HashSet<string>();

		public HashSet<UnitAction> actions = [];

		public HashSet<string> attributes = new HashSet<string>();

		public HashSet<Resource> requiredResources { get; set; } = [];

		public HashSet<Terraform> terraformActions = [];

		// terraformActions.Count > 0 is not enough, as for example the Crusader unit can build a Fortress
		public bool isWorker => terraformActions.Count > 0 && actions.Contains(UnitAction.Automate);
		public bool isSettler => actions.Contains(UnitAction.BuildCity);


		public UnitPrototype() { }

		public UnitPrototype(SaveUnitPrototype proto, IEnumerable<Terraform> terraforms) {
			(name, art, shieldCost, populationCost)
				= (proto.name, proto.art, proto.shieldCost, proto.populationCost);

			civilopediaEntry = proto.civilopediaEntry;

			(attack, defense, bombard, bombardRange, rateOfFire)
				= (proto.attack, proto.defense, proto.bombard, proto.bombardRange, proto.rateOfFire);

			(movement, capacity, hpBonus, unproducible) =
				(proto.movement, proto.capacity, proto.hpBonus, proto.unproducible);

			categories = new HashSet<string>(proto.categories);
			actions = proto.actions;
			attributes = new HashSet<string>(proto.attributes);
			flags = new HashSet<SaveUnitPrototype.Flag>(proto.flags);

			terraformActions = proto.terraformActions.Select(id => terraforms.First(t => t.Id == id)).ToHashSet();
		}

		public int ShieldCost(HashSet<Civilization.Trait> civTraits, float costFactor) {
			return (int)(shieldCost * costFactor);
		}

		public bool IsLandUnit() {
			return categories.Contains("Land");
		}

		public bool IsSeaUnit() {
			return categories.Contains("Sea");
		}

		public override string ToString() {
			return $"{name} ({attack}/{defense}/{movement})";
		}

		public double BaseStrength(CombatRole role) {
			switch (role) {
				case CombatRole.Attack: return attack;
				case CombatRole.Defense: return defense;
				case CombatRole.Bombard: return bombard;
				case CombatRole.BombardDefense: return defense;
				case CombatRole.DefensiveBombard: return bombard;
				case CombatRole.DefensiveBombardDefense: return defense;
				default: throw new ArgumentOutOfRangeException("Invalid CombatRole");
			}
		}

		public MapUnit GetInstance(ID id, UnitPrototype proto, Player owner, Civilization nationality = null, Tile location = null, TileDirection facingDirection = TileDirection.SOUTHWEST, int hitPoints = 3) {
			MapUnit instance = new MapUnit(id);
			instance.unitType = proto;
			instance.name = proto.name;
			instance.hitPointsRemaining = hitPoints;    //todo: make this configurable
			instance.owner = owner;
			if (nationality != null)
				instance.nationality = nationality;
			else
				instance.nationality = owner.civilization;
			instance.location = location;

			instance.movementPoints.reset(movement);
			return instance;
		}

		/// Immediate upgrade target, given a civilization.
		private UnitPrototype GetUnitUpgrade(Civilization civ) {
			var match = upgradesTo.Where(x => x.producibleBy.Contains(civ)).ToList();
			if (match.Count > 1)
				Log.Warning($"Unexpected upgrade chain: more than one valid target for upgrading {name} with {civ.name}.");
			return match.FirstOrDefault();
		}

		/// The upgrade chain: a unit upgrade series as an ordered collection of unit prototypes,
		/// for a particular civilization, starting from this unit.
		///
		/// Note: must be unique and stable: in-game every unit has at most one direct upgrade target.
		private List<UnitPrototype> GetUpgradeChain(Civilization civ) {
			var chain = new List<UnitPrototype>();
			var current = this.GetUnitUpgrade(civ);
			while (current != null) {
				chain.Add(current);
				current = current.GetUnitUpgrade(civ);
			}
			return chain;
		}

		/// Whether a given city can produce this unit, given available resources.
		public bool CanProduce(City city, HashSet<Resource> accessibleResources) {
			var civ = city.owner.civilization;
			return this.IsAvailableTo(civ)
				   && this.MeetsProductionRequirements(city, accessibleResources)
				   && !this.IsUnitObsolete(city, accessibleResources);
		}

		/// Whether a Civ could build this unit, if it had a suitable city and necessary resources.
		private bool IsAvailableTo(Civilization civ) {
			return !this.unproducible && this.producibleBy.Contains(civ);
		}

		/// Whether this unit can be built in this city (by the owner), given this particular set of resources.
		private bool MeetsProductionRequirements(City city, HashSet<Resource> accessibleResources) {
			if (!city.owner.HasRequiredTechnology(this)) {
				return false;
			}

			if (this.IsSeaUnit() && !city.location.NeighborsWater()) {
				return false;
			}

			if (!this.requiredResources.All(accessibleResources.Contains)) {
				return false;
			}

			return true;
		}

		/// A unit is obsolete if a unit in its upgrade chain can be produced (in this city
		/// with the given resources).
		///
		/// "AllowLesserUnitProduction" removes unit obsolescence.
		private bool IsUnitObsolete(City city, HashSet<Resource> accessibleResources) {
			// TODO: Consider golden ages when determining whether a unit is obsolete.
			// If a golden age has not yet been triggered and a unit can trigger one,
			// it shouldn't be marked as obsolete, even if its upgrade is available.

			if (EngineStorage.gameData.rules.AllowLesserUnitProduction) return false;

			if (this.GetProducibleUpgrade(city, accessibleResources) == null) return false;

			return true;
		}

		/// The "true" upgrade for any given in-game unit. The unique unit prototype available to
		/// this unit for this civ, in this city, with the given resources.
		public UnitPrototype GetProducibleUpgrade(City city, HashSet<Resource> accessibleResources) {
			var civ = city.owner.civilization;

			var unitUpgradeChain = this.GetUpgradeChain(civ);
			if (!unitUpgradeChain.Any())
				return null;

			// We expand the upgrade chain with "siblings", units that join the chain from nearby "branches"
			var potentialUnits = this.GetUnitsThatUpgradeTo(this.upgradesTo);
			var units = unitUpgradeChain.Union(potentialUnits);

			// Filter down to units we can produce
			var producibleUnits = units.Where(uu =>
				uu.MeetsProductionRequirements(city, accessibleResources)
				&& uu.producibleBy.Contains(civ)
			).ToList();

			// Select the best unit we can upgrade to. Say we are upgrading a Warrior: if Medieval Infantry
			// is available, we don't want to upgrade to a mere Swordsman.
			var unitUpgrade = SortInUpgradeOrder(producibleUnits).LastOrDefault();

			return unitUpgrade;
		}

		// For example, if we want to check if the Trebuchet is obsolete for the Koreans,
		// what we can do, because we don't want to hardcode anywhere that
		// the Hwacha is the "replacement" to the Cannon (which is Trebuchet's upgrade)
		// we can check if the upgrade (Artillery) of the upgrade (Cannon) of our unit (Trebuchet)
		// has other units that upgrade to it and are available to the Koreans.
		// This is how we get that, since we can build a Hwacha,
		// which upgrades to Artillery, that the Trebuchet is obsolete.
		private List<UnitPrototype> GetUnitsThatUpgradeTo(ICollection<UnitPrototype> units) {
			var unitProtos = EngineStorage.gameData.unitPrototypes;

			HashSet<UnitPrototype> upgradeUpgrades = (units ?? [])
				.SelectMany(x => x.upgradesTo ?? [])
				.ToHashSet();

			List<UnitPrototype> allUnits = unitProtos.Where(p
					=> (p.upgradesTo ?? []).Intersect(upgradeUpgrades).Any())
				.Except([this])
				.ToList();

			return allUnits;
		}

		/// Sort by the upgrade relation: if a upgrades to b, a is "smaller than" b
		private List<UnitPrototype> SortInUpgradeOrder(IEnumerable<UnitPrototype> units) {
			var sorted = units.ToList();
			sorted.Sort((a, b) => {
				if (a.upgradesTo.Contains(b)) return -1;
				if (b.upgradesTo.Contains(a)) return 1;
				return 0;
			});
			return sorted;
		}
	}
}
