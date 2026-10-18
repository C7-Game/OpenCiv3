namespace C7Engine {
	using C7GameData;
	using System;

	public interface IMessageToUI {
		public void send();
	}

	public class MessageToUI : IMessageToUI {
		public void send() {
			EngineStorage.messagesToUI.Enqueue(this);
		}
	}

	public class AnimationMessage : IMessageToUI {
		internal Guid animationId = Guid.NewGuid();

		public void send() {
			EngineStorage.animationMessages.Enqueue(this);
		}

		public void markCompleted() {
			if (EngineStorage.pendingAnimations.TryGetValue(animationId, out var tcs)) {
				EngineStorage.pendingAnimations.Remove(animationId);
				tcs.TrySetResult(true);
			}
		}
	}

	public class MsgStartUnitAnimation : AnimationMessage {
		public ID unitID;
		public MapUnit.AnimatedAction action;
		public AnimationEnding ending;

		public MsgStartUnitAnimation(MapUnit unit, MapUnit.AnimatedAction action, AnimationEnding ending) {
			this.unitID = unit.id;
			this.action = action;
			this.ending = ending;
		}
	}

	public class MsgStartEffectAnimation : AnimationMessage {
		public int tileIndex;
		public AnimatedEffect effect;
		public AnimationEnding ending;

		public MsgStartEffectAnimation(Tile tile, AnimatedEffect effect, AnimationEnding ending) {
			this.tileIndex = EngineStorage.gameData.map.tileCoordsToIndex(tile.XCoordinate, tile.YCoordinate);
			this.effect = effect;
			this.ending = ending;
		}
	}

	public class MsgNewGame : MessageToUI {
		public ID controllerId;

		public MsgNewGame(ID controllerId) {
			this.controllerId = controllerId;
		}
	}
	public class MsgStartTurn : MessageToUI { }
	public class MsgGameMainMenu : MessageToUI { }

	public class MsgDiplomacyPopUp : MessageToUI { }

	public class MsgRefuseContact : MessageToUI {
		public ID opponentId;

		public MsgRefuseContact(ID opponentId) {
			this.opponentId = opponentId;
		}
	}

	public class MsgAlreadyInRevolution : MessageToUI {
		public ID controllerId;

		public MsgAlreadyInRevolution(ID controllerId) {
			this.controllerId = controllerId;
		}
	}

	public class MsgConfirmStartRevolution : MessageToUI {
		public ID controllerId;

		public MsgConfirmStartRevolution(ID controllerId) {
			this.controllerId = controllerId;
		}
	}
	public class MsgDescendIntoAnarchy : MessageToUI {
		public ID controllerId;
		public Government government;

		public MsgDescendIntoAnarchy(ID controllerId, Government government) {
			this.controllerId = controllerId;
			this.government = government;
		}
	}

	public class MsgScienceGuidance : MessageToUI { }
	public class MsgShowScienceAdvisor : MessageToUI { }

	public class MsgUpdateUiAfterDomesticChange : MessageToUI { }

	public class MsgWarDeclarationNotification : MessageToUI {
		public Player aggressor;
		public Player opponent;

		public MsgWarDeclarationNotification(Player aggressor, Player opponent) {
			this.aggressor = aggressor;
			this.opponent = opponent;
		}
	}

	public class MsgNameCity : MessageToUI {
		public ID controllerId;
		public string nextCityName;

		public MsgNameCity(ID controllerId, string nextCityName) {
			this.controllerId = controllerId;
			this.nextCityName = nextCityName;
		}
	}

	// Foreign
	public class MsgWarDeclarationConfirmation : MessageToUI {
		public ID aggressorId;
		public ID opponentId;
		public Action callback;

		public MsgWarDeclarationConfirmation(ID aggressorId, ID opponentId, Action callback) {
			this.aggressorId = aggressorId;
			this.opponentId = opponentId;
			this.callback = callback;
		}
	}

	// Military
	public class MsgDiplomacyWarDeclarationConfirmation(ID aggressorId, ID opponentId, Action callback)
		: MsgWarDeclarationConfirmation(aggressorId, opponentId, callback);

	public class MsgCityDestroyed : MessageToUI {
		public City city;

		public MsgCityDestroyed(City city) {
			this.city = city;
		}
	}

	public class MsgCityRaised : MessageToUI {
		public ID controllerId;
		public ID ownerId;
		public City city;
		public int goldTaken;
		public bool controllerWon;

		public MsgCityRaised(ID controllerId, ID ownerId, City city, int goldTaken, bool controllerWon) {
			this.controllerId = controllerId;
			this.ownerId = ownerId;
			this.city = city;
			this.goldTaken = goldTaken;
			this.controllerWon = controllerWon;
		}
	}

	public class MsgCityRansacked : MessageToUI {
		public ID controllerId;
		public City city;
		public string barbTribe;
		public int goldLiberated;

		public MsgCityRansacked(ID controllerId, City city, string barbTribe, int goldLiberated) {
			this.controllerId = controllerId;
			this.city = city;
			this.barbTribe = barbTribe;
			this.goldLiberated = goldLiberated;
		}
	}

	public class MsgCityRiotWarning : MessageToUI {
		public ID controllerId;
		public City city;

		public MsgCityRiotWarning(ID controllerId, City city) {
			this.controllerId = controllerId;
			this.city = city;
		}
	}

	public class MsgSelectGovernment : MessageToUI {
		public ID controllerId;

		public MsgSelectGovernment(ID controllerId) {
			this.controllerId = controllerId;
		}
	}

	public class MsgCivilizationDestroyed : MessageToUI {
		public ID controllerId;
		public Civilization civilization;

		public MsgCivilizationDestroyed(ID controllerId, Civilization civ) {
			this.controllerId = controllerId;
			this.civilization = civ;
		}
	}

	public class MsgCityCreated : MessageToUI {
		public City city;

		public MsgCityCreated(City city) {
			this.city = city;
		}
	}

	public class MsgDisplayHurryProductionPopup : MessageToUI {
		public City city;
		public City.HurryProductionDetails details;

		public MsgDisplayHurryProductionPopup(City c, City.HurryProductionDetails d) {
			city = c;
			details = d;
		}
	}

	public class MsgDisplayStopWorkerActionPopup : MessageToUI {
		public MapUnit worker;
		public Terraform workerJob;
		public float turnsLeft;

		public MsgDisplayStopWorkerActionPopup(MapUnit worker, Terraform workerJob, float turnsLeft) {
			this.worker = worker;
			this.workerJob = workerJob;
			this.turnsLeft = turnsLeft;
		}
	}

	public class MsgShowCityScreen : MessageToUI {
		public City city;

		public MsgShowCityScreen(City city) {
			this.city = city;
		}
	}

	public class MsgShowMilitaryAdvisorPopup : MessageToUI {
		public string message;
		public bool happy;
		public MsgShowMilitaryAdvisorPopup(string message, bool happy) {
			this.message = message;
			this.happy = happy;
		}
	}

	public class MsgShowTemporaryPopup : MessageToUI {
		public string message;
		public Tile location;

		public MsgShowTemporaryPopup(string message, Tile location) {
			this.message = message;
			this.location = location;
		}
	}

	public class MsgShowTradeOffer : MessageToUI {
		public Player aiPlayer;
		public Player humanPlayer;
		public TradeOffer aiWant;
		public TradeOffer aiGive;

		public MsgShowTradeOffer(Player aiPlayer, Player humanPlayer, TradeOffer aiWant, TradeOffer aiGive) {
			this.aiPlayer = aiPlayer;
			this.humanPlayer = humanPlayer;
			this.aiWant = aiWant;
			this.aiGive = aiGive;
		}
	}

	public class MsgUnitMoved : MessageToUI {
		public MapUnit Unit;
		public MsgUnitMoved(MapUnit unit) {
			this.Unit = unit;
		}
	}

	public class MsgTransportUnloaded : MessageToUI {
		public MapUnit Unit;
		public MsgTransportUnloaded(MapUnit unit) {
			this.Unit = unit;
		}
	}

	public class MsgDisplayAbandonCityPopup : MessageToUI {
		public City city;
		public MsgDisplayAbandonCityPopup(City city) {
			this.city = city;
		}
	}

	public class MsgDisbandUnitConfirmation : MessageToUI {
		public MapUnit mapUnit;
		public MsgDisbandUnitConfirmation(MapUnit mapUnit) {
			this.mapUnit = mapUnit;
		}
	}

	public class MsgReplaceTerrainImprovementConfirmation : MessageToUI {
		public TerrainImprovement terrainImprovement;
		public Terraform terraform;
		public MsgReplaceTerrainImprovementConfirmation(TerrainImprovement terrainImprovement, Terraform terraform) {
			this.terrainImprovement = terrainImprovement;
			this.terraform = terraform;
		}
	}

	public class MsgVictory : MessageToUI {
		public Player winner;
		public IVictory victory;

		public MsgVictory(Player winner, IVictory victory) {
			this.winner = winner;
			this.victory = victory;
		}
	}
}
