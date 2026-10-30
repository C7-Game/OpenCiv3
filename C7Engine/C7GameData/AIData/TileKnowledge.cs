using System.Collections.Generic;
using System.Linq;
using C7Engine;
using Serilog;

namespace C7GameData {
	public class TileKnowledge {
		private readonly Player _player;

		public TileKnowledge(Player player) {
			_player = player;
		}

		public HashSet<Tile> knownTiles { get; private set; } = new();
		public Dictionary<ID, IEnumerable<string>> knownTerrainImprovements { get; private set; } = new();
		public HashSet<Tile> borderTiles { get; private set; } = new();
		private HashSet<Tile> activeTiles = new HashSet<Tile>();

		// Has this player explored all known ocean tiles?
		// TODO: this should be split out for coast/ocean
		public bool fullyExploredOceans = false;

		// The set of tiles this player currently has explorers headed towards.
		public HashSet<Tile> aiExplorationTargets = new();

		public void AddTilesToKnown(Tile location, bool recomputeActiveTiles = true) {
			knownTiles.Add(location);
			borderTiles.Remove(location);

			// Crude benchmarking tool for GetTilesVisibleToUnit, which can be
			// a hot function when profiling.
			// {
			// 	Stopwatch stopwatch = new Stopwatch();
			// 	stopwatch.Start();

			// 	for (int i = 0; i < 10000; ++i) {
			// 		foreach (Tile t in GetTilesVisibleToUnit(location)) {
			// 			knownTiles.Add(t);
			// 		}
			// 	}

			// 	System.Console.WriteLine($"10k runs took: {stopwatch.ElapsedMilliseconds} milliseconds");
			// }

			foreach (Tile t in GetTilesVisibleFrom(location)) {
				knownTiles.Add(t);
				borderTiles.Remove(t);

				knownTerrainImprovements[t.Id] = t.overlays.GetImprovements().Select(ti => ti.key).ToList();

				foreach (Tile border in t.neighbors.Values) {
					if (border == Tile.NONE) {
						continue;
					}
					if (!knownTiles.Contains(border)) {
						borderTiles.Add(border);
					}
				}
			}

			if (recomputeActiveTiles) {
				RecomputeActiveTiles();
			}
		}

		public List<Tile> GetTilesVisibleFrom(Tile location) {
			if (location.unitsOnTile.Count > 0) {
				return GetTilesVisibleToUnit(location);
			}

			List<Tile> tiles = new List<Tile>();
			foreach (var neighbor in location.neighbors.Values) {
				tiles.Add(neighbor);
			}

			return tiles;
		}

		public List<Tile> GetTilesVisibleToUnit(Tile unitLocation) {
			// TODO: Make visibility configurable in game rules
			// Space for current tile, 8 inner ring tiles, 16 outer ring tiles
			List<Tile> result = new(25);
			result.Add(unitLocation);
			int unitHeight = unitLocation.overlayTerrainType.height;

			var rules = EngineStorage.gameData.rules;

			// Units with radar can see X (2 in OG game, configurable here) tiles away, regardless of terrain.
			// They also don't need to be the "top" unit on the tile, if they exist on the tile, they boost visibility.
			var anyRadarUnits = unitLocation.unitsOnTile.Any(u => u.unitType.hasRadar);
			if (anyRadarUnits) {
				result.AddRange(unitLocation.GetTilesWithinTileSquare(rules.RadarTileVisibility).Where(Tile.IsTileValid).ToList());
				return result;
			}

			foreach (var (innerTileDirection, innerRingNeighbor) in unitLocation.neighbors) {
				if (innerRingNeighbor == Tile.NONE) {
					continue;
				}
				result.Add(innerRingNeighbor);
				int innerHeight = innerRingNeighbor.overlayTerrainType.height;

				foreach (var (outerTileDirection, outerRingNeighbor) in innerRingNeighbor.neighbors) {
					// Say we have the following. We are standing on the XX tile
					// and need to figure out which tiles we can see. We can see
					// the hill directly to our SW, because we're next to it.
					// We can see the hill that is S+SW of us, because it's
					// diagonal to us and we can see tiles of height 2 from 2
					// tiles away. We can't see the hill that is SW+SW of us
					// because we are blocked. To implement this we need to
					// prevent 90 degree "turns", which we handle by ensuring
					// the innerTileDirection+outerTileDirection combo is never
					// a combination of two N/S/E/W dirs (except for when they are the same).
					//
					//                      <  ..  >
					//                  <  ..  ><  XX  >
					//              <  ..  >< Hill ><  gg  >
					//                  < Hill ><  gg  >
					//                      < Hill >

					// N->N, S->S, W->W, E->E, etc
					var hasDirectLineOfSight = ((int)innerTileDirection == (int)outerTileDirection);

					if (!hasDirectLineOfSight && (((int)innerTileDirection) % 2 == 0 && ((int)outerTileDirection) % 2 == 0)) {
						continue;
					}

					if (outerRingNeighbor == Tile.NONE) {
						continue;
					}
					int outerHeight = outerRingNeighbor.overlayTerrainType.height;

					// Water tiles provide us with a boost in visibility,
					// so we can see water & land tiles that are further away from a water tile,
					// but land tiles "block" any tiles that are in their line of sight

					if ((outerRingNeighbor.IsWater() && !innerRingNeighbor.IsLand())) {
						result.Add(outerRingNeighbor);
						continue;
					}

					if ((outerRingNeighbor.IsLand() && innerRingNeighbor.IsWater())) {
						result.Add(outerRingNeighbor);
						continue;
					}

					// Tiles with a height of at least 2 are visible from 2 tiles
					// away as long as the tile in between is 2 less than the
					// outer tile (so you can see mountains over hills, but
					// not hills over forest).
					if (outerHeight >= 2 && innerHeight + 2 <= outerHeight) {
						result.Add(outerRingNeighbor);
						continue;
					}

					// You can also see tiles whose height is 2 lower than where
					// you are standing, as long as the tile in the middle has
					// height at least 2 lower than you.
					if (outerHeight + 1 <= unitHeight && innerHeight + 2 <= unitHeight) {
						result.Add(outerRingNeighbor);
						continue;
					}
				}
			}
			return result;
		}

		// neighboring tiles should not be added when loading tile knowledge
		// from a .sav file
		internal bool AddTileToKnown(Tile unitLocation) {
			bool added = knownTiles.Add(unitLocation);
			borderTiles.Remove(unitLocation);

			foreach (Tile border in unitLocation.neighbors.Values) {
				if (border == Tile.NONE) {
					continue;
				}
				if (!knownTiles.Contains(border)) {
					borderTiles.Add(border);
				}
			}

			return added;
		}

		public bool isTileKnown(Tile t) {
			return knownTiles.Contains(t);
		}

		public bool isActiveTile(Tile t) {
			if (t == Tile.NONE) {
				return false;
			}
			return activeTiles.Contains(t);
		}

		/**
		 * Returns a copy of the list of known tiles.
		 * This prevents external modifications.
		 **/
		public List<Tile> AllKnownTiles() {
			List<Tile> list = new List<Tile>();
			foreach (Tile t in knownTiles) {
				list.Add(t);
			}
			return list;
		}

		public void RecomputeActiveTiles() {
			activeTiles.Clear();
			foreach (Tile t in knownTiles) {
				// A tile within a city's borders and all of its neighbors are active.
				// A unit's visibility might be going further than that of a city,
				// so we don't need to calculate this here.
				if (t.unitsOnTile.Count < 1 && t.owningCity != null && t.owningCity.owner == _player) {
					activeTiles.Add(t);

					foreach (Tile neighbor in t.neighbors.Values) {
						activeTiles.Add(neighbor);
					}
				}

				// A tile with a unit on it and all of its neighbors are active.
				if (t.unitsOnTile.Count > 0 && t.unitsOnTile[0].owner == _player) {
					foreach (Tile x in GetTilesVisibleToUnit(t)) {
						activeTiles.Add(x);
					}
				}
			}
		}

		public List<Tile> OwnedTiles() {
			return knownTiles.Where(t => t.OwningPlayer() == _player).ToList();
		}

		public List<Tile> DominationTiles() {
			return OwnedTiles().Where(t => t.IsCountedForDomination()).ToList();
		}

		public List<Tile> ScoreTiles() {
			return OwnedTiles().Where(t => t.IsCountedForScore()).ToList();
		}
	}
}
