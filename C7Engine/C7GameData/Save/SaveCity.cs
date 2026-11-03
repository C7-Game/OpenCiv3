using System.Collections.Generic;

namespace C7GameData.Save {
	public class SaveCityResident {
		public ID citizenType;
		public string nationality;
		public ID city;
		public TileLocation tileWorked;
	}

	public class SaveCityBuilding {
		public string building;
		public ID builtByPlayer;
		public int year;
		public int totalCulture;

		public SaveCityBuilding() { }

		public SaveCityBuilding(CityBuilding cityBuilding) {
			building = cityBuilding.building.name;
			builtByPlayer = cityBuilding.builtByPlayer.id;
			year = cityBuilding.year;
			totalCulture = cityBuilding.totalCulture;
		}

		public CityBuilding ToCityBuilding(List<Building> buildings, List<Player> players) {
			return new CityBuilding {
				building = buildings.Find(buildingType => buildingType.name == building),
				builtByPlayer = players.Find(player => player.id == builtByPlayer),
				year = year,
				totalCulture = totalCulture,
			};
		}

	}

	public enum ProducibleType { INFLOW, BUILDING, UNIT };

	public class SaveCity : IHasID {
		public ID id { get; set; }
		public ID owner;
		public bool capital;
		public TileLocation location;
		public string producible;
		public ProducibleType producibleType;
		public string name;
		public int size;
		public Dictionary<string, int> perPlayerCulture = new();
		public int shieldsStored;
		public int foodStored;
		public int turnsOfUnhappinessDueToPopRushing;
		public List<SaveCityResident> residents = new List<SaveCityResident>();
		public List<SaveCityBuilding> buildings = [];

		public SaveCity() { }

		public SaveCity(City city) {
			id = city.id;
			owner = city.owner.id;
			capital = city.capital;
			location = new TileLocation(city.location);
			name = city.name;
			producible = city.itemBeingProduced.name;
			producibleType = city.itemBeingProduced switch {
				Inflow => ProducibleType.INFLOW,
				UnitPrototype => ProducibleType.UNIT,
				Building => ProducibleType.BUILDING,
			};
			shieldsStored = city.shieldsStored;
			foodStored = city.foodStored;
			turnsOfUnhappinessDueToPopRushing = city.turnsOfUnhappinessDueToPopRushing;
			residents = city.residents.ConvertAll(resident => {
				return new SaveCityResident {
					nationality = resident.nationality?.name,
					city = resident.city.id,
					tileWorked = new TileLocation(resident.tileWorked),
					citizenType = resident.citizenType.Id,
				};
			});
			buildings = city.constructed_buildings.ConvertAll(building => new SaveCityBuilding(building));

			foreach (KeyValuePair<Player, int> keyValuePair in city.perPlayerCulture) {
				perPlayerCulture.Add(keyValuePair.Key.id.ToString(), keyValuePair.Value);
			}
		}

		public City ToCity(GameMap gameMap,
							List<Player> players,
							List<UnitPrototype> unitPrototypes,
							List<Civilization> civilizations,
							List<Building> buildings,
							List<CitizenType> citizenTypes,
							List<Inflow> inflows) {
			City city = new City{
				id = id,
				location = gameMap.tileAt(location.X, location.Y),
				owner = players.Find(p => p.id == owner),
				name = name,
				itemBeingProduced = producibleType switch {
					ProducibleType.INFLOW => inflows.Find(inflow => inflow.name == producible),
					ProducibleType.UNIT => unitPrototypes.Find(proto => proto.name == producible),
					ProducibleType.BUILDING => buildings.Find(building => building.name == producible),
				},
				foodStored = foodStored,
				turnsOfUnhappinessDueToPopRushing = turnsOfUnhappinessDueToPopRushing,
				capital = capital,
				constructed_buildings = this.buildings.ConvertAll(building => building.ToCityBuilding(buildings, players)),
			};

			city.SetStoredShields(shieldsStored);

			foreach (KeyValuePair<string, int> keyValuePair in perPlayerCulture) {
				city.perPlayerCulture.Add(players.Find(x => x.id.ToString() == keyValuePair.Key), keyValuePair.Value);
			}

			city.residents = residents.ConvertAll(resident => {
				return new CityResident {
					citizenType = citizenTypes.Find(x => x.Id == resident.citizenType),
					// Cities founded before founding citizens got a nationality saved none.
					nationality = civilizations.Find(civ => civ.name == resident.nationality) ?? city.owner.civilization,
					tileWorked = gameMap.tileAt(resident.tileWorked.X, resident.tileWorked.Y),
					city = city,
				};
			});

			// Fill in the back pointers.
			foreach (CityResident cr in city.residents) {
				cr.tileWorked.personWorkingTile = cr;
			}

			// Scenarios don't specify the citizens of each city, only the city
			// size. So we need to add the appropriate residents here.
			//
			// We wait to assign them to tiles until after tile ownership has
			// been established in SaveGame.cs.
			if (city.residents.Count == 0) {
				CitizenType ct = citizenTypes.Find(x => x.IsDefaultCitizen);
				for (int i = 0; i < size; ++i) {
					CityResident newResident = new() {
						citizenType = ct,
						nationality = city.owner.civilization,
						city = city
					};
					city.residents.Add(newResident);
				}
			}

			return city;
		}
	}
}
