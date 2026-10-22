using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace C7GameData.Save {

	public class SavePlayer {
		public ID id;
		public int primaryColorIndex;
		public int secondaryColorIndex;
		public bool human = false;
		public bool hasPlayedCurrentTurn = false;
		public bool defeated = false;
		public bool isIncludedInGame = true;
		public bool canBePicked = true;
		public bool skipFirstTurn = false;

		public string civilization;

		public List<TileLocation> tileKnowledge = new List<TileLocation>();

		// A map from player id to the relationship this player has with the other player.
		public Dictionary<string, PlayerRelationship> playerRelationships = new();

		// The list of techs known by this player.
		public HashSet<ID> knownTechs = new();

		// The tech the player is currently researching.
		public ID currentlyResearchedTech;

		// The tech queue the player is currently researching.
		public List<ID> researchQueue = new();

		// How many free tech advances the player still has to spend.
		public int freeTechsRemaining;

		// The civilopedia name of the era this player is in.
		//
		// The civilopedia name is what is used for art lookups, not the actual
		// name.
		public string eraCivilopediaName;

		public int turnsUntilPriorityReevaluation = 0;

		// The values of the science/happiness/tax sliders (tax is implicit)
		// A value of 1 => 10%, a value of 10 => 100%.
		//
		// INVARIANT: LuxuryRate + ScienceRate + TaxRate = 10
		public int luxuryRate = 0;
		public int scienceRate = 5;
		public int taxRate = 5;

		// The amount of gold this player has.
		public int gold = 0;

		// The number of "beakers" (gold) spent on the currently researched
		// tech.
		public int beakers = 0;

		// The number of turns the player has been researching the current tech.
		public int turnsResearched = 0;

		// If the government is anarchy (or a govt with the transition bool set
		// to true), the turn number at which switching governments is allowed.
		public int inAnarchyUntilTurn = 0;

		// The current government of the player.
		public ID governmentId;

		public string alliance;

		// Used when importing from .biq, to make it easier to distinguish barbarians from other players.
		// It's not meant to be saved in the json.
		[JsonIgnore]
		public bool isBarbarian { get; init; }

		public Player ToPlayer(GameMap map, List<Civilization> civilizations, List<Government> governments, List<Tech> techs, Rules rules, HashSet<Alliance> alliances) {
			Player player = new Player{
				id = id,
				isHuman = human,
				isIncludedInGame = isIncludedInGame,
				canBePicked = canBePicked,
				alliance = alliance is not null ? alliances.First(a => a.name == alliance) : null,
				hasPlayedThisTurn = hasPlayedCurrentTurn,
				skipFirstTurn = skipFirstTurn,
				defeated = defeated,
				primaryColorIndex = primaryColorIndex,
				secondaryColorIndex = secondaryColorIndex,
				civilization = civilization is not null ? civilizations.Find(civ => civ.name == civilization) : null,
				knownTechs = knownTechs,
				eraCivilopediaName = eraCivilopediaName,
				luxuryRate = luxuryRate,
				scienceRate = scienceRate,
				taxRate = taxRate,
				gold = gold,
				turnsUntilPriorityReevaluation = turnsUntilPriorityReevaluation,
				inAnarchyUntilTurn = inAnarchyUntilTurn,
				government = governments.Find(x => x.id == governmentId),
				rules = rules,
			};
			foreach (TileLocation tile in tileKnowledge) {
				player.tileKnowledge.AddTileToKnown(map.tileAt(tile.X, tile.Y));
			}
			foreach (ID techId in player.civilization.startingTechs) {
				if (!player.knownTechs.Contains(techId)) {
					player.knownTechs.Add(techId);
				}
			}
			foreach (KeyValuePair<string, PlayerRelationship> keyValuePair in this.playerRelationships) {
				player.playerRelationships.Add(ID.FromString(keyValuePair.Key), keyValuePair.Value);
			}

			// Because of the custom setter we need to set the researched tech
			// and then set the beakers and turns researched - otherwise they'd
			// be reset by the setter.
			player.SetCurrentlyResearchedTech(currentlyResearchedTech);
			player.beakers = beakers;
			player.turnsResearched = turnsResearched;

			foreach (ID techId in researchQueue) {
				Tech tech = techs.Find(x => x.id == techId);
				player.AddTechItemToResearchQueue(tech);
			}

			player.freeTechsRemaining = freeTechsRemaining;

			if (player.civilization.isBarbarian) {
				player.canBePicked = false;
			}

			return player;
		}

		public SavePlayer() { }

		public SavePlayer(Player player) {
			id = player.id;
			isIncludedInGame = player.isIncludedInGame;
			canBePicked = player.canBePicked;
			alliance = (player.isBarbarians || player.alliance == null) ? null : player.alliance.name;
			primaryColorIndex = player.primaryColorIndex;
			secondaryColorIndex = player.secondaryColorIndex;
			human = player.isHuman;
			hasPlayedCurrentTurn = player.hasPlayedThisTurn;
			defeated = player.defeated;
			civilization = player.civilization?.name;
			// TODO: this should be computed by looking at cities defined in the save
			// so that adding cities in the save structure doesn't require updating this value
			tileKnowledge = player.tileKnowledge.AllKnownTiles().ConvertAll(tile => new TileLocation(tile));
			turnsUntilPriorityReevaluation = player.turnsUntilPriorityReevaluation;
			knownTechs = player.knownTechs;
			currentlyResearchedTech = player.currentlyResearchedTech;
			researchQueue = new List<ID>(player.ResearchQueue.Select(t => t.id));
			freeTechsRemaining = player.freeTechsRemaining;
			eraCivilopediaName = player.eraCivilopediaName;
			luxuryRate = player.luxuryRate;
			scienceRate = player.scienceRate;
			taxRate = player.taxRate;
			gold = player.gold;
			beakers = player.beakers;
			turnsResearched = player.turnsResearched;
			inAnarchyUntilTurn = player.inAnarchyUntilTurn;
			governmentId = player.government.id;

			foreach (KeyValuePair<ID, PlayerRelationship> keyValuePair in player.playerRelationships) {
				playerRelationships.Add(keyValuePair.Key.ToString(), keyValuePair.Value);
			}
		}

		public override string ToString() {
			if (civilization != null)
				return $"{civilization} [{this.id}]";
			return "";
		}
	}
}
