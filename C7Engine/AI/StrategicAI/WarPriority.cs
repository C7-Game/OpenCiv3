using System;
using System.Linq;
using System.Collections.Generic;
using C7Engine;
using C7Engine.AI.StrategicAI;
using C7GameData.Save;
using Serilog;

namespace C7GameData.AIData {
	/// <summary>
	/// Represents a goal of making war with another nation.
	/// Although I don't expect for this to be fully fleshed out by Carthage (diplomacy is way off in the future),
	/// having a priority that stores data is important for fleshing out how this is going to work, and this is an obvious
	/// case of a priority that will store data.
	/// </summary>
	public class WarPriority : StrategicPriority {
		private static ILogger log = Log.ForContext<WarPriority>();

		private readonly int TEMP_WAR_PRIORITY_WEIGHT = 50; //temporary weight of this priority, if it isn't zero

		public WarPriority() { }

		public override string ToString() {
			return "WarPriority";
		}

		/// <summary>
		/// For now, we're simply going to say if we've run out of room for expansion, we'll fight someone.
		/// As we add more elements to the game, this should get more complex, as things like science and industry are considered.
		/// </summary>
		/// <param name="player"></param>
		/// <returns></returns>
		public override void CalculateWeightAndMetadata(Player player) {
			if (player.cities.Count < 2) {
				this.calculatedWeight = 0;
				return;
			}

			// If we're at war, make sure we're prioritizing war.
			foreach (KeyValuePair<ID, PlayerRelationship> p in player.playerRelationships) {
				// TODO: Make sure having seen barbarians doesn't prevent us from
				// declaring new wars.
				if (p.Value.AtWar()) {
					this.calculatedWeight = 1000;
					return;
				}
			}

			bool outOfLandToExpandTo = UtilityCalculations.CalculateAvailableLandScore(player) < 1;

			// Don't go to war if there's still land we should be expanding to.
			if (!outOfLandToExpandTo) {
				this.calculatedWeight = 0;
				return;
			}

			var (_, _, unitSupportCost) = player.TotalUnitsAllowedUnitsAndSupportCost();
			bool overUnitSupportCap = unitSupportCost > 0;

			// If we're still under the unit support cap, grow our military more
			// before going to war.
			if (!overUnitSupportCap) {
				this.calculatedWeight = 0;
				return;
			}

			// Pick the player we're going to war with.
			//
			// TODO: We should also consider growing "vertically" if we're over
			// the unit support cap, and increasing the size of our cities in 
			// this situation, depending on the government type. This would be
			// especially true if we're weak to all our opponents.
			Player toFight = PickPlayerToFight(player);
			if (toFight == null) {
				this.calculatedWeight = 0;
				return;
			}
			player.DeclareWarOn(toFight, EngineStorage.gameData.turn);
			log.Information($"{player} declared war on {toFight}");
			new MsgWarDeclarationNotification(player, toFight).send();
			this.calculatedWeight = 1000;
		}

		private static Player PickPlayerToFight(Player player) {
			Dictionary<Player, float> scoredOpponents = new();
			Dictionary<Player, int> borderTileCount = CountSharedBorderTiles(player);

			// Calculate a score for each of our potential opponents.
			foreach (Player p in EngineStorage.gameData.players) {
				float score = 0;

				// We always fight barbarians, we don't need to declare war on
				// them.
				if (p.isBarbarians) {
					continue;
				}

				// We can't fight ourselves.
				if (player == p) {
					continue;
				}

				// Players we share a longer border with are more likely to be
				// our enemy.
				score += borderTileCount[p];

				// The further away an opponent is the harder a war will be.
				score -= DistanceToClosestCity(player, p);

				// We want to be more likely to declare war on our weaker opponents,
				// so scale our scores based on strength.
				float us = player.CalculateMilitaryStrength();
				float them = p.CalculateMilitaryStrength();

				score *= us / them;

				scoredOpponents[p] = score;
			}

			// Pick the highest score to declare war on.
			float bestScore = int.MinValue;
			Player bestOpponent = null;
			log.Information($"Evaluating possible enemies for {player}...");
			foreach (KeyValuePair<Player, float> pair in scoredOpponents) {
				log.Information($"  {pair.Key} : {pair.Value}");
				if (pair.Value > bestScore) {
					bestScore = pair.Value;
					bestOpponent = pair.Key;
				}
			}

			return bestOpponent;
		}

		private static Dictionary<Player, int> CountSharedBorderTiles(Player player) {
			// Use a hash set of tiles per player to avoid double counting.
			Dictionary<Player, HashSet<Tile>> borderTiles = new();
			foreach (Player p in EngineStorage.gameData.players) {
				borderTiles.Add(p, new HashSet<Tile>());
			}

			foreach (Tile t in EngineStorage.gameData.map.tiles) {
				if (t.OwningPlayer() != player) {
					continue;
				}

				// Check to see if we have a neighbor owned by our opponent.
				foreach (Tile n in t.neighbors.Values) {
					Player? other = n.OwningPlayer();
					if (other == null) {
						continue;
					}

					borderTiles[other].Add(n);
				}
			}

			return borderTiles.ToDictionary(x => x.Key, x => x.Value.Count);
		}

		// Calculate the smallest pairwise distance between cities of the two
		// players. This is O(N^2), but shouldn't run very often and N shouldn't
		// be excessively large.
		private static int DistanceToClosestCity(Player a, Player b) {
			int result = int.MaxValue;

			foreach (City ca in a.cities) {
				foreach (City cb in b.cities) {
					// TODO: Implement cross-continent fighting.
					if (ca.location.continent != cb.location.continent) {
						continue;
					}

					result = Math.Min(result, ca.location.DistanceTo(cb.location));
				}
			}
			return result;
		}
	}
}
