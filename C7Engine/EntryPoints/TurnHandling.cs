using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using C7GameData;
using Serilog;

namespace C7Engine {

	public class TurnHandling {
		private static ILogger log = Log.ForContext<TurnHandling>();

		public static void OnBeginTurn() {
			GameData gameData = EngineStorage.gameData;
			log.Information("\n*** Beginning turn " + gameData.turn + " ***");
		}

		public static void OnEndTurn(Player player) {
			GameData gameData = EngineStorage.gameData;

			var busyWorkers = gameData.mapUnits.Where(u => u.owner.id == player.id && u.WorkerJob != null);
			foreach (MapUnit busyWorker in busyWorkers)
				if (busyWorker.WorkerJob != null)
					_ = busyWorker.PerformEndOfTurnAction();
		}

		public static void InitTurnData(Player player = null, bool skipTurn = false) {
			GameData gameData = EngineStorage.gameData;
			if (player == null) {
				foreach (MapUnit mapUnit in gameData.mapUnits)
					mapUnit.OnBeginTurn(skipTurn);
			} else {
				foreach (MapUnit mapUnit in gameData.mapUnits.Where(u => u.owner == player))
					mapUnit.OnBeginTurn(skipTurn);
			}
		}

		// Implements the game loop. This method is called when the game is started and when the player signals that they're done moving.
		public static async Task AdvanceTurn() {
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();
			GameData gameData = EngineStorage.gameData;

			// Loop ends with a function return once we reach the UI controller during the movement phase
			while (true) {
				bool firstTurn = GetTurnNumber() == 0;

				// Movement phase
				if (await PlayPlayerTurns(gameData, firstTurn)) {
					stopwatch.Stop();
					log.Debug("Turn time took " + stopwatch.ElapsedMilliseconds + " milliseconds");
					return;
				}

				// Clear all wait queue, so if a player ended the turn without handling all waited units, they are selected
				// at the same place in the order. Confirmed this is what Civ3 does.
				UnitInteractions.ClearWaitQueue();

				BarbarianInteractions.SpawnBarbarians(gameData);

				gameData.turn++;
				foreach (Player player in gameData.players) {
					player.MaybeSpawnBonusUnits(gameData);
					player.RecalculateCitizenMoods(gameData, goIntoDisorderIfUnhappy: true);
					player.DoCorruptionCalculations(gameData);

					// Do the financial updates before updating the cities, so
					// that a newly produced unit won't put a player over the
					// unit support cap and cause them to lose gold unexpectedly.
					player.DoPerTurnFinanceUpdates(gameData);
					player.DoPerTurnScienceUpdates(gameData);

					// Note that we do growth after calculating citizen moods,
					// to ensure that the player has a chance to deal with the
					// unhappiness of a new citizen during their turn.
					log.Information($"\n*** City growth/production for turn {gameData.turn}, player {player} ***");
					player.HandleCityUpdates(gameData);

					player.UpdateHistory(gameData);
				}

				CheckVictory(gameData);

				// Now that the turn is ending, do all the bookkeeping for the
				// start of the next turn. We don't put the "hasPlayedThisTurn"
				// logic in OnBeginTurn because OnBeginTurn is called when a
				// save game is loaded, and that would erase the saved information
				// about which players have played.
				OnBeginTurn();
				InitTurnData();
				foreach (Player player in gameData.players) {
					player.hasPlayedThisTurn = false;
				}
			}
		}

		/// <summary>
		/// Plays the turns for all the players in the game (including barbarians).
		/// </summary>
		/// <param name="gameData"></param>
		/// <param name="firstTurn"></param>
		/// <returns>true when it is time for the human to take control again</returns>
		private static async Task<bool> PlayPlayerTurns(GameData gameData, bool firstTurn) {
			// Order players: Human -> AI -> Barbarian AI
			var orderedPlayers = gameData.players.OrderByDescending(p => !p.isBarbarians).ThenByDescending(p => p.isHuman).ToList();
			foreach (Player player in orderedPlayers) {
				if (player.hasPlayedThisTurn || player.defeated) {
					continue;
				}

				if (firstTurn && player.SitsOutFirstTurn()) {
					continue;
				}

				if (player.isBarbarians) {
					await BarbarianAI.PlayTurn(player, gameData);
				} else if (!player.isHuman) {
					await PlayerAI.PlayTurn(player, gameData);
				}

				if (player.id != EngineStorage.uiControllerID) {
					OnEndTurn(player);
					player.hasPlayedThisTurn = true;
				}

				//Human player check. Let the human see what's going on even if they are in observer mode.
				if (player.id == EngineStorage.uiControllerID) {
					new MsgStartTurn().send();
					return true;
				}
			}
			return false;
		}

		///Eventually we'll have a game year or month or whatever, but for now this provides feedback on our progression
		public static int GetTurnNumber() {
			return EngineStorage.gameData.turn;
		}

		private static void CheckVictory(GameData gameData) {
			if (gameData.winner != null || gameData.victoryType != null)
				return; // Game is already over

			List<Tuple<Player, IVictory>> winners = [];

			foreach (Player player in gameData.players) {
				if (player.isBarbarians)
					continue;

				foreach (IVictory victory in gameData.victories) {
					VictoryStatus status = victory.Evaluate(player, gameData);
					if (victory.HasVictory(status)) {
						winners.Add(new Tuple<Player, IVictory>(player, victory));
						break;
					}
				}
			}

			if (winners.Count == 1) {
				DeclareVictory(winners[0].Item1, winners[0].Item2, gameData);
			} else if (winners.Count > 1) {
				var topScoring = winners.OrderByDescending(pv => {
					var player = pv.Item1;
					HistTurnRecord lastTurn = gameData.history[player.id.ToString()].LastOrDefault();
					return lastTurn?.Score ?? 0;
				}).First();
				DeclareVictory(topScoring.Item1, topScoring.Item2, gameData);
			}
		}

		private static void DeclareVictory(Player winner, IVictory victory, GameData gameData) {
			Log.Information("Player {Winner} has wone a {Victory} victory!",
				winner, victory.Header());

			gameData.winner = winner;
			gameData.victoryType = victory;

			new MsgVictory(winner, victory).send();
		}
	}
}
