using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using C7Engine.Pathing;
using C7GameData;
using C7GameData.AIData;
using C7Engine.AI;
using C7Engine.AI.StrategicAI;
using C7Engine.AI.UnitAI;
using Serilog;
using System.Diagnostics;
using static C7GameData.PlayerRelationship;

namespace C7Engine {
	public class PlayerAI {
		private static ILogger log = Log.ForContext<PlayerAI>();

		public static readonly int MAX_LAND_EXPLORERS = 10;
		public static readonly int MAX_WATER_EXPLORERS = 4;

		public static async Task PlayTurn(Player player, GameData gameData) {
			if (player.isHuman || player.isBarbarians || !player.isIncludedInGame) {
				return;
			}
			List<Tech> techs = gameData.techs;

			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();
			log.Information("-> Begin " + player.civilization.cityNames[0] + " turn");

			// TODO: Before we call this method to automatically end obsolete deals, we could make this more versatile.
			// For example unless we have a good reason, as a human, receiving luxuries, gpt,
			// or having an active RoP, doesn't hurt us.
			PlayerRelationship.CheckForObsoleteDeals(player, EngineStorage.gameData.players, EngineStorage.gameData.turn);

			MaybeDoPriorityReevaluation(player);
			MaybePickTechToResearch(player, techs);

			// Roughly every 4 turns, see if there are trades to be made.
			if (GameData.rng.Next(100) < 25) {
				await AttemptTrading(player);
			}

			await DoUnitActions(player);

			// Before ending the turn, adjust our sliders. We do this after unit
			// moves so that any worker moves that finished during the unit moves
			// will be usable (like if a luxury got hooked up).
			AdjustSliders(player);

			log.Information("-> End " + player.civilization.cityNames[0] + $" turn {stopwatch.ElapsedMilliseconds} milliseconds");
		}

		private static void MaybeDoPriorityReevaluation(Player player) {
			if (player.turnsUntilPriorityReevaluation == 0) {
				log.Information("Re-evaluating strategic priorities for " + player);
				List<StrategicPriority> priorities = StrategicPriorityArbitrator.Arbitrate(player);
				player.strategicPriorityData.Clear();
				foreach (StrategicPriority priority in priorities) {
					player.strategicPriorityData.Add(priority);
				}
				player.turnsUntilPriorityReevaluation = 15 + GameData.rng.Next(10);

				string summary = string.Join(',', player.strategicPriorityData);
				log.Information($"Player {player.civilization.name} now has priorities of: {summary}. {player.turnsUntilPriorityReevaluation} turns until next re-evaluation");

				// Wake up any fortified units so our new strategy (if we have one)
				// takes effect.
				foreach (MapUnit u in player.units) {
					u.Wake();
				}
			} else {
				player.turnsUntilPriorityReevaluation--;
			}
		}

		public static void MaybePickTechToResearch(Player player, List<Tech> techs) {
			// AI players get no prompt, so they spend any free advances immediately.
			while (!player.isHuman && player.freeTechsRemaining > 0) {
				Tech freeTech = player.GetAvailableTechsToResearch(techs).FirstOrDefault();
				if (freeTech == null || !player.ClaimFreeTech(EngineStorage.gameData, freeTech)) {
					break;
				}
			}

			while (player.currentlyResearchedTech == null || player.knownTechs.Contains(player.currentlyResearchedTech)) {
				Tech toResearch = player.GetAvailableTechsToResearch(techs).FirstOrDefault();
				if (toResearch == null) {
					log.Information($"Player {player.civilization.name} has no techs available to research.");
					player.SetCurrentlyResearchedTech(null);
					break;
				}

				if (player.ResearchQueue.Count <= 0) {
					player.AddTechItemToResearchQueue(toResearch);
				}
				player.SetCurrentlyResearchedTech(player.ResearchQueue.Peek().id);
				log.Information($"Player {player.civilization.name} is researching {player.ResearchQueue.Peek().Name}.");
			}
		}

		private static async Task DoUnitActions(Player player) {
			// Reorder the list so that settlers are second to last, giving our escorts a
			// chance to configure themselves without wasting a turn.
			// Finally reorder so that the workers are last, so in case a settler builds a new city,
			// they move to terraform a tile instead of wasting a turn (especially in the very first round in a new game).
			player.units.OrderBy(x => x.unitType.isSettler).ThenBy(x => x.unitType.isWorker);

			// Any time we have an unescorted settler, wake up any other units
			// on the same tile to force us to re-evaluate whether they should
			// be an escort. Otherwise, can have perfectly fine escorts sitting
			// there fortified.
			foreach (MapUnit u in player.units) {
				if (u.currentAI is SettlerAI settlerAi && settlerAi.data.escort == null) {
					foreach (MapUnit uu in u.location.unitsOnTile) {
						uu.Wake();
					}
				}
			}

			// Do things with units. Copy into an array first to avoid collection-was-modified exception
			foreach (MapUnit unit in player.units.ToArray()) {
				// Don't waste time recalculating behaviors for fortified units.
				// This means we'll have to unfortify all our units after
				// interesting events like war declarations, but this seems like
				// a good tradeoff for faster turns.
				if (unit.isFortified) {
					continue;
				}

				// For each unit, if there's already an AI task assigned, it will attempt to complete its goal.
				// It may fail due to conditions having changed since that goal was assigned; in that case it will
				// get a new task to try to complete.
				//
				// Cap our attempts at 2 to avoid getting stuck in bad situations.
				for (int attempt = 0; attempt < 2; ++attempt) {
					if (unit.currentAI == null) {
						unit.currentAI = GetAIForUnit(unit, player);
					}

					// If the unit is still the process of doing its plan, allow
					// it to continue next turn.
					UnitAI.Result result = await unit.currentAI.PlayTurn(player, unit);
					if (result == UnitAI.Result.InProgress) {
						break;
					}

					if (result == UnitAI.Result.Error) {
						unit.currentAI = null;
						break;
					}

					if (unit.hitPointsRemaining <= 0 || unit.isFortified) {
						unit.currentAI = null;
						break;
					}

					// Otherwise we need a new plan for next turn. Pick it now
					// to avoid things like new units being preferred for
					// exploration instead of units already far away from home
					// for exploration.
					unit.currentAI = GetAIForUnit(unit, player);
				}

				player.tileKnowledge.AddTilesToKnown(unit.location);
			}
		}

		public static UnitAI GetAIForUnit(MapUnit unit, Player player) {
			//figure out an AI behavior
			//TODO: Use strategies, not names
			if (unit.unitType.name == "Settler") {
				return new SettlerAI(SettlerAI.MakeAiData(unit, player));
			} else if (unit.unitType.name == "Worker") {
				return new WorkerAI(WorkerAI.MakeAiData(unit, player));
			} else if (unit.location.cityAtTile != null && unit.CanDefendOnLand() && unit.location.unitsOnTile.Count(u => u.CanDefendOnLand() && u != unit) == 0) {
				return new DefenderAI(DefenderAI.MakeAiDataForDefendInPlace(unit, player));
			} else if (GetCombatAIIfUnitCanAttackNearbyBarbCamp(unit, player) is UnitAI unitAI && unitAI != null) {
				return unitAI;
			} else if (unit.unitType.name == "Catapult") {
				//For now tell catapults to sit tight.  It's getting really annoying watching them pointlessly bombard barb camps forever
				return new DefenderAI(DefenderAI.MakeAiDataForDefendInPlace(unit, player));
			}

			// Special case: we're at war.
			if (IsInAnyWar(player, EngineStorage.gameData.players)) {
				// Priority 1: ensure we don't have any unguarded cities.
				//
				// If this is an offensive unit only go defend if there are
				// fewer than 3 units in a city, otherwise consider offensive
				// action.
				int minDefenders = unit.unitType.attack >= unit.unitType.defense ? 3 : int.MaxValue;
				DefenderAIData maybeDefend = DefenderAI.MakeAiDataForDefendAtRiskCity(unit, player, minDefenders);
				if (maybeDefend != null) {
					return new DefenderAI(maybeDefend);
				}

				// Priority 2: go on the offensive.
				CombatAIData maybeCombat = CombatAI.MakeAiData(unit, player);
				if (maybeCombat != null) {
					return new CombatAI(maybeCombat);
				}

				// Priority 3: defend our cities.
				return new DefenderAI(DefenderAI.MakeAiDataForDefendAtRiskCity(unit, player, minDefenders: int.MaxValue));
			}

			// If there's an unescorted settler, escort it.
			EscortAIData? maybeEscortData = EscortAI.MaybeMakeAiData(unit, player);
			if (maybeEscortData != null) {
				return new EscortAI(maybeEscortData);
			}

			// As long as we don't have too many explorers yet of this unit's
			// type (land vs sea), start a new exploring unit.
			int numRelevantExplorers = 0;
			int maxExplorers = unit.IsLandUnit() ? MAX_LAND_EXPLORERS : MAX_WATER_EXPLORERS;
			foreach (MapUnit u in player.units) {
				if (u.currentAI is ExplorerAI && u.IsLandUnit() == unit.IsLandUnit()) {
					++numRelevantExplorers;
				}
			}

			if (numRelevantExplorers < maxExplorers) {
				ExplorerAIData? maybeAiData = ExplorerAI.MaybeMakeAiData(unit, player);
				if (maybeAiData != null) {
					return new ExplorerAI(maybeAiData);
				}
			}

			//Nowhere to explore or too many explorers.  What to do now?
			//Priority 1: Adequate defense of cities.
			//Priority 2: Clearing out barbs
			//Priority 3: Defending chokepoints
			//Priority 4: ???
			//Priority 5: Profit!
			//(Realistically, as we evolve there will be a lot of options, such as defending borders from barbs, preparing attackers on other civs, defending
			//resources.  I expect we'll have some sort of arbiter that decides between competing priorities, with each being given a score as to how important
			//they are, including a weight by how far away the task is.  But this will evolve gradually over a long time)

			//As of today (4/7/2022), let's tackle just one of those - adequate defense of cities.  The AI is really good at losing cities to barbs right now,
			//and that's a problem.
			return new DefenderAI(DefenderAI.MakeAiDataForDefendAtRiskCity(unit, player, minDefenders: int.MaxValue));
		}

		private static UnitAI GetCombatAIIfUnitCanAttackNearbyBarbCamp(MapUnit unit, Player player) {
			if (unit.unitType.attack <= 0) {
				return null;
			}

			List<Tile> reachableBarbCampsTiles = player.tileKnowledge.AllKnownTiles()
				.Where(t => unit.CanEnter(t) && t.hasBarbarianCamp).ToList();

			Tile closestBarbCamp = Tile.NONE;
			int closestBarbDistance = int.MaxValue;
			foreach (Tile t in reachableBarbCampsTiles) {
				int crowDistance = t.DistanceTo(unit.location);
				if (crowDistance < closestBarbDistance) {
					closestBarbCamp = t;
					closestBarbDistance = crowDistance;
				}
			}

			if (closestBarbDistance <= 3) {
				CombatAIData caid = new CombatAIData();
				caid.destination = closestBarbCamp;

				PathingAlgorithm algorithm = PathingAlgorithmChooser.GetAlgorithm(unit);
				caid.path = algorithm.PathFrom(unit.location, closestBarbCamp, unit);
				log.Information($"Set unit {unit} to take out barb camp at {closestBarbCamp}");
				return new CombatAI(caid);
			}
			return null;
		}

		private static async Task AttemptTrading(Player us) {
			GameData gD = EngineStorage.gameData;

			log.Information($"{us} is checking for trading opportunities");
			foreach (Player them in EngineStorage.gameData.players) {
				// We can't trade with players we don't know or players we're at
				// war with.
				if (!us.playerRelationships.ContainsKey(them.id) || AtWar(us, them)) {
					continue;
				}

				// Barbarians can't trade.
				if (them.isBarbarians || us.isBarbarians) {
					continue;
				}

				// Figure out what techs are available for trading.
				List<Tech> techsTheyCanTrade = gD.techs.FindAll(x => {
					return them.knownTechs.Contains(x.id) && !us.knownTechs.Contains(x.id);
				});
				techsTheyCanTrade.Sort((a, b) => { return gD.TechCostFor(b, us).CompareTo(gD.TechCostFor(a, us)); });

				List<Tech> techsWeCanTrade = gD.techs.FindAll(x => {
					return us.knownTechs.Contains(x.id) && !them.knownTechs.Contains(x.id);
				});
				techsWeCanTrade.Sort((a, b) => { return gD.TechCostFor(b, them).CompareTo(gD.TechCostFor(a, them)); });

				// If we can't trade techs there's no point in continuing - we
				// can't yet trade anything else interesting.
				if (techsWeCanTrade.Count == 0 && techsTheyCanTrade.Count == 0) {
					continue;
				}

				TradeOffer weGive = new();
				Func<int> CalculateWeGiveValue = () => {
					return Math.Max(weGive.GoldEquivalentFor(gD, them), weGive.GoldEquivalentFor(gD, us));
				};
				TradeOffer weWant = new();
				Func<int> CalculateWeWantValue = () => {
					return Math.Min(weWant.GoldEquivalentFor(gD, them), weWant.GoldEquivalentFor(gD, us));
				};

				// Figure out the value of what we have available to trade.
				weGive.gold = us.gold;
				foreach (Tech t in techsWeCanTrade) {
					weGive.techs.Add(t);
				}
				int ourMaxPossibleOffer = CalculateWeGiveValue();

				// Going from the most to the least valuable valuable techs, see
				// if we can afford them. This greedy algorithm should be good
				// enough - we don't need perfect binpacking.
				foreach (Tech t in techsTheyCanTrade) {
					int cost = gD.TechCostFor(t, us);
					if (cost < ourMaxPossibleOffer) {
						weWant.techs.Add(t);
						ourMaxPossibleOffer -= cost;
					}
				}

				// Also ask for any gold we can get.
				weWant.gold = Math.Min(ourMaxPossibleOffer, them.gold);

				// At this point we are getting as much as we possibly can get
				// from the opponent. However, we might be overpaying, possibly
				// by a significant amount. Keep removing techs from our offer
				// as long as it doesn't make our offer worse than theirs.
				int theirOfferValue = CalculateWeWantValue();
				for (int i = 0; i < weGive.techs.Count;) {
					if (CalculateWeGiveValue() - gD.TechCostFor(weGive.techs[i], them) >= theirOfferValue) {
						weGive.techs.RemoveAt(0);
					} else {
						++i;
					}
				}

				// Now use any gold to even things out, if possible.
				int remainingDelta = Math.Max(0, CalculateWeGiveValue() - theirOfferValue);
				weGive.gold -= Math.Min(remainingDelta, weGive.gold.Value);

				// And ensure we minimize the total gold traded, to keep the
				// logs cleaner.
				int redundantGold = Math.Min(weGive.gold.Value, weWant.gold.Value);
				weGive.gold -= redundantGold;
				weWant.gold -= redundantGold;
				if (weGive.gold == 0) {
					weGive.gold = null;
				}
				if (weWant.gold == 0) {
					weWant.gold = null;
				}

				// Finally if the deal is too mismatched or only contains a swap
				// of gold, abandon it. Otherwise we can execute the deal.

				float tradeFactor = them.isHuman ? 1.0f : gD.gameDifficulty.AIToAITradeRate / 100.0f;
				if (CalculateWeGiveValue() > tradeFactor * CalculateWeWantValue()) {
					continue;
				}
				if (weGive.techs.Count == 0 && weWant.techs.Count == 0) {
					continue;
				}

				if (them.isHuman) {
					new MsgShowTradeOffer(us, them, weWant, weGive).send();
					await EngineStorage.WaitForMessageToEngine<MsgDiplomacyCompleted>();
				} else {
					us.ExecuteDeal(gD, them, weWant, weGive);
				}
			}
		}

		private static void AdjustSliders(Player player) {
			const int MAX_SLIDER_VALUE = 10;
			const int MAX_AI_LUXURY_SLIDER = 5;

			// Start by zeroing out the sliders.
			player.luxuryRate = 0;
			player.scienceRate = 0;
			player.taxRate = 0;

			// Increase the luxury slider until only a small handful of cities
			// are unhappy (sometimes making all cities happy with the luxury
			// slider is too expensive and it's easier to just use entertainers
			// there).
			while (MostCitiesUnhappy(player) && player.luxuryRate < MAX_AI_LUXURY_SLIDER) {
				++player.luxuryRate;
			}

			// Fix up any remaining unhappy cities.
			FixRemainingUnhappyCities(player);

			// Now max out the science slider and then decrease it (increasing
			// the tax rate) until we're not losing money.
			player.scienceRate = MAX_SLIDER_VALUE - player.luxuryRate;
			while (player.scienceRate > 0 && !BudgetIsTolerable(player)) {
				player.scienceRate--;
				player.taxRate++;
			}

			log.Information($"{player} slider values: Science: {player.scienceRate}, Luxury: {player.luxuryRate}, Tax: {player.taxRate}");
		}

		// Returns true if more than 10% of the player's cities are unhappy with
		// current settings.
		private static bool MostCitiesUnhappy(Player player) {
			int unhappyCities = 0;
			foreach (City city in player.cities) {
				City.Mood cityMood = city.RecalculateCitizenMoods(EngineStorage.gameData);
				if (cityMood == City.Mood.Unhappy) {
					++unhappyCities;
				}
			}
			return unhappyCities / (double)player.cities.Count > .1;
		}

		// In each city, reassign citizens, managing moods, to ensure that we
		// don't have any cities that will riot.
		private static void FixRemainingUnhappyCities(Player player) {
			foreach (City city in player.cities) {
				// TODO: This throws away existing nationalities, fix that.
				int numResidents = city.residents.Count;
				city.RemoveAllCitizens();

				for (int i = 0; i < numResidents; ++i) {
					CityResident newResident = new() {
						citizenType = EngineStorage.gameData.citizenTypes.Find(x => x.IsDefaultCitizen),
						nationality = city.owner.civilization,
						city = city
					};
					city.AddCitizen(newResident);
					CityTileAssignmentAI.AssignNewCitizenToTile(EngineStorage.gameData, newResident, manageMoods: true);
				}
			}
		}

		public static bool BudgetIsTolerable(Player player) {
			var gpt = player.CalculateGoldPerTurn();
			var tolerableDeficit = gpt > player.gold * -0.1;
			return gpt > 0 || tolerableDeficit;
		}
	}
}
