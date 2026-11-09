using System.Collections.Generic;
using C7GameData;
using C7GameData.Save;
using C7GameData.AIData;
using Serilog;
using System;
using C7Engine.Pathing;
using System.Linq;

namespace C7Engine.AI.UnitAI {
	/// <summary>
	/// A unit whose intended role is combat, other than purely point-defense combat.
	///
	/// This is likely to evolve significantly, and likely have sub-classes; this is the very first iteration.
	/// This first iteration is focused on defeating barbarians.
	/// </summary>
	public class CombatAI : C7GameData.UnitAI {
		private CombatAIData data;

		private static ILogger log = Log.ForContext<CombatAI>();

		public CombatAI(CombatAIData d) {
			data = d;
		}

		public static CombatAIData? MakeAiData(MapUnit unit, Player player) {
			CombatAIData? result = GetBestTileToAttack(unit, player, GetPlayersAtWarWith(player));
			if (result == null) {
				return result;
			}

			log.Information($"{unit} is planning to attack {result.destination}");
			return result;
		}

		public void UpdateOnDeath() { }

		public C7GameData.UnitAI.MoveResult PlayTurnImpl(Player player, MapUnit unit) {
			if (data == null) {
				return C7GameData.UnitAI.Result.Error;
			}

			if (unit.location == data.destination) {
				return C7GameData.UnitAI.Result.Done;
			}

			// If there is a no longer an enemy on the tile we were heading
			// towards, ensure we recalculate our plan.
			Tile dest = data.destination;
			bool destinationHasEnemyUnits = dest.unitsOnTile.Count > 0
				&& !dest.unitsOnTile[0].owner.IsAtPeaceWith(unit.owner);
			bool destinationHasEnemyCity = dest.cityAtTile != null
				&& !data.destination.cityAtTile.owner.IsAtPeaceWith(unit.owner);
			bool destinationHasBarbCamp = dest.HasBarbarianCamp();

			if (!destinationHasEnemyCity && !destinationHasEnemyUnits && !destinationHasBarbCamp) {
				return C7GameData.UnitAI.Result.Done;
			}

			// See if we need to re-evaluate our target if we walk next to an
			// enemy unit or city. We don't return an error here to avoid the
			// case of looping forever if our target didn't change.
			if (data.path.PathLength() > 1) {
				foreach (Tile t in unit.location.neighbors.Values) {
					bool nextToEnemyCity = t.cityAtTile != null && !t.cityAtTile.owner.IsAtPeaceWith(unit.owner);
					bool nextToEnemyUnit = t.unitsOnTile.Count > 0 && !t.unitsOnTile[0].owner.IsAtPeaceWith(unit.owner);
					bool nextToBarbCamp = t.HasBarbarianCamp();

					if (!nextToEnemyCity && !nextToEnemyUnit && !nextToBarbCamp) {
						continue;
					}

					// Once we re-evaluate once, break out of the loop. There's
					// no point in doing it again if nothing changed.
					CombatAIData? maybeData = MakeAiData(unit, player);
					if (maybeData != null && maybeData.destination != data.destination) {
						data = maybeData;
					}
					break;
				}
			}

			// Move along the path, initiating combat if we reach our target.
			return this.TryToMoveAlongPath(unit, ref data.path);
		}

		public string SummarizePlan() {
			return "CombateAI: " + data.ToString();
		}

		private static HashSet<ID> GetPlayersAtWarWith(Player player) {
			HashSet<ID> enemyIds = new();

			// Special case: barbarians are at war with all other players but
			// don't have player relationships with them.
			if (player.isBarbarians) {
				foreach (Player p in EngineStorage.gameData.players) {
					if (!p.isBarbarians) {
						enemyIds.Add(p.id);
					}
				}
				return enemyIds;
			}

			foreach (KeyValuePair<ID, PlayerRelationship> p in player.playerRelationships) {
				if (p.Value.AtWar()) {
					enemyIds.Add(p.Key);
				}
			}
			return enemyIds;
		}

		private static CombatAIData? GetBestTileToAttack(MapUnit unit, Player player, HashSet<ID> enemyIds) {
			Dictionary<Tile, float> scoredTiles = new();

			// First we want to check all the tiles in our visible knowledge for
			// enemy units. As of 2025-03-09, we don't track known and visible
			// tiles separately, so this should be updated in the future.
			foreach (Tile t in player.tileKnowledge.knownTiles) {
				// Barbarians don't have unified knowledge across their "empire",
				// only local knowledge.
				if (player.isBarbarians && t.DistanceTo(unit.location) > 4) {
					continue;
				}

				float score = ScoreTile(t, unit, player, enemyIds);
				if (score == int.MinValue) {
					continue;
				}

				scoredTiles.Add(t, score);
			}

			if (scoredTiles.Count == 0) {
				return null;
			}

			// Find the best target to attack.
			IOrderedEnumerable<KeyValuePair<Tile, float>> sortedScoredTiles =
				scoredTiles.OrderByDescending(x => x.Value);
			PathingAlgorithm algorithm = PathingAlgorithmChooser.GetAlgorithm(unit);

			foreach (KeyValuePair<Tile, float> p in sortedScoredTiles) {
				CombatAIData result = new();
				result.destination = p.Key;
				result.path = algorithm.PathFrom(unit.location, result.destination, unit);

				// If we can't reach the destination, go to the next candidate.
				if ((result.path?.PathLength() ?? -1) == -1) {
					continue;
				}

				return result;
			}

			return null;
		}

		private static float ScoreTile(Tile t, MapUnit unit, Player player, HashSet<ID> enemyIds) {
			bool hasEnemyCity = t.cityAtTile != null && enemyIds.Contains(t.cityAtTile.owner.id);
			bool hasEnemyUnits = t.unitsOnTile.Count > 0 && enemyIds.Contains(t.unitsOnTile[0].owner.id);
			float score = 0;

			// Ignore tiles without units or cities.
			if (!hasEnemyCity && !hasEnemyUnits) {
				return int.MinValue;
			}

			// Ignore land tiles for sea units, and vice versa.
			if (t.IsLand() != unit.IsLandUnit()) {
				return int.MinValue;
			}

			// Handle the case of units running around.
			//
			// Note: Civ3 rates unit strength using this formula:
			//   HP * (1.5* AttackPoints + 1 * DefensePoints ) 0.175 * Bombard Points
			//   (https://forums.civfanatics.com/threads/study-of-inner-workings-of-military-advisor.83599/)
			//
			// TODO: We should figure out how to incorporate this.
			if (hasEnemyUnits && !hasEnemyCity) {
				MapUnit defender = t.FindTopDefender(unit);

				// Units in our territory are a threat.
				if (t.owningCity != null && t.owningCity.owner == player) {
					score += 3 * defender.unitType.attack;
				}

				foreach (Tile neighbor in t.neighbors.Values) {
					// Units next to our cities are an even bigger threat. Note
					// that it is possible for an enemy unit to be next to a
					// city but not in our borders if two cities are very close
					// together.
					if (neighbor.cityAtTile != null && neighbor.cityAtTile.owner == player) {
						score += 3 * defender.unitType.attack;
					}

					if (neighbor == unit.location) {
						// Units next to us are also a threat.
						score += 1 * defender.unitType.attack;
					}
				}
			}

			if (hasEnemyCity) {
				// Unguarded cities are pretty tempting.
				if (!hasEnemyUnits) {
					score += 20;
				} else {
					// TODO: this should incorporate defender strength
					// TODO: we really want stack attacks
					score += 10;
				}
			}

			// Prefer to attack nearer targets.
			score -= (float)Math.Pow(t.DistanceTo(unit.location), 2);

			return score;
		}
	}
}
