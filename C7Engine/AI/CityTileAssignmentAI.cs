using System;
using C7GameData;
using System.Linq;
using Serilog;

namespace C7Engine.AI {
	public class CityTileAssignmentAI {
		public static int DesiredFoodSurplusPerTurn = 2;
		private static readonly int FOOD_PER_CITIZEN = 2;   //eventually will be configured by rules


		private static int foodPriorityRate = 40;
		private static int productionPriorityRate = 50;
		private static int commercePriorityRate = 30;

		private static ILogger log = Log.ForContext<CityTileAssignmentAI>();

		// Assigns a citizen, which is alredy part of a city, to a tile, if possible.
		//
		// When a city is having all of its citizens re-assigned one at a time,
		// pass the final head count as totalResidents so that the food target
		// accounts for citizens who haven't been placed yet.
		public static void AssignNewCitizenToTile(GameData gameData, CityResident newResident, bool manageMoods = false, int totalResidents = 0) {
			City city = newResident.city;
			int targetTileFoodAmount = TargetTileFoodAmount(city, totalResidents);

			double maxScore = 0;
			Tile preferredTile = Tile.NONE;
			foreach (Tile t in city.GetWorkableTiles()) {
				if (t.personWorkingTile == null) {
					double score = CalculateTileYieldScore(t, targetTileFoodAmount, city.owner);
					log.Debug($"Tile {t} scored {score}");
					if (score > maxScore) {
						maxScore = score;
						preferredTile = t;
					}
				}
			}

			string yield = city.location.YieldString(city.owner);
			log.Debug($"Assigning new citizen of {city.name} to tile {preferredTile} with yield {yield}");

			if (preferredTile == Tile.NONE) {
				newResident.citizenType = city.owner.GetKnownSpecialists(gameData)[0];
			} else {
				newResident.tileWorked = preferredTile;
				preferredTile.personWorkingTile = newResident;
			}

			// Check to see if this citizen working a tile would cause the city
			// to be unhappy. If so, make them an entertainer.
			City.Mood cityMood = city.RecalculateCitizenMoods(gameData);

			if (cityMood == City.Mood.Unhappy && manageMoods) {
				// Take the worker off the least valuable tile rather than
				// whoever happened to be added last, otherwise a citizen who
				// was placed to cover the city's food can get undone while
				// earlier, less food-focused picks remain.
				CityResident entertainer = newResident.tileWorked == Tile.NONE
					? newResident
					: FindLeastValuableWorker(city, TargetTileFoodAmount(city, totalResidents));
				MakeEntertainer(gameData, entertainer);
			}
		}

		// The food the tiles of citizens yet to be placed need to provide to
		// reach the desired surplus.
		internal static int TargetTileFoodAmount(City city, int totalResidents) {
			int residents = Math.Max(totalResidents, city.residents.Count);
			int desiredFoodRate = residents * FOOD_PER_CITIZEN + DesiredFoodSurplusPerTurn;
			return desiredFoodRate - city.CurrentFoodYield();
		}

		private static CityResident FindLeastValuableWorker(City city, int targetFoodAmount) {
			return city.residents
				.Where(r => r.tileWorked != Tile.NONE)
				.MinBy(r => CalculateTileYieldScore(r.tileWorked, targetFoodAmount, city.owner));
		}

		private static void MakeEntertainer(GameData gameData, CityResident resident) {
			resident.citizenType = resident.city.owner.GetKnownSpecialists(gameData).MaxBy(x => x.Luxuries);
			if (resident.tileWorked != Tile.NONE) {
				resident.tileWorked.personWorkingTile = null;
				resident.tileWorked = Tile.NONE;
			}
		}

		public static double CalculateTileYieldScore(Tile t, int targetFoodAmount, Player player) {
			int score = t.FoodYield(player).yield * foodPriorityRate
				+ t.ProductionYield(player).yield * productionPriorityRate
				+ t.CommerceYield(player).yield * commercePriorityRate;
			int penalty = (targetFoodAmount - t.FoodYield(player).yield);
			if (penalty <= 0) {
				return score;
			}
			return score / (Math.Pow(2, penalty));
		}
	}
}
