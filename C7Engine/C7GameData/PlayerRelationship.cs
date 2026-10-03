using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using Serilog;

namespace C7GameData;

// A class holding all the state of the relationship between two civs.
// If a relationship between 2 active civs doesn't exist it means that they haven't met yet.
// If a relationship exists but doesn't have any multi-turn deals on either side,
// it means that these 2 civs are at war, because Peace itself is a multi-turn deal.
// A civ can't (and shouldn't) have a relationship with themselves, barbarians or defeated players.
//
// Another important detail is that, a PlayerRelationship is a two part relationship,
// or a two-way relationship if it's easier to think about it like that.
// Therefore, a multiturn deal exists in both player's relationship info.
// If a deal is removed from player's 1 multiTurnDeals list, it still very much exists in player's 2 list,
// unless explicitly removed.
public class PlayerRelationship {
	private static ILogger log = Log.ForContext<PlayerRelationship>();

	// p1.playerRelationships[p2].warDeclarationCount is the number of times
	// p2 declared war on p1.
	// TODO: contribute towards reputation
	public int warDeclarationCount = 0;

	// p1.playerRelationships[p2].warDeclarationWithRoPActiveCount is the number of times
	// p2 declared war on p1 while having an active RoP.
	// TODO: contribute towards reputation
	public int warDeclarationWithRoPActiveCount = 0;

	// true if a war declaration happened with units inside the player's
	// borders.
	public bool wasSneakAttacked = false;

	// If at war, refuse contact with the relevant player until this turn
	// has been reached.
	public int refuseContactUntilTurn = -1;

	public List<MultiTurnDeal> multiTurnDeals = new List<MultiTurnDeal>();

	public bool declaredWarWithActiveRightOfPassage = false;

	public bool AtWar() {
		return multiTurnDeals.Count == 0;
	}

	/// <summary>
	/// Returns <b>true</b>, and the left's player relationship to the right player, if it exists.<br/>
	/// Otherwise returns <b>false</b>.<br/>
	/// If you want the opposite relationship, flip the arguments when calling the method.
	/// </summary>
	/// <param name="left"></param>
	/// <param name="right"></param>
	/// <param name="relationship"></param>
	/// <returns></returns>
	public static bool TryGetRelationship(Player left, Player right, out PlayerRelationship relationship) {
		relationship = null;

		if (left == null || right == null) return false;
		if (left.id == right.id) return false;
		if (left.isBarbarians || right.isBarbarians) return false;
		if (left.defeated || right.defeated) return false;
		if (!left.playerRelationships.TryGetValue(right.id, out var pr)) return false;

		relationship = pr;
		return true;
	}

	public static bool AtWar(Player left, Player right) {
		// only one is barbarians, but not both
		if (left.isBarbarians != right.isBarbarians) return true;

		if (TryGetRelationship(left, right, out var relationship) && relationship.AtWar()) {
			return true;
		}

		return false;
	}

	public static bool AtPeace(Player left, Player right) {
		return !AtWar(left, right);
	}

	/// <summary>
	/// Returns true if the player is at war with any of the other players/AI except the barbarians.
	/// </summary>
	/// <param name="player"></param>
	/// <param name="players"></param>
	/// <returns></returns>
	public static bool IsInAnyWar(Player player, List<Player> players) {
		if (players.Any(other => !other.isBarbarians && AtWar(player, other))) {
			return true;
		}

		return false;
	}

	public static bool HaveActiveRightOfPassage(Player left, Player right) {
		return TryGetRelationship(left, right, out var pr) &&
			   pr.multiTurnDeals.Any(d => d.dealSubType == DealSubType.RightOfPassage);
	}

	// Breaks peace and all other multiturn deals when war is declared 
	public static void DeclareWar(Player aggressor, Player defender, bool sneakAttack, int currentTurn, int refuseContactUntilTurn) {
		var defenderRelationshipToAggressor = defender.playerRelationships[aggressor.id];
		var aggressorRelationshipToDefender = aggressor.playerRelationships[defender.id];
		// increment the times the aggressor has declared war on the defender
		defenderRelationshipToAggressor.warDeclarationCount++;

		// increment the times the aggressor has declared war on the defender while there is an active RoP
		if (HaveActiveRightOfPassage(aggressor, defender)) {
			defenderRelationshipToAggressor.warDeclarationWithRoPActiveCount++;
		}

		// update whether the defender was sneak attacked
		defenderRelationshipToAggressor.wasSneakAttacked = sneakAttack;

		// Set for how many turns the defender will refuse contact from the aggressor
		defenderRelationshipToAggressor.refuseContactUntilTurn = refuseContactUntilTurn;

		// TODO: Figure out a better formula to calculate the aggressor's refusal in turns.
		// Right now it's hardcoded to half of what the defender's value is.
		//
		// The thinking is that if AI attacks a human, the human as a defender
		// might try to talk to the AI aggressor earlier than an AI in it's place is programmed to do.
		aggressorRelationshipToDefender.refuseContactUntilTurn = refuseContactUntilTurn / 2;

		// Finally clear all multi-turn deals, including Peace, which is how we actually declare war
		aggressorRelationshipToDefender.multiTurnDeals = new List<MultiTurnDeal>();
		defenderRelationshipToAggressor.multiTurnDeals = new List<MultiTurnDeal>();

		log.Information($"{aggressor} declared war on {defender}{(sneakAttack ? $" in a sneak attack" : "")}!" +
						$" Defender is refusing contact for at least up to turn {refuseContactUntilTurn}" +
						$" ({refuseContactUntilTurn - currentTurn} turns)!");
	}

	public static void SignPeaceAfterWar(Player left, Player right, GameData gameData) {
		if (left.isBarbarians || left.defeated || right.isBarbarians || right.defeated || left.id == right.id)
			throw new Exception($"Can't sign peace between {left} and {right}");

		if (!AtWar(left, right))
			throw new Exception($"This is not the proper method to use if the two players are not at war");

		MultiTurnDeal mtd = new MultiTurnDeal(DealType.DiplomaticAgreement, DealSubType.Peace, DealDetails.Exchange,
			0, null, gameData.rules.DefaultDealDuration, gameData.turn, null);

		RegisterMultiTurnDeal(left, right, mtd);

		left.playerRelationships[right.id].refuseContactUntilTurn = -1;
		right.playerRelationships[left.id].refuseContactUntilTurn = -1;

		log.Information($"{left} signed a peace treaty with {right}");
	}

	public static void RegisterMultiTurnDeal(Player left, Player right, MultiTurnDeal mtd) {
		if (mtd == null || mtd.dealDetails == DealDetails.None)
			throw new Exception("Not a valid deal");

		if (mtd.dealDetails == DealDetails.Exchange) {
			RegisterTwoWayDeal(left, right, mtd);
			return;
		}
		RegisterOneWayDeal(left, right, mtd);
	}

	private static void RegisterOneWayDeal(Player left, Player right, MultiTurnDeal mtd) {
		if (mtd.dealDetails == DealDetails.None || mtd.dealDetails == DealDetails.Exchange)
			throw new Exception("This is not a valid one way deal. Perhaps you intended to use RegisterTwoWayDeal() instead.");

		// add the deal for this player
		left.playerRelationships[right.id].multiTurnDeals.Add(mtd);

		// add the deal for the other player
		right.playerRelationships[left.id].multiTurnDeals.Add(new MultiTurnDeal(mtd.dealType, mtd.dealSubType,
			mtd.dealDetails == DealDetails.Inbound ? DealDetails.Outbound : DealDetails.Inbound,
			mtd.goldPerTurn, mtd.resourcePerTurn, mtd.dealDuration, mtd.goldPerTurn, mtd.againstPlayer));
	}

	private static void RegisterTwoWayDeal(Player left, Player right, MultiTurnDeal mtd) {
		if (mtd.dealDetails != DealDetails.Exchange)
			throw new Exception("This is not a valid two way deal. Perhaps you intended to use RegisterOneWayDeal() instead.");

		// add the deal for this player
		left.playerRelationships[right.id].multiTurnDeals.Add(mtd);

		// add the deal for the other player
		right.playerRelationships[left.id].multiTurnDeals.Add(mtd);
	}

	/// <summary>
	/// This ends all multi-turn deals except peace, when they go over the initial agreed upon duration.<br/>
	/// The multi-turn deal is only cancelled for the Player and not the other party.
	/// </summary>
	/// <param name="player"></param>
	/// <param name="players"></param>
	/// <param name="currentTurn"></param>
	public static void CheckForObsoleteDeals(Player player, List<Player> players, int currentTurn) {
		log.Information($"Checking to terminate any deals past their due duration for player {player}");

		var playerIds = players.Select(x => x.id).ToList();

		// check player's relationship with the other players
		foreach (var playerId in playerIds) {
			Player other = players.First(p => p.id == playerId);
			// if the player doesn't have a relationship with the other civ, or they are at war exit
			if (TryGetRelationship(player, other, out var relationship) && !relationship.AtWar()) {
				// we don't want to cancel peace
				List<MultiTurnDeal> deadDeals = relationship.multiTurnDeals
					.Where(mtd => mtd != null
							  && mtd.dealSubType != DealSubType.Peace
							  && mtd.TurnsRemaining(currentTurn) <= 0)
					.Where(mtd => mtd.dealSubType != DealSubType.MutualProtectionPact
								   && !EngineStorage.gameData.AreInLockedPeace(player, other))
					.ToList();

				foreach (MultiTurnDeal deadDeal in deadDeals) {
					// TODO: Add a popup to notify if an AI/Human deal expires
					// TODO: Add renegotiate logic (plus preferences option Always Renegotiate Deals)
					log.Information($"Cancelling multi turn deal: {player} -- {other}");
					UnRegisterMultiTurnDeal(relationship, deadDeal);
				}
			}
		}
	}

	private static void UnRegisterMultiTurnDeal(PlayerRelationship relationship, MultiTurnDeal mtd) {
		relationship.multiTurnDeals.Remove(mtd);
	}
}

public class MultiTurnDeal {
	private const int DEFAULT_DEAL_DURATION = 20;
	public DealType dealType { get; private set; }
	public DealSubType dealSubType { get; private set; }
	public DealDetails dealDetails { get; private set; }
	public int goldPerTurn { get; private set; }
	public string resourcePerTurn { get; private set; }
	public int dealDuration { get; private set; }
	public int turnStartDeal { get; private set; }

	public int turnEndDeal { get; private set; }

	// only applicable for Military Alliances and Trade Embargoes
	public ID againstPlayer { get; private set; }

	public MultiTurnDeal(DealType dealType, DealSubType dealSubType, DealDetails dealDetails, int goldPerTurn = 0,
		string resourcePerTurn = null, int dealDuration = DEFAULT_DEAL_DURATION, int turnStartDeal = 0, ID againstPlayer = null) {
		this.dealSubType = dealSubType;
		this.dealType = dealType;
		this.dealDetails = dealDetails;
		this.goldPerTurn = goldPerTurn;
		this.resourcePerTurn = resourcePerTurn;
		this.dealDuration = dealDuration;
		this.turnStartDeal = turnStartDeal;
		// basically peace from the start, don't need to know when it ends
		if (dealSubType == DealSubType.Peace && turnStartDeal == 0)
			this.turnEndDeal = 0;
		else
			this.turnEndDeal = turnStartDeal + dealDuration;
		this.againstPlayer = againstPlayer;
	}

	public int TurnsRemaining(int currentTurn) {
		return turnEndDeal - currentTurn <= 0 ? 0 : turnEndDeal - currentTurn;
	}

	// A multi turn peace deal that is from the start of the game without end.
	// Used for when a civ meets another civ to establish the initial peace.
	public static MultiTurnDeal DEFAULT_PEACE => new MultiTurnDeal(DealType.DiplomaticAgreement, DealSubType.Peace,
			DealDetails.Exchange, 0, null, 0, 0, null);

	public static MultiTurnDeal DEFAULT_MUTUAL_PROTECTION_PACT => new MultiTurnDeal(DealType.DiplomaticAgreement, DealSubType.MutualProtectionPact,
			DealDetails.Exchange, 0, null, 0, 0, null);

	/// <summary>
	/// Returns the counterpart to a deal.<br/><br/>
	/// The simplest example would be, if PlayerA gives wines to PlayerB,
	/// PlayerA has an Outbound deal in their multiTurnDeals info,
	/// whereas PlayerB has an Inbound deal, while the rest is the same.<br/><br/>
	/// This method, given PlayerA, PlayerB and the PlayerA's side of the deal (Outbound),
	/// returns the PlayerB's side of the deal (Inbound).<br/><br/>
	/// We could have this also the other way around, and provide
	/// PlayerB, PlayerA and PlayerB's side of the deal (Inbound),
	/// and retrieve PlayerA's side of the deal (Outbound).
	/// </summary>
	/// <param name="playerA"></param>
	/// <param name="playerB"></param>
	/// <param name="original"></param>
	/// <returns></returns>
	public static MultiTurnDeal GetCounterpartDeal(Player playerA, Player playerB, MultiTurnDeal original) {
		MultiTurnDeal mtd = null;
		if (PlayerRelationship.TryGetRelationship(playerB, playerA, out var relationship)) {
			DealDetails oppositeDetails = DealDetails.None;
			if (original.dealDetails == DealDetails.Exchange) {
				oppositeDetails = DealDetails.Exchange;
			} else {
				if (original.dealDetails == DealDetails.Inbound) {
					oppositeDetails = DealDetails.Outbound;
				} else {
					oppositeDetails = DealDetails.Inbound;
				}
			}

			mtd = relationship.multiTurnDeals.FirstOrDefault(d => {
				return
					d.dealType == original.dealType
					&& d.dealSubType == original.dealSubType
					&& d.dealDetails == oppositeDetails
					&& d.goldPerTurn == original.goldPerTurn
					&& d.resourcePerTurn == original.resourcePerTurn
					&& d.dealDuration == original.dealDuration
					&& d.turnStartDeal == original.turnStartDeal
					&& d.againstPlayer == original.againstPlayer;
			});

		}
		return mtd;
	}
}

// https://github.com/maxpetul/C3X/blob/064c8307c5085205c0dc8f2ee5b61ad2c2606523/Civ3Conquests.h#L1399
public enum DealType {
	DiplomaticAgreement,
	Alliance,
	Embargo,
	Map,
	Communication,
	Resource,
	Luxury,
	Gold,
	Technology,
	City,
	Unit,
	None,
}

public enum DealSubType {
	Peace,
	MutualProtectionPact,
	RightOfPassage,
	MilitaryAlliance,
	TradeEmbargo,
	GoldPerTurn,
	ResourcePerTurn,
	LuxuryPerTurn,
	None,
}

public enum DealDetails {
	Inbound,
	Outbound,
	Exchange,
	None,
}
