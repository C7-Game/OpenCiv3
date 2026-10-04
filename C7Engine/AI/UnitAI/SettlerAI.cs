using C7Engine.Pathing;
using C7GameData;
using C7GameData.AIData;
using Serilog;

namespace C7Engine {
	public class SettlerAI : UnitAI {
		private static ILogger log = Log.ForContext<SettlerAI>();
		public SettlerAIData data;

		public static SettlerAIData MakeAiData(MapUnit unit, Player player) {
			SettlerAIData settlerAiData = new SettlerAIData();
			settlerAiData.goal = SettlerAIData.SettlerGoal.BUILD_CITY;
			//If it's the starting settler, have it settle in place.  Otherwise, use an AI to find a location.
			if (player.cities.Count == 0 && unit.location.cityAtTile == null) {
				settlerAiData.destination = unit.location;
				log.Information("No cities yet!  Set AI for unit to settler AI with destination of " + settlerAiData.destination);
			} else {
				settlerAiData.destination = SettlerLocationAI.FindSettlerLocation(unit.location, player);
				if (settlerAiData.destination == Tile.NONE) {
					//This is possible if all tiles within 4 tiles of a city are either not land, or already claimed
					//by another colonist.  Longer-term, the AI shouldn't be building settlers if that is the case,
					//but right now we'll just spike the football to stop the clock and avoid building immediately next to another city.
					settlerAiData.goal = SettlerAIData.SettlerGoal.JOIN_CITY;
					log.Information($"Set AI for unit {unit.id} of {unit.owner.civilization.name} to JOIN_CITY due to lack of locations to settle");
				} else {
					PathingAlgorithm algorithm = PathingAlgorithmChooser.GetAlgorithm(unit);
					settlerAiData.pathToDestination = algorithm.PathFrom(unit.location, settlerAiData.destination, unit);
					log.Information($"Set AI for unit {unit.id} of {unit.owner.civilization.name} to BUILD_CITY with destination of " + settlerAiData.destination);
				}

				// TODO: return the ranked list, so we can check paths here and avoid duplicate calculations.
			}
			return settlerAiData;
		}

		public SettlerAI(SettlerAIData d) {
			data = d;
		}

		public void UpdateOnDeath() {
			// When we're destroyed, clear out our reference to the unit we're
			// being escorted by, and clear our their reference to us.
			if (data.escort != null && data.escort.currentAI is EscortAI escortAi) {
				escortAi.data.unitToEscort = null;
			}
			data.escort = null;
		}

		C7GameData.UnitAI.MoveResult UnitAI.PlayTurnImpl(Player player, MapUnit unit) {
			switch (data.goal) {
				case SettlerAIData.SettlerGoal.BUILD_CITY:
					if (IsInvalidCityLocation(data.destination)) {
						log.Information("Seeking new destination for settler " + unit.id + " headed to " + data.destination);
						return C7GameData.UnitAI.Result.Error;
					}

					if (unit.location == data.destination) {
						log.Information("Building city with " + unit);
						//TODO: This should use a message, and the message handler should cause the disbanding to happen.
						CityInteractions.BuildCity(unit.location, player, unit.owner.GetNextCityName());
						unit.RemoveFromPlay();
					} else if (data.escort == null) {
						log.Information($"Settler {unit.id} is waiting for an escort");
						unit.movementPoints.onConsumeAll();
						return C7GameData.UnitAI.Result.InProgress;
					} else {
						C7GameData.UnitAI.MoveResult moveResult = this.TryToMoveAlongPath(unit, ref data.pathToDestination);
						if (moveResult.Result == C7GameData.UnitAI.Result.Error) {
							return FindNewDestination(unit, player);
						}
						return moveResult;
					}
					break;
				case SettlerAIData.SettlerGoal.JOIN_CITY:
					if (unit.location.cityAtTile != null) {
						//TODO: Actually join the city.  Haven't added that action.
						//For now, just get rid of the unit.  Sorry, bro.
						unit.RemoveFromPlay();
					} else {
						//TODO: Eventually, go to the city we're supposed to join
						//For now, just disband
						unit.RemoveFromPlay();
					}
					break;
			}

			return C7GameData.UnitAI.Result.Done;
		}

		private static bool IsInvalidCityLocation(Tile tile) {
			if (tile.cityAtTile != null) {
				return true;
			}
			foreach (Tile neighbor in tile.neighbors.Values) {
				if (neighbor.cityAtTile != null) {
					return true;
				}
			}
			return false;
		}

		public string SummarizePlan() {
			return "SettlerAI: " + data.ToString();
		}

		// The destination became unreachable (issue #213); pick a new one, or
		// fall back to JOIN_CITY if nothing is left.
		// TODO: prefer path-checking at selection time over exclude-and-repick.
		public C7GameData.UnitAI.MoveResult FindNewDestination(MapUnit unit, Player player) {
			data.unreachableDestinations.Add(data.destination);
			log.Information($"Settler {unit.id} cannot reach {data.destination}, retargeting");

			Tile newDestination = SettlerLocationAI.FindSettlerLocation(unit.location, player, data.unreachableDestinations);
			if (newDestination == Tile.NONE) {
				data.goal = SettlerAIData.SettlerGoal.JOIN_CITY;
				log.Information($"Settler {unit.id} has no reachable destination left, joining a city instead");
			} else {
				data.destination = newDestination;
				PathingAlgorithm algorithm = PathingAlgorithmChooser.GetAlgorithm(unit);
				data.pathToDestination = algorithm.PathFrom(unit.location, newDestination, unit);
				log.Information($"Settler {unit.id} retargeting from an unreachable tile to {newDestination}");
			}

			// Consume movement so PlayTurn does not retry the failed move this turn.
			unit.movementPoints.onConsumeAll();
			return C7GameData.UnitAI.Result.InProgress;
		}
	}
}
