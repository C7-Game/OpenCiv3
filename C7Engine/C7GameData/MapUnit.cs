using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using C7Engine;
using Serilog;
using static C7GameData.PlayerRelationship;

namespace C7GameData {
	/**
	 * A unit on the map.  Not to be confused with a unit prototype.
	 **/
	public partial class MapUnit {
		private static ILogger log = Log.ForContext<MapUnit>();
		public ID id { get; internal set; }
		public string name { get; internal set; }
		public Civilization nationality { get; set; }
		public UnitPrototype unitType { get; set; }
		public Player owner { get; set; }
		public Tile previousLocation { get; internal set; }
		private Tile currentLocation;

		public Tile location {
			get => currentLocation;
			set {
				previousLocation = location;
				currentLocation = value;
			}
		}
		public TilePath path { get; set; }

		public string experienceLevelKey;
		[JsonIgnore]
		public ExperienceLevel experienceLevel { get; set; }

		public MovementPoints movementPoints = new MovementPoints();
		public int hitPointsRemaining { get; set; }
		public int maxHitPoints {
			get {
				return this.experienceLevel.baseHitPoints + this.unitType.hpBonus;
			}
		}
		public bool isFortified { get; set; }

		public bool isAutomated { get; set; }

		//sentry, etc. will come later.  For now, let's just have a couple things so we can cycle through units that aren't fortified.
		public int defensiveBombardsRemaining;

		public TileDirection facingDirection = TileDirection.SOUTHEAST;

		public float WorkerProgressTowardsJob { get; set; }
		public Terraform WorkerJob { get; set; }

		public ID loadedOnUnitId { get; set; }

		public UnitAI currentAI;

		public MapUnit(ID id) {
			this.id = id;
		}

		internal MapUnit() { }

		public static MapUnit NONE = new MapUnit(ID.None("unit"));

		public static bool IsMapUnitValid(MapUnit mapUnit) {
			return mapUnit != null && mapUnit != NONE;
		}

		public bool IsBusy() {
			return isFortified || (path != null && path.PathLength() > 0) || WorkerJob != null || isAutomated;
		}

		public bool IsLandUnit() {
			return this.unitType.categories.Contains("Land");
		}
		public bool IsWaterUnit() {
			return this.unitType.categories.Contains("Sea");
		}
		public bool IsAirUnit() {
			return this.unitType.categories.Contains("Air");
		}

		public bool CanDefendOnLand() {
			return IsLandUnit() && unitType.defense > 0;
		}

		public bool IsCombatUnit() {
			return this.unitType.attack > 0 || this.unitType.defense > 0;
		}

		public bool CanBeActive() {
			return !this.IsBusy() && this.movementPoints.canMove;
		}

		public bool IsCaptive() {
			return !string.Equals(this.nationality.name, this.owner.civilization.name, StringComparison.CurrentCultureIgnoreCase);
		}

		public bool CanTransport() {
			return this.unitType.capacity > 0 && this.unitType.actions.Contains(UnitAction.Unload);
		}

		public bool IsLoadable() {
			return this.unitType.actions.Contains(UnitAction.Load);
		}

		public bool IsLoaded() {
			return this.loadedOnUnitId != null;
		}

		public bool IsLoadedIn(MapUnit transport) {
			return transport.id == this.loadedOnUnitId;
		}

		public override string ToString() {
			if (this != NONE) {
				return $"{this.owner} {this.GetDisplayName()} at [{this.location.XCoordinate}, {this.location.YCoordinate}] " +
					   $"with {this.movementPoints.getMixedNumber()} MP and {this.hitPointsRemaining} HP, id = {id}";
			} else {
				return "This is the NONE unit";
			}
		}

		public string GetDisplayName() {
			return this.IsCaptive() ? $"{this.name} ({this.nationality.name})" : this.name;
		}

		// TODO: best move this to lua at some point
		public string GetArtName() {
			if (this.unitType.art.mainArt.variations != null) {
				if (this.unitType.isWorker && this.IsCaptive()) {
					if (this.unitType.art.mainArt.variations.FirstOrDefault(s => s.Key.EndsWith("SLAVE")).Value != null)
						return this.unitType.art.mainArt.variations.First(s => s.Key.EndsWith("SLAVE")).Value;
				}

				if (this.unitType.art.mainArt.variations.TryGetValue($"{this.owner.eraCivilopediaName}", out var value))
					return value;

				//TODO: add military + science leader variation
			}

			return this.unitType.art.mainArt.defaultName;
		}

		public string Describe() {
			UnitPrototype type = this.unitType;
			string exp = this.IsCombatUnit() ? $"{this.experienceLevel.displayName}" : "";
			string hPDesc = ((type.attack > 0) || (type.defense > 0)) ? $" ({this.hitPointsRemaining}/{this.maxHitPoints})" : "";
			string displayName = this.IsCaptive() ? $" ({this.nationality.adjective}) {this.name}" : $" {this.name}";
			string attackDesc = (type.bombard > 0) ? $"{type.attack}({type.bombard})" : type.attack.ToString();
			string stats = $" ({attackDesc}.{type.defense}.{(EngineStorage.uiControllerID == this.owner.id ? $"{this.movementPoints.getMixedNumber()}/" : "")}{type.movement})";
			return $"{exp}{hPDesc}{displayName}{stats}".Trim();
		}

		// TODO: The contents of this enum are copy-pasted from UnitAction in Civ3UnitSprite.cs. We should unify these so we don't have two different
		// but virtually identical enums.
		public enum AnimatedAction {
			BLANK,
			DEFAULT,
			WALK,
			RUN,
			ATTACK1,
			ATTACK2,
			ATTACK3,
			DEFEND,
			DEATH,
			DEAD,
			FORTIFY,
			FORTIFYHOLD,
			FIDGET,
			VICTORY,
			TURNLEFT,
			TURNRIGHT,
			BUILD,
			ROAD,
			MINE,
			IRRIGATE,
			FORTRESS,
			CAPTURE,
			JUNGLE,
			FOREST,
			PLANT
		}

		public struct Appearance {
			public AnimatedAction action;
			public TileDirection direction;
			public float progress; // Varies 0 to 1
			public float offsetX, offsetY; // Offset is in grid cells from the unit's location
			public AnimationEnding ending;

			// When true, indicates that the animation is still playing (f.e. a unit is still running between tiles) so the UI shouldn't yet
			// autoselect another unit.
			public bool DeservesPlayerAttention() {
				// TODO: Special rules for different animations. We don't need to see workers do their thing but we do want to watch units
				// move. IMO we should also not show units fortifying even though I know the original game does.
				// This may also be the culprit behind why we can fortify a unit that is in motion.
				if (ending == AnimationEnding.Repeat) {
					return false;
				}
				return progress < 1.0;
			}
		}

		private const int JOB_PROGRESS_WORKER = 2;
		private const int JOB_PROGRESS_SLAVE = 1;

		private static int GetWorkerJobCost(Tile tile, Terraform workerJob) {
			// For the movement cost multiplier, see note 7
			// (https://apolyton.net/forum/civilization-series/civilization-iii/59815-civilization-iii-bic-file-format-2nd-thread?p=1362768#post1362768)
			// For example, clearing a forest has a cost of 4, but with a normal
			// worker that would take 2 turns. In order for the job to take the
			// expected 4 turns we need to multiply by the movement cost of the
			// terrain. This also makes roading hills/mountains more expensive.
			return tile.overlayTerrainType.movementCost * workerJob.TurnsToComplete;
		}

		public async Task animateAsync(AnimatedAction action, AnimationEnding ending = AnimationEnding.Stop) {
			var animationsEnabled = EngineStorage.animationsEnabled && !EngineStorage.gameData.observerMode;
			var skipAnimations = SkipAnimations(action);

			if (animationsEnabled && !skipAnimations) {
				var msg = new MsgStartUnitAnimation(this, action, ending);
				msg.send();

				await EngineStorage.WaitForAnimationFinished(msg.animationId);
			}
			if (this.owner.isHuman)
				new MsgUnitMoved(this).send();
		}

		public void animate(AnimatedAction action, AnimationEnding ending = AnimationEnding.Stop) {
			_ = animateAsync(action, ending);
		}

		private bool SkipAnimations(AnimatedAction action) {
			if (action != AnimatedAction.RUN) return false;

			// as soon as we move, the tile we were just on becomes the previous tile
			var isOnRailroad = Tile.IsTileValid(this.previousLocation) && this.previousLocation.HasRailroad();
			if (!isOnRailroad) return false;

			// and the tile we are moving towards, becomes the current tile
			var movingOnRailroad = Tile.IsTileValid(this.location) && this.location.HasRailroad();
			if (!movingOnRailroad) return false;

			var canMoveFreely = Player.CanMoveFreely(this.owner, this.previousLocation, this.location);
			if (!canMoveFreely) return false;

			return true;
		}

		public void ResetFacingDirection() {
			facingDirection = TileDirection.SOUTHEAST;
		}

		public IEnumerable<StrengthBonus> ListStrengthBonusesVersus(MapUnit opponent, CombatRole role, TileDirection? attackDirection) {
			GameData gD = EngineStorage.gameData;

			if (role.Defending()) {
				if (isFortified)
					yield return gD.fortificationBonus;

				yield return location.overlayTerrainType.defenseBonus;

				foreach (StrengthBonus sb in location.overlays.GetDefenseBonuses()) {
					yield return sb;
				}

				if ((!role.Bombarding()) && (attackDirection is TileDirection dir) && location.HasRiverCrossing(dir.Reversed()))
					yield return gD.riverCrossingBonus;

				if (location.cityAtTile != null) {
					foreach (StrengthBonus sb in location.cityAtTile.GetDefenseBonuses()) {
						yield return sb;
					}
				}
			}
		}

		public double StrengthVersus(MapUnit opponent, CombatRole role, TileDirection? attackDirection) {
			return unitType.BaseStrength(role) * StrengthBonus.ListToMultiplier(ListStrengthBonusesVersus(opponent, role, attackDirection));
		}

		public bool CanDefendAgainst(MapUnit attacker) {
			//Basically, unit type must match.  Sea/air units in a city/airfield can't defend against land units.
			//Land units on a boat or planes on a carrier can't defend against boats.  Anti-air is another category that should be checked before the direct combat.
			//Potential future hybrid units that have multiple categories (e.g. amphibious vehicles) may contain more than one category.
			if (attacker.unitType.categories.Contains("Land") && !unitType.categories.Contains("Land")) {
				return false;
			}
			if (attacker.unitType.categories.Contains("Sea") && !unitType.categories.Contains("Sea")) {
				return false;
			}
			if (attacker.unitType.categories.Contains("Air") && !unitType.categories.Contains("Air")) {
				return false;
			}
			return true;
		}

		// Answers the question: if "opponent" is attacking the tile that this unit is standing on, does this unit defend instead of "otherDefender"?
		// Note that otherDefender does not necessarily belong to the same civ as this  Under standard Civ 3 rules you can't have units belonging
		// to two different civs on the same tile, but we don't want to assume that. In that case, whoever is an enemy of "opponent" should get
		// priority. Otherwise it's just whoever is stronger on defense.
		public bool HasPriorityAsDefender(MapUnit otherDefender, MapUnit opponent) {
			Player opponentPlayer = opponent.owner;
			bool weAreEnemy           = !opponentPlayer?.IsAtPeaceWith(owner) ?? false;
			bool otherDefenderIsEnemy = !opponentPlayer?.IsAtPeaceWith(otherDefender.owner) ?? false;

			if (weAreEnemy && !otherDefenderIsEnemy)
				return true;
			if (otherDefenderIsEnemy && !weAreEnemy)
				return false;

			double ourTotalStrength = StrengthVersus(opponent, CombatRole.Defense, null) * hitPointsRemaining;
			double theirTotalStrength = otherDefender.StrengthVersus(opponent, CombatRole.Defense, null) * otherDefender.hitPointsRemaining;
			return ourTotalStrength > theirTotalStrength;
		}


		public void RollToPromote(MapUnit opponent) {
			// Barbarians can't promote.
			if (owner.isBarbarians) {
				return;
			}

			double promotionChance = experienceLevel.promotionChance;
			if (opponent.owner.isBarbarians)
				promotionChance /= 2.0;
			if (owner.civilization.traits.Contains(Civilization.Trait.Militaristic))
				promotionChance *= 2;
			if (GameData.rng.NextDouble() < promotionChance) {
				Promote();
				animate(AnimatedAction.VICTORY);
			}
		}

		public void Promote() {
			ExperienceLevel nextLevel = EngineStorage.gameData.GetExperienceLevelAfter(experienceLevel);
			if (nextLevel != null) {
				experienceLevelKey = nextLevel.key;
				experienceLevel = nextLevel;
				hitPointsRemaining++;
			}
		}

		public double RetreatChance(MapUnit opponent, bool isAttacking) {
			return ((unitType.movement > 1) && (opponent.unitType.movement <= 1)) ? experienceLevel.retreatChance : 0.0;
		}

		internal TileDirection GetAttackAnimationDirection(TileDirection attackDirection) {
			return unitType.rotateBeforeAttack ? attackDirection.RotatedCounterClockwise90Degrees() : attackDirection;
		}

		internal TileDirection GetDefenseAnimationDirection(TileDirection attackDirection) {
			return GetAttackAnimationDirection(attackDirection.Reversed());
		}

		public int HealRateAt(Tile location) {
			GameData gD = EngineStorage.gameData;
			City city = location.cityAtTile;
			bool inFriendlyCity = (city != null) && (city != City.NONE) && owner.IsAtPeaceWith(city.owner);
			if (inFriendlyCity)
				return gD.healRateInCity;
			if (unitType.categories.Contains("Sea"))
				return 0;
			return gD.healRateInNeutralField;
			// TODO: Consider friendly/neutral/enemy territory once that's implemented, barracks, the Red Cross
		}

		public enum Intent {
			Disabled,         // do nothing, don't move
			MoveFreely,       // enter tile
			Fight,            // move and fight an enemy unit, capture city/unit, pillage freely, etc
			Load,             // load a unit on a friendly transport on this tile
			Unload,           // unload a unit from a transport on this tile
			NoticeUnit,       // a non-combat unit tries to move on another owner's unit tile
			NoticeCity,       // a non-combat unit tries to move on another owner's city tile
			NoticeAlliance,   // a combat unit tries to move on an ally's city/unit tile
			WarDeclaration,   // a combat unit tries to move on another owner's city/unit tile (TODO: pillage non-enemy road/colony etc for example)
		}

		/// <summary>
		/// Returns a unit's intent trying to move in on a tile.
		/// </summary>
		/// <param name="tile"></param>
		/// <returns></returns>
		// private Intent ResolveIntent(Tile tile, bool tset = false) {
		private Intent ResolveIntent(Tile tile) {
			if (!Tile.IsTileValid(tile))
				return Intent.Disabled;

			// Impassable terrain (e.g. some mods' deserts) cannot be entered
			// by any unit, no matter what.
			if (tile.IsImpassable())
				return Intent.Disabled;

			var unitOwner = this.owner;

			// TODO: Perhaps this is not sufficient, but it is for now,
			// since otherwise we can move air units on land and sea
			if (this.IsAirUnit())
				return Intent.Disabled;

			if (unitOwner.isHuman && !unitOwner.HasExploredTile(tile))
				return Intent.MoveFreely;

			var hasOwnCity = HasOwnCity(tile, unitOwner);

			// Keep land units on land and sea units on water
			if (this.IsWaterUnit() && tile.IsLand()) {
				if (hasOwnCity) return Intent.MoveFreely;

				return Intent.Disabled;
			}

			if (this.CanBoardTransportOnTile(tile))
				return Intent.Load;

			if (this.CanUnloadToTile(tile))
				return Intent.Unload;

			if (this.IsLandUnit() && !tile.IsLand())
				return Intent.Disabled;

			var hasForeignCity = HasForeignCity(tile, unitOwner);
			var isHumanOwner = this.owner.isHuman;
			var isActiveTile = this.owner.tileKnowledge.isActiveTile(tile);

			if (isHumanOwner && !isActiveTile && hasForeignCity) {
				return Intent.Disabled;
			}

			if (isHumanOwner && !isActiveTile) {
				return Intent.MoveFreely;
			}

			var hasHostileUnits = HasHostileUnits(tile, unitOwner);
			var hasForeignUnits = HasForeignUnits(tile, unitOwner);
			var foreignOwner = ForeignOwner(tile);
			var hasHostileCity = HasHostileCity(tile, unitOwner);
			var cityOwner = tile.cityAtTile?.owner;
			var hasBarbCamp = tile.hasBarbarianCamp;
			var isCombatUnit = this.IsCombatUnit();
			var distanceToTile = this.location.DistanceTo(tile);

			if (isHumanOwner && (hasForeignCity || hasHostileCity || hasForeignUnits || hasHostileUnits) && distanceToTile > 1) {
				return Intent.Disabled;
			}

			if (!isCombatUnit) {
				if (hasForeignUnits || hasHostileUnits) {
					return Intent.NoticeUnit;
				}
				if (hasForeignCity || hasHostileCity || hasBarbCamp) {
					return Intent.NoticeCity;
				}
			}

			// TODO: add check for when we want to pillage something

			if (isCombatUnit) {
				if (hasHostileUnits || hasHostileCity) {
					return Intent.Fight;
				}
				if (hasForeignUnits) {
					if (distanceToTile == 1) {
						if (EngineStorage.gameData.AreInLockedPeace(unitOwner, foreignOwner)) {
							return Intent.NoticeAlliance;
						}
						return Intent.WarDeclaration;
					}

					if (isActiveTile) {
						return Intent.Disabled;
					}
				}
				if (hasForeignCity) {
					if (distanceToTile == 1) {
						if (EngineStorage.gameData.AreInLockedPeace(unitOwner, cityOwner)) {
							return Intent.NoticeAlliance;
						}
						return Intent.WarDeclaration;
					}
					return Intent.Disabled;
				}
			}

			return Intent.MoveFreely;
		}

		public bool CanEnterPeacefully(Tile tile) {
			return CanEnterPeacefully(tile, out _);
		}
		public bool CanEnterPeacefully(Tile tile, out Intent intent) {
			intent = this.ResolveIntent(tile);
			return intent == Intent.MoveFreely || intent == Intent.Load || intent == Intent.Unload;
		}

		public bool CanEnter(Tile tile) {
			return CanEnter(tile, out _);
		}
		public bool CanEnter(Tile tile, out Intent intent) {
			var canEnterPeacefully = CanEnterPeacefully(tile, out var it);
			intent = it;
			return canEnterPeacefully || it == Intent.Fight;
		}

		public bool CanEnterForcefully(Tile tile) {
			return CanEnterForcefully(tile, out _);
		}
		public bool CanEnterForcefully(Tile tile, out Intent intent) {
			var canEnter = CanEnter(tile, out var it);
			intent = it;
			return canEnter || it == Intent.WarDeclaration;
		}

		private bool CanBoardTransportOnTile(Tile tile) {
			if (!IsLoadable())
				return false;

			var availableTransports = tile.unitsOnTile.Where(u => u.CanTransport());
			foreach (var transport in availableTransports) {
				if (transport.CanLoad(this))
					return true;
			}

			return false;
		}

		private bool CanUnboardTransportToTile(Tile tile) {
			return IsLoadable() && IsLoaded() && tile.IsLand();
		}

		private MapUnit SelectTransportToBoard(Tile tile) {
			// TODO: Let human player choose via UI which transport to load unit in

			var availableTransports = tile.unitsOnTile
				.Where(u => u.CanTransport())
				.Where(u => !u.IsFull());

			// Sort candidates by free capacity, but prefer transports that already have units
			availableTransports = availableTransports
				.OrderBy(t => !t.IsEmpty())
				.ThenByDescending(t => t.FreeCapacity());

			foreach (var transport in availableTransports) {
				if (transport.CanLoad(this))
					return transport;
			}

			return null;
		}

		private MapUnit FindTransportToUnboard(Tile tile, ID transport) {
			return tile.unitsOnTile.FirstOrDefault(t => t.id == transport);
		}

		private bool CanLoad(MapUnit mapUnit) {
			if (owner != mapUnit.owner)
				return false;

			if (!mapUnit.IsLoadable())
				return false;

			var hasRoom = !IsFull();

			// TODO: type restrictions: only subs can carry nukes, carriers take aircraft, etc.
			var suitableUnit = mapUnit.IsLandUnit();  // only land units in transports for now
			return hasRoom && suitableUnit;
		}

		// TODO: Transport chaining
		// TODO: Amphibious assault

		private bool CanUnloadToTile(Tile tile) {
			if (!CanTransport())
				return false;

			var isValidLanding = tile.IsLand();
			return !IsEmpty() && isValidLanding;
		}

		public int FreeCapacity() {
			var loaded = this.location.unitsOnTile.Where(u => u.IsLoadedIn(this)).ToList();
			return this.unitType.capacity - loaded.Count;
		}

		private bool IsEmpty() => unitType.capacity > 0 && FreeCapacity() == unitType.capacity;
		private bool IsFull() => unitType.capacity > 0 && FreeCapacity() == 0;

		private static bool HasHostileUnits(Tile tile, Player player) {
			foreach (MapUnit other in tile.unitsOnTile) {
				if (player != other.owner && AtWar(player, other.owner))
					return true;
			}
			return false;
		}
		private static bool HasForeignUnits(Tile tile, Player player) {
			foreach (MapUnit other in tile.unitsOnTile) {
				if (player != other.owner)
					return true;
			}
			return false;
		}

		private static Player ForeignOwner(Tile tile) {
			return tile.unitsOnTile.FirstOrDefault()?.owner;
		}

		private static bool HasHostileCity(Tile tile, Player player) {
			return tile.HasCity() && AtWar(player, tile.cityAtTile.owner);
		}
		private static bool HasForeignCity(Tile tile, Player player) {
			return tile.HasCity() && tile.cityAtTile.owner != player;
		}
		private static bool HasOwnCity(Tile tile, Player player) {
			return tile.HasCity() && tile.cityAtTile.owner == player;
		}

		private float SumWorkerProgress(Tile tile, Terraform workerJob) {
			float result = 0;
			foreach (MapUnit unit in tile.unitsOnTile) {
				if (unit.WorkerJob == workerJob) {
					result += unit.WorkerProgressTowardsJob;
				}
			}
			return result;
		}

		public int TurnsToCompleteTerraform(Terraform t) {
			// Figure out how much work remains to do on this particular job.
			int remainingTerraformCost = GetWorkerJobCost(location, t) - (int)this.SumWorkerProgress(location, t);

			// Figure out how fast all of the wokers doing this particular
			// terraform will work.
			float combinedWorkerSpeed = this.workerSpeed();
			foreach (MapUnit unit in location.unitsOnTile.Where(u => u.id != this.id)) {
				if (unit.WorkerJob == t) {
					combinedWorkerSpeed += unit.workerSpeed();
				}
			}

			// Divide the two, rounding up.
			return (int)Math.Ceiling(remainingTerraformCost / combinedWorkerSpeed);
		}

		public bool canBuildCity() {
			if (!unitType.actions.Contains(UnitAction.BuildCity)) {
				return false;
			}
			if (location.HasCity() || !location.IsAllowCities()) {
				return false;
			}
			return location.neighbors.Values.All(tile => !tile.HasCity());
		}

		public bool CanPerformTerraformAction(Terraform terraform) {
			return CanPerformTerraformAction(terraform, location);
		}

		public bool CanPerformTerraformAction(Terraform terraform, Tile tile) {
			var containsTerraform = unitType.terraformActions.Contains(terraform);
			var meetsRequirements = terraform.MeetsRequirements(owner, tile);
			var hasCity = tile.HasCity();
			return containsTerraform && meetsRequirements && !hasCity;
		}

		public float workerSpeed() {
			float progressPerTurn = this.IsCaptive() ? JOB_PROGRESS_SLAVE : JOB_PROGRESS_WORKER;
			if (owner.civilization.traits.Contains(Civilization.Trait.Industrious)) {
				progressPerTurn *= 1.5f;
			}
			return progressPerTurn;
		}

		public void resetWorkerJob() {
			WorkerJob = null;
			WorkerProgressTowardsJob = 0;
			animate(AnimatedAction.BLANK, AnimationEnding.Repeat);
		}

		public bool canAutomate() {
			return unitType.actions.Contains(UnitAction.Automate);
		}

		public bool canExplore() {
			return unitType.actions.Contains(UnitAction.Explore);
		}

		public List<Terraform> GetAvailableTerraforms() {
			return EngineStorage.gameData.Terraforms.Where(CanPerformTerraformAction).ToList();
		}

		/**
		 * Helper function to get the available actions for a unit
		 * based on what terrain it is on.
		 **/
		public List<UnitAction> GetAvailableActions() {
			List<UnitAction> result = new();

			// Eventually, we should look this up somewhere to see what all actions we have (and mods might add more)
			// For now, this is still an improvement over the last iteration.
			UnitAction[] implementedActions = { UnitAction.Hold, UnitAction.Wait, UnitAction.Fortify, UnitAction.Disband, UnitAction.Goto, UnitAction.Bombard };
			foreach (UnitAction action in implementedActions) {
				if (unitType.actions.Contains(action)) {
					result.Add(action);
				}
			}

			if (canBuildCity()) {
				result.Add(UnitAction.BuildCity);
			}
			if (canExplore()) {
				result.Add(UnitAction.Explore);
			}
			if (canAutomate()) {
				result.Add(UnitAction.Automate);
			}

			if (CanBoardTransportOnTile(this.location) && this.loadedOnUnitId == null) {
				result.Add(UnitAction.Load);
			}
			if (CanUnloadToTile(this.location) && this.location.HasCity()) {
				result.Add(UnitAction.Unload);
			}

			// Eventually we will have advanced actions too, whose availability will rely on their base actions' availability.
			// unit.availableActions.Add("rename");

			return result;
		}
	}
}
