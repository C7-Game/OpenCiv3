using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using static C7GameData.City;
using static C7GameData.TerrainImprovement;
using static C7GameData.Tile.TileOverlays;

namespace C7GameData {
	public partial class Tile {
		public ID Id { get; internal set; }
		public Civ3ExtraInfo ExtraInfo;
		public int XCoordinate;
		public int YCoordinate;

		// Needed for coordinate wrapping.
		public GameMap map;

		// An arbitrary number indicating which landmass this tile is part of,
		// for land-based tiles, or -1 for water.
		//
		// This is used to avoid the expensive process of pathfinding between
		// two land tiles just to discover they have no land connection.
		public int continent;

		// For water tiles, is this tile part of an inland sea with fresh water?
		public bool isFreshWater = false;

		// An arbitrary number indicating which part of the continent this tile
		// is part of, for the purposes of biome assignment.
		public int biomeRegion = -1;

		public City owningCity; // The city whose border contains this tile
		public TerrainType baseTerrainType = TerrainType.NONE;
		public TerrainType overlayTerrainType = TerrainType.NONE;

		public bool HasCity(out City city) {
			city = null;
			if (IsValidCity(cityAtTile)) {
				city = cityAtTile;
				return true;
			}

			return false;
		}
		public bool HasCity() {
			return HasCity(out _);
		}

		private City _cityAtTile;
		public City cityAtTile {
			get => _cityAtTile;
			set {
				_cityAtTile = value;
				// Build city
				if (value != null) {
					BuildCityCallback();
				}
				// Abandon/Destroy city
				else {
					DestroyCityCallback();
				}
			}
		}

		//One thing to decide is do we want to have a tile have a list of units on it,
		//or a unit have reference to the tile it is on, or both?
		//The downside of both is that both have to be updated (and it uses a miniscule amount
		//of memory for pointers), but I'm inclined to go with both since it makes it easy and
		//efficient to perform calculations, whether you need to know which unit on a tile
		//has the best defense, or which tile a unit is on when viewing the Military Advisor.
		public List<MapUnit> unitsOnTile = new List<MapUnit>();
		public string ResourceKey { get; set; }
		public Resource Resource { get; set; }

		public Dictionary<TileDirection, Tile> neighbors { get; set; } = new Dictionary<TileDirection, Tile>();

		public CityResident personWorkingTile = null;   //allows us to see if another city is working this tile

		public bool hasBarbarianCamp = false;

		//See discussion on page 4 of the "Babylon" thread (https://forums.civfanatics.com/threads/0-1-babylon-progress-thread.673959) about sub-terrain type and Civ3 properties.
		//We may well move these properties somewhere, whether that's Civ3ExtraInfo, a Civ3Tile child class, a Dictionary property, or something else, in the future.
		public bool isBonusShield;
		public bool isSnowCapped;
		public bool isPineForest;

		public bool riverNorth;
		public bool riverNortheast;
		public bool riverEast;
		public bool riverSoutheast;
		public bool riverSouth;
		public bool riverSouthwest;
		public bool riverWest;
		public bool riverNorthwest;

		// The first time a forest is cleared on a tile it can award shields to
		// a nearby city.
		public bool hasHadForestCleared = false;

		public TileOverlays overlays;

		public Tile(ID id) {
			this.Id = id;
			unitsOnTile = new List<MapUnit>();
			Resource = Resource.NONE;
			overlays = new(this);
		}

		public static Tile NONE = new Tile(ID.None("tile")) {
			XCoordinate = -1,
			YCoordinate = -1,
		};

		public static bool IsTileValid(Tile tile) {
			return tile != null && tile != NONE;
		}

		private void BuildCityCallback() {
			var hasRoad = this.HasRoad();
			var hasRailroad = this.HasRailroad();

			// remove stuff like a forest, jungle etc (whatever can be cleared by a worker action), but not hills/mountains etc
			if (this.overlayTerrainType.allowedFoliageAction != TerrainType.Civ3FoliageAction.None)
				ClearTerrainOverlay();

			overlays.Clear();

			// Auto connect cities to adjacent road/railroad network
			TryAddRoad(this, hasRoad, hasRailroad);
			TryAddRailroad(this, hasRailroad);

			// Somehow in the base game, craters persist when a city is built on top.
			// I choose to implement this differently here,
			// where ruins are cleared when the city is built
		}

		private void DestroyCityCallback() {
			ClearTerrainOverlay();
			overlays.Clear();
			TryAddRuins(this);
		}

		public bool HasPollution() {
			return this.overlays.GetImprovements().Any(i => i.key == POLLUTION);
		}
		public bool HasRuins() {
			return this.overlays.GetImprovements().Any(i => i.key == RUINS);
		}
		public bool HasCraters() {
			return this.overlays.GetImprovements().Any(i => i.key == CRATERS);
		}

		// TODO: this should be either an extension in C7Engine, or otherwise
		// calculated somewhere else, but it's not obvious to someone unfamiliar
		// with the save format that it's the overaly terrain that has actual
		// movement cost
		public int MovementCost() {
			return overlayTerrainType.movementCost;
		}

		// Like MovementCost(), impassability comes from the overlay terrain
		// that actually exists on the tile.
		public bool IsImpassable() {
			return overlayTerrainType.impassable;
		}

		//This should be used when we want to check if land tiles are next to water tiles.
		//Usually this is coast, but it could be Sea - see the "Deepwater Harbours" topics at CFC.
		//Sometimes we care *specifically* about the Coast terrain, e.g. galleys can only move on that terrain, not Sea or Ocean
		//Those cases should not use this method.
		public bool NeighborsWater() {
			foreach (Tile neighbor in neighbors.Values) {
				if (neighbor.baseTerrainType.isWater()) {
					return true;
				}
			}
			return false;
		}

		public bool NeighborsFreshWater() {
			foreach (Tile neighbor in neighbors.Values) {
				if (neighbor.baseTerrainType.isWater() && neighbor.isFreshWater) {
					return true;
				}
			}
			return false;
		}

		public bool NeighborsOcean() {
			foreach (Tile neighbor in neighbors.Values) {
				if (neighbor.baseTerrainType.isWater() && !neighbor.isFreshWater) {
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Returns neighbors along edges only.
		/// This is used by some graphics algorithms.
		/// </summary>
		/// <returns></returns>
		public Tile[] GetEdgeNeighbors() {
			List<Tile> edgeNeighbors = new();
			if (neighbors.TryGetValue(TileDirection.NORTHEAST, out Tile ne)) edgeNeighbors.Add(ne);
			if (neighbors.TryGetValue(TileDirection.NORTHWEST, out Tile nw)) edgeNeighbors.Add(nw);
			if (neighbors.TryGetValue(TileDirection.SOUTHEAST, out Tile se)) edgeNeighbors.Add(se);
			if (neighbors.TryGetValue(TileDirection.SOUTHWEST, out Tile sw)) edgeNeighbors.Add(sw);
			return edgeNeighbors.ToArray();
		}

		public override string ToString() {
			return "[" + XCoordinate + ", " + YCoordinate + "] (" + overlayTerrainType.Key + " on " + baseTerrainType.Key + ")";
		}

		public List<Tile> GetLandNeighbors() {
			return neighbors.Values.Where(tile => tile != NONE && !tile.baseTerrainType.isWater()).ToList();
		}

		/**
		 * Returns neighbors of the "Coast" type, not including Sea or Ocean.  This is used e.g. for Galley movement.
		 * Eventually, this should be refactored into a more general "get valid neighbors to move to" type of method,
		 * which could work e.g. for units that can move anywhere except desert.
		 **/
		public List<Tile> GetCoastNeighbors() {
			return neighbors.Values.Where(tile => tile.baseTerrainType.Key == "coast").ToList();
		}

		public bool HasRiverCrossing(TileDirection dir) {
			switch (dir) {
				case TileDirection.NORTH: return riverNorth;
				case TileDirection.NORTHEAST: return riverNortheast;
				case TileDirection.EAST: return riverEast;
				case TileDirection.SOUTHEAST: return riverSoutheast;
				case TileDirection.SOUTH: return riverSouth;
				case TileDirection.SOUTHWEST: return riverSouthwest;
				case TileDirection.WEST: return riverWest;
				case TileDirection.NORTHWEST: return riverNorthwest;
				default: throw new ArgumentOutOfRangeException("Invalid TileDirection");
			}
		}

		public bool IsLand() {
			return !baseTerrainType.isWater();
		}

		public bool IsWater() {
			return baseTerrainType.isWater();
		}

		public bool IsCoast() {
			return baseTerrainType.isCoast();
		}

		public bool IsSea() {
			return baseTerrainType.isSea();
		}

		public bool IsCountedForDomination() {
			return IsLand() || IsCoast();
		}

		public bool IsCountedForScore() {
			return IsLand() || IsCoast() || IsSea();
		}

		public bool IsAllowCities() {
			return overlayTerrainType.allowCities && !hasBarbarianCamp;
		}

		public bool IsVolcano() {
			return overlayTerrainType.isVolcano();
		}

		public bool IsRoaded() {
			return this.HasRoad() || this.HasRailroad();
		}

		public bool HasRoad() {
			if (this.HasRailroad())
				return true;

			if (this.overlays.terrainImprovementByLayer.TryGetValue(Layer.Roads, out var value)) {
				if (this.overlays.ImprovementAtLayer(value.layer).key == ROAD)
					return true;
			}
			return false;
		}

		public bool HasRailroad() {
			if (this.overlays.terrainImprovementByLayer.TryGetValue(Layer.Roads, out var value)) {
				if (this.overlays.ImprovementAtLayer(value.layer).key == RAILROAD)
					return true;
			}
			return false;
		}

		public bool HasIrrigation() {
			if (this.overlays.terrainImprovementByLayer.TryGetValue(Layer.ResourceDevelopment, out var value)) {
				if (this.overlays.ImprovementAtLayer(value.layer).key == IRRIGATION)
					return true;
			}
			return false;
		}

		public bool BordersRiver() {
			return riverNorth || riverNortheast || riverEast || riverSoutheast || riverSouth || riverSouthwest || riverWest || riverNorthwest;
		}

		// TODO: This method doesn't handle the electicity tech which allows
		// irrigating without fresh water access.
		public bool CanBeIrrigated(TerrainImprovement irrigation, Player player) {
			// Irrigation can't be done if there is no irrigation bonus for the
			// tile or if there's already an improvement or city on the tile.
			if (!overlays.CanAdd(this, irrigation) ||
				irrigation.GetYieldBonus(overlayTerrainType, YieldType.Food) <= 0 ||
				cityAtTile != null) {
				return false;
			}

			// If a tile borders a river or fresh water, it has fresh water access.
			if (this.BordersRiver() || this.NeighborsFreshWater()) {
				return true;
			}

			foreach (KeyValuePair<TileDirection, Tile> dirToTile in neighbors) {
				// If a neighboring tile is irrigated, this tile has fresh water access.
				if (dirToTile.Value.overlays.HasImprovement(irrigation)) {
					return true;
				}

				// Special case, if we are neighboring a city, check
				// if the city can act as part of an irrigation chain.
				if (dirToTile.Value.cityAtTile != null) {
					if (dirToTile.Value.BordersRiver() || dirToTile.Value.NeighborsFreshWater()) {
						return true;
					}

					foreach (var (dir, tile) in dirToTile.Value.neighbors) {
						if (tile.overlays.HasImprovement(irrigation)) {
							return true;
						}
					}
				}
			}

			return false;
		}

		public Player? OwningPlayer() {
			if (cityAtTile != null) {
				return cityAtTile.owner;
			}
			if (owningCity != null) {
				return owningCity.owner;
			}
			return null;
		}

		public void MaybeAwardForestClearingShields() {
			if (hasHadForestCleared) {
				return;
			}
			hasHadForestCleared = true;

			// Shields can only be awarded if the forest is within some city's
			// borders.
			if (OwningPlayer() == null) {
				return;
			}

			// Check all the tiles of the forest that a city could be in, taking into account the big fat cross size.
			foreach (Tile other in GetTilesWithinRankDistance(EngineStorage.gameData.rules.MaxRankOfWorkableTiles)) {
				if (other.cityAtTile == null) {
					continue;
				}

				// Shields aren't awarded to wonders.
				if (other.cityAtTile.itemBeingProduced is Building b
					&& (b.greatWonderProperties != null || b.isSmallWonder)) {
					continue;
				}

				City c = other.cityAtTile;
				int shieldsAwarded = EngineStorage.gameData.rules.ForestValueInShields;
				c.SetStoredShields(shieldsAwarded, true);
				c.SetStoredShields(Math.Min(c.shieldsStored, c.owner.ShieldCost(c.itemBeingProduced)));

				if (c.owner.isHuman) {
					new MsgShowTemporaryPopup($"{shieldsAwarded} shields awarded for clearing forests", other).send();
				}

				return;
			}
		}

		public MapUnit FindTopDefenderForBombard(MapUnit opponent) {
			return FindTopDefenderForBombard(this, opponent);
		}

		public MapUnit FindTopDefenderForBombard(Tile tile, MapUnit opponent) {
			MapUnit target;
			var combatUnits = tile.unitsOnTile.Where(u => u.IsCombatUnit()).ToList();

			if ((tile.IsLand() && opponent.unitType.isLandBombardmentLethal) || (tile.IsWater() && opponent.unitType.isSeaBombardmentLethal))
				target = FindTopCombatUnit(opponent, combatUnits);
			else
				target = FindTopCombatUnit(opponent, combatUnits.Where(u => u.hitPointsRemaining > 1).ToList());

			return target;
		}

		public MapUnit FindTopDefender(MapUnit opponent) {
			return FindTopDefender(opponent, unitsOnTile);
		}

		public MapUnit FindTopDefender(MapUnit opponent, List<MapUnit> units) {
			if (units.Count > 0) {
				List<MapUnit> potentialDefenders = units.Where(u => u.CanDefendAgainst(opponent)).ToList();
				if (potentialDefenders.Count() == 0) {
					return MapUnit.NONE;
				}

				return FindTopCombatUnit(opponent, potentialDefenders);
			}

			return MapUnit.NONE;
		}

		public MapUnit FindTopCombatUnit(MapUnit opponent, List<MapUnit> units) {
			if (units.Count < 1) return MapUnit.NONE;

			MapUnit leadingCandidate = units[0];
			foreach (MapUnit u in units)
				if (u.HasPriorityAsDefender(leadingCandidate, opponent))
					leadingCandidate = u;
			return leadingCandidate;
		}

		/// <summary>
		/// Disbands non-defending units on a tile.  This should only be called when all defending units have been destroyed,
		/// hence its name.  E.g. if only air/sea units remain after a land battle, this should be called.
		///
		/// Eventually, we should also have a method to make relevant units (workers, artillery, etc.) be captured.
		/// </summary>
		/// <param name="tile"></param>
		public void DisbandNonDefendingUnits(Player owner) {
			//There may have been naval units, if so, disband them
			if (unitsOnTile.Count > 0) {
				//Copy to a separate array so we don't crash due to concurrent modification exceptions
				MapUnit[] unitsOnTile = new MapUnit[this.unitsOnTile.Count];
				this.unitsOnTile.CopyTo(unitsOnTile);
				foreach (MapUnit destroyedUnit in unitsOnTile) {
					// Ensure we only destroy units of the losing side of the
					// combat, not the unit entering the city.
					if (destroyedUnit.owner == owner) {
						destroyedUnit.RemoveFromPlay();
					}
				}
			}
		}

		/// <summary>
		/// After a WorkerJob has finished, Cclean up all the WorkerJobs and set the correct overlay
		/// </summary>
		/// <param name="tile">the current tile</param>
		/// <param name="currentWorkerJob">the worker job currently finished, must not be null</param>
		public void FinishWorkerJob(Terraform currentWorkerJob) {
			// Reset All Workers working on the finished Job
			Player player = null;
			foreach (MapUnit unit in unitsOnTile) {
				player = unit.owner;
				if (currentWorkerJob == unit.WorkerJob) {
					unit.resetWorkerJob();
				}
			}

			currentWorkerJob.OnComplete(player, this);
		}

		public float GetCurrentUnaccountedJobProgress(Terraform currentWorkerJob) {
			return unitsOnTile.Where(unit => currentWorkerJob == unit.WorkerJob).Sum(unit => unit.workerSpeed());
		}

		public async Task AnimateAsync(AnimatedEffect effect) {
			if (!EngineStorage.animationsEnabled) return;

			var msg = new MsgStartEffectAnimation(this, effect, AnimationEnding.Stop);
			msg.send();

			await EngineStorage.WaitForAnimationFinished(msg.animationId);
		}

		public void Animate(AnimatedEffect effect) {
			_ = AnimateAsync(effect);
		}

		public void ClearTerrainOverlay() {
			overlayTerrainType = baseTerrainType;
		}

		public bool HasImprovements => overlays.HasBeenImproved();
	}
}
