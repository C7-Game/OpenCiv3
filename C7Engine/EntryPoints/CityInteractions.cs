using System.Linq;
using C7Engine.AI;
using Serilog;

namespace C7Engine {
	using System;
	using C7GameData;

	public class CityInteractions {
		private static ILogger log = Log.ForContext<CityInteractions>();

		public static City BuildCity(Tile tileWithNewCity, Player owner, string name) {
			GameData gameData = EngineStorage.gameData;
			City newCity = new City(tileWithNewCity, owner, name, gameData.ids.CreateID("city"));
			if (owner.cities.Count == 0) {
				newCity.capital = true;
				newCity.AddBuilding(gameData.Buildings.Find(x => x.isCenterOfEmpire));
			}
			gameData.cities.Add(newCity);
			owner.cities.Add(newCity);
			tileWithNewCity.cityAtTile = newCity;

			CityResident firstResident = new CityResident();
			firstResident.nationality = owner.civilization;
			firstResident.city = newCity;
			firstResident.citizenType = gameData.citizenTypes.Find(x => x.IsDefaultCitizen);
			newCity.AddCitizen(firstResident);

			// Update owners before we assign the citizen so the tile owners are
			// accurate. We do this after adding the resident though, because
			// cities with zero residents are considered destroyed.
			gameData.UpdateTileOwners();

			// Now that the city exists and its borders have been established,
			// invalidate the trade network so it can be recomputed with this
			// new information.
			gameData.InvalidateCachedTradeNetwork();

			// Assigning citizens to tiles requires knowing luxuries, so this
			// has to happen after invalidating the trade network.
			CityTileAssignmentAI.AssignNewCitizenToTile(gameData, firstResident);

			newCity.SetItemBeingProduced(ChooseProducible.Choose(newCity, owner));

			// Redo corruption calculations after a city is created, since it
			// may change rank corruption values.
			owner.DoCorruptionCalculations(gameData);

			return newCity;
		}

		public static void DestroyCity(City city) {
			DestroyCity(city.location);
		}
		public static void DestroyCity(Tile tile) {
			DestroyCity(tile.XCoordinate, tile.YCoordinate);
		}

		public static void DestroyCity(int X, int Y) {
			GameData gameData = EngineStorage.gameData;
			Tile tile = gameData.map.tileAt(X, Y);
			Player owner = tile.cityAtTile.owner;

			// TODO: this will get removed eventually, since we will be capturing non-combat units,
			// plus, it doesn't what it says, if the city is abandoned for example, ALL units are removed.
			// I am leaving it as it is for the moment.
			tile.DisbandNonDefendingUnits(owner);

			tile.cityAtTile.RemoveAllCitizens();
			tile.cityAtTile.owner.cities.Remove(tile.cityAtTile);

			gameData.cities.Remove(tile.cityAtTile);
			gameData.UpdateTileOwnersOnCityDestruction(tile.cityAtTile);

			new MsgCityDestroyed(tile.cityAtTile).send();

			gameData.CheckForCivDestructionAndNotifyUi(owner);

			tile.cityAtTile = null;

			// Now that the city has been destroyed and tile owners updated,
			// invalidate the trade network in case removing this city cut off
			// resource access.
			gameData.InvalidateCachedTradeNetwork();

			// If that was the capital, the palace moves to another city.
			RelocateCapitalIfLost(owner, gameData.Buildings.Find(x => x.isCenterOfEmpire));

			// Redo corruption calculations after a city is destroyed, since it
			// may change rank corruption values.
			owner.DoCorruptionCalculations(gameData);
		}

		// How strongly a city claims to be the new capital when the old one is
		// lost, using Civ3's formula from
		// https://civfanatics.com/civ3/strategy/game-mechanics/free-palace-jump/:
		// 3 points per citizen of the owner's nationality, 1 per foreign citizen,
		// 1 per military unit in the city, and 1/2/3 points per neighboring
		// town/city/metropolis of the owner's within the rules'
		// CapitalNeighborDistance.
		public static int CapitalScore(Player owner, City city) {
			int score = 0;
			foreach (CityResident resident in city.residents) {
				score += resident.nationality == owner.civilization ? 3 : 1;
			}
			score += city.location.unitsOnTile.Count(u => u.owner == owner && u.IsCombatUnit());
			foreach (City neighbor in owner.cities) {
				if (neighbor != city && city.location.DistanceTo(neighbor.location) <= owner.rules.CapitalNeighborDistance) {
					score += neighbor.SizeLevel();
				}
			}
			return score;
		}

		// The city that becomes the capital when the old one is lost: the one
		// with the highest CapitalScore. Ties go to the city that comes first in
		// the owner's list of cities, which is founding order (as in Civ3, where
		// ties go to the first city in the database, usually the oldest). Null if
		// no cities are left.
		public static City ChooseNewCapital(Player owner) {
			City best = null;
			int bestScore = int.MinValue;
			foreach (City city in owner.cities) {
				int score = CapitalScore(owner, city);
				if (score > bestScore) {
					best = city;
					bestScore = score;
				}
			}
			return best;
		}

		// Moves the capital, and the palace, to the best remaining city when the
		// owner no longer has one (e.g. it was destroyed or abandoned). Any
		// Forbidden Palace in the new capital stays: its effects stack with the
		// capital's. Returns the new capital, or null if there was nothing to do.
		public static City RelocateCapitalIfLost(Player owner, Building palace) {
			if (owner.cities.Count == 0 || owner.cities.Any(c => c.IsCapital())) {
				return null;
			}
			City newCapital = ChooseNewCapital(owner);
			newCapital.capital = true;
			if (palace != null && !newCapital.constructed_buildings.Any(b => b.building == palace)) {
				newCapital.AddBuilding(palace);
			}
			log.Information($"{owner.civilization?.name} lost its capital; the palace moves to {newCapital.name}");
			return newCapital;
		}
	}
}
