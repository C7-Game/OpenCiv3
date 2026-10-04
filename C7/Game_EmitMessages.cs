using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using C7.UIElements.Popups;
using C7Engine;
using C7GameData;
using Godot;
using static AdvisorHead;

public partial class Game {
	public void EmitDawnOfCivilizationSignal(MsgNewGame msg) {
		var message = "PLACEHOLDER MSG";
		var delimeter = "and";
		EngineStorage.ReadGameData(data => {
			var controller = data.players.First(p => p.id == msg.controllerId);
			var year = data.timeOptions.GetDisplayTime(data.turn);
			var knownTechs = controller.knownTechs;

			var sb = new StringBuilder();
			for (int i = 0; i < controller.civilization.traits.Count; i++) {
				var trait = controller.civilization.traits.ElementAt(i);
				if (i > 0) {
					sb.Append(delimeter).Append(" ");
				}
				sb.Append($"[url={trait}][color=Blue]");
				sb.Append(trait);
				sb.Append("[/color][/url]");
				sb.Append(" ");
			}

			var traits = sb.ToString();

			var sbt = new StringBuilder();
			// [url=whatever][color=Blue]wrap[/color][/url]
			for (int i = 0; i < knownTechs.Count; i++) {
				var tech = data.techs.First(t => t.id == knownTechs.ElementAt(i));
				if (i > 0) {
					sbt.Append(delimeter).Append(" ");
				}
				sbt.Append($"[url={tech.CivilopediaEntry}][color=Blue]");
				sbt.Append(tech.Name);
				sbt.Append("[/color][/url]");
				sbt.Append(" ");
			}

			var techs = sbt.ToString();

			// TODO: make this dynamic, it's in the civ data
			var king = "[b]Caesar[/b]";

			message = $"It is the year {year}. Your ancestors were nomads. " +
					  $"But over the generations your people have learned the secrets of [i]farming," +
					  $" road-building[/i], and [i]irrigation[/i], and they are ready to settle down." +
					  $"\n\n{king}, your people are {traits}and have recently mastered {techs}." +
					  $"\n\nThe people have vested [i]absolute power[/i] in you, trusting that you can build a Civilization to stand the test of time!";
		});

		EmitSignal(SignalName.InteractivePopUp, new ParameterWrapper<InteractablePopUp>(
			new InformationPopup(
				controller.id,
				"Dawn of Civilization",
				message,
				Advisor.None,
				Mood.None,
				630,
				margins: new Margins(top: 50)
			)
		));
	}

	public void EmitGameMainMenuSignal() {
		var options = new List<ButtonAction>();

		// TODO: add Map option. This opens a new pop-up with more options

		var loadGameBtn = new ButtonAction() {
			message = "Load Game",
			action = () => OnLoadGame(),
		};
		options.Add(loadGameBtn);

		// TODO: add New Game option. This seems to do exactly the same as Retire in the original game

		// TODO: add Preferences option

		var retireBtn = new ButtonAction() {
			message = "Retire",
			action = () => OnRetire(),
		};
		options.Add(retireBtn);

		var saveBtn = new ButtonAction() {
			message = "Save Game",
			action = () => OnSaveGame(),
		};
		options.Add(saveBtn);

		var quitBtn = new ButtonAction() {
			message = "Quit Game (ESC)",
			action = () => EmitQuitGameSignal(),
		};
		options.Add(quitBtn);

		EmitSignal(SignalName.InteractivePopUp, new ParameterWrapper<InteractablePopUp>(
			new OptionsPopUp(
				controller.id,
				"Main Menu",
				null,
				options,
				Advisor.None,
				Mood.None,
				hSize: 350,
				margins: new Margins(top: 100)
			)
		));
	}

	public void EmitDiplomacyPopUpSignal() {
		var options = new List<ButtonAction>();

		EngineStorage.ReadGameData(data => {
			var allPlayers = data.players;
			var buttons = new List<ButtonAction>();
			foreach (KeyValuePair<ID, PlayerRelationship> kvp in controller.playerRelationships) {
				string status = kvp.Value.AtWar() ? "War" : "Peace";

				var btn = new ButtonAction() {
					message = $"{allPlayers.Find(x => x.id == kvp.Key).civilization.noun} (at {status})",
					action = () => {
                        // EmitSignal(PopupOverlay.SignalName.HidePopup);
                        OnDiplomacySelected(new ParameterWrapper<ID>(kvp.Key));
                        // EmitSignal(PopupOverlay.SignalName.DiplomacySelection, new ParameterWrapper<ID>(kvp.Key));
                    }
				};
				buttons.Add(btn);
			}
			options = buttons;
		});


		EmitSignal(SignalName.InteractivePopUp, new ParameterWrapper<InteractablePopUp>(
			new OptionsPopUp(
				controller.id,
				"Pick the civilization...",
				null,
				options,
				Advisor.None,
				Mood.None,
				hSize: 550,
				margins: new Margins(top: 150)
			)
		));
	}

	public void EmitScienceGuidanceSignal() {
		EmitSignal(SignalName.InteractivePopUp, new ParameterWrapper<InteractablePopUp>(
			new InformationPopup(
				controller.id,
				"Science Advisor",
				"Sir, out Prophets need guidance. What shall we research?",
				Advisor.Science,
				Mood.Sad,
				350
			)
		));
	}

	public void EmitConfirmStopWorkerActionSignal(MsgDisplayStopWorkerActionPopup msg) {
		EmitSignal(SignalName.InteractivePopUp, new ParameterWrapper<InteractablePopUp>(
			new ConfirmPopUp(
				controller.id,
				"Domestic Advisor",
				$"This worker has been ordered to {C7Action.ToTooltip(msg.workerJob.UIAction)} and will be done in {msg.turnsLeft} turns." +
				$"\nDo you want them to stop?",
				Advisor.Domestic,
				Mood.Happy,
				"Yes, there is more important work to do!",
				"No, carry on.",
				() => {
					new MsgDoStopWorkerAction(msg.worker).send();
				},
				hSize: 350
			)
		));
	}

	public void EmitHurryProductionSignal(MsgDisplayHurryProductionPopup msg) {
		if (msg.details.errorMessage != null) {
			EmitSignal(SignalName.InteractivePopUp, new ParameterWrapper<InteractablePopUp>(
				new InformationPopup(
					controller.id,
					"Domestic Advisor",
					msg.details.errorMessage,
					Advisor.Domestic,
					Mood.Sad,
					hSize: 300
				)
			));
			return;
		}

		string yesText = "Yes";
		string noText = "No";
		var mood = Mood.Sad;
		if (msg.details.hurryProductionType == Government.HurryProductionType.ForcedLabor) {
			yesText = "It's that important. Get out my whip!";
			noText = "Never mind.";
		}
		if (msg.details.hurryProductionType == Government.HurryProductionType.PaidLabor) {
			yesText = "Don't argue with me. Start counting!";
			noText = "Oh, I see. Never mind..";
			mood = Mood.Surprised;
		}
		EmitSignal(SignalName.InteractivePopUp, new ParameterWrapper<InteractablePopUp>(
			new ConfirmPopUp(
				controller.id,
				"Domestic Advisor",
				msg.details.costMessage,
				Advisor.Domestic,
				mood,
				yesText,
				noText,
				() => {
					new MsgDoHurryProduction(msg.city).send();
				},
				hSize: 350
			)
		));
	}

	public void EmitCityRansackedSignal(MsgCityRansacked msg) {
		var ransacked = $"[url=no idea][color=Blue]ransacked[/color][/url]";
		var message = $"{msg.city.name} was {ransacked} by {msg.barbTribe} tribe!" +
					  $" They have carried away {msg.goldLiberated} gold!\nWe [i]must build[/i] our military!";

		EmitSignal(SignalName.InteractivePopUp, new ParameterWrapper<InteractablePopUp>(
			new InformationPopup(
				controller.id,
				"Military Advisor",
				message,
				Advisor.Military,
				Mood.Angry,
				350
			)
		));
	}

	public void EmitCivilizationDestroyedSignal(MsgCivilizationDestroyed msg) {
		var friend = false;
		EngineStorage.ReadGameData(data => {
			var destroyedPlayer = data.players.First(p => p.civilization == msg.civilization);
			if (controller.IsAtPeaceWith(destroyedPlayer))
				friend = true;
		});
		EmitSignal(SignalName.InteractivePopUp, new ParameterWrapper<InteractablePopUp>(
			new InformationPopup(
				controller.id,
				"Military Advisor",
				$"The {msg.civilization.noun} have been destroyed.",
				Advisor.Military,
				friend ? Mood.Sad : Mood.Happy,
				350
			)
		));
	}

	public void EmitCityRaisedSignal(MsgCityRaised msg) {
		Player attacker = null;
		Player defender = null;

		EngineStorage.ReadGameData(data => {
			attacker = data.players.First(p => p.id == msg.controllerId);
			defender = data.players.First(p => p.id == msg.ownerId);
		});

		var winningMessage =
			$"Supreme Lord, once again our magnificent armies are victorious!\n" +
			$"We have destroyed {msg.city.name} and \"liberated\" {msg.goldTaken} gold!";

		var winningMood = Mood.Happy;

		var losingMessage =
			$"Terrible news, Sir!\n" +
			$"The evil {attacker.civilization.adjective} have stolen " +
			$"[i]{msg.goldTaken} gold[/i] from {msg.city.name} and burned it to the ground!\n" +
			$"They should pay dearly for this atrocity!";

		var losingMood = Mood.Angry;

		EmitSignal(SignalName.InteractivePopUp, new ParameterWrapper<InteractablePopUp>(
			new InformationPopup(
				controller.id,
				"Military Advisor",
				msg.controllerWon ? winningMessage : losingMessage,
				Advisor.Military,
				msg.controllerWon ? winningMood : losingMood,
				350
			)
		));
	}

	private void EmitWarDeclarationNotificationSignal(MsgWarDeclarationNotification msg) {
		EmitSignal(SignalName.InteractivePopUp, new ParameterWrapper<InteractablePopUp>(
			new InformationPopup(
				controller.id,
				"Military Advisor",
				$"The {msg.aggressor.civilization.noun} declared war on the {msg.opponent.civilization.noun}",
				Advisor.Military,
				Mood.Sad,
				400
			)
		));
	}

	private void EmitWarDeclarationConfirmationSignal(MsgWarDeclarationConfirmation msg) {
		Player aggressor = null;
		Player opponent = null;

		Action yesAction = null;
		// "I said [i]DO IT![/i]",
		string yesText = "I said DO IT!";

		Action nosAction = null;
		string noText = "No. You're right, perhaps we should re-consider.";

		var message = "PLACEHOLDER MSG";

		EngineStorage.ReadGameData(data => {
			aggressor = data.players.First(p => p.id == controller.id);
			opponent = data.players.First(p => p.id == msg.opponentId);

			var currentTurn = data.turn;

			message = $"Sir, this will cause war with the {opponent.civilization.adjective} people.\nAre you sure?";

			yesAction = () => {
				aggressor.DeclareWarOn(opponent, currentTurn);
				msg.callback();
			};
		});

		EmitSignal(SignalName.InteractivePopUp, new ParameterWrapper<InteractablePopUp>(
			new ConfirmPopUp(
				controller.id,
				"Foreign Advisor",
				message,
				Advisor.Foreign,
				Mood.Sad,
				yesText,
				noText,
				yesAction
			)
		));
	}

	private void EmitDiplomaticWarDeclarationConfirmationSignal(MsgWarDeclarationConfirmation msg) {
		Player aggressor = null;
		Player opponent = null;

		Action yesAction = null;
		// "I said [i]DO IT![/i]",
		string yesText = "You 're right! They are scum!";

		Action nosAction = null;
		string noText = "No. We should respect our neighbors.";

		var message = "PLACEHOLDER MSG";

		EngineStorage.ReadGameData(data => {
			aggressor = data.players.First(p => p.id == controller.id);
			opponent = data.players.First(p => p.id == msg.opponentId);

			var currentTurn = data.turn;

			message = $"Let us destroy them, sir!";

			yesAction = () => {
				aggressor.DeclareWarOn(opponent, currentTurn);
				msg.callback();
			};
		});

		EmitSignal(SignalName.InteractivePopUp, new ParameterWrapper<InteractablePopUp>(
			new ConfirmPopUp(
				controller.id,
				"Military Advisor",
				message,
				Advisor.Military,
				Mood.Angry,
				yesText,
				noText,
				yesAction,
				hSize: 350,
				layoutPreset: Control.LayoutPreset.Center,
				margins: new Margins(top: -350, left: 650)
			)
		));
	}

	private void EmitRefuseContactSignal(MsgRefuseContact msg) {
		var noun = "Australians";
		EngineStorage.ReadGameData(data => {
			noun = data.players.First(p => p.id == msg.opponentId).civilization.noun;
		});
		EmitSignal(SignalName.InteractivePopUp, new ParameterWrapper<InteractablePopUp>(
			new InformationPopup(
				controller.id,
				"Foreign Advisor",
				$"The {noun} refused to acknowledge our envoy!",
				Advisor.Foreign,
				Mood.Angry,
				350
			)
		));
	}

	private void EmitCityRiotWarningSignal(MsgCityRiotWarning msg) {
		EmitSignal(SignalName.InteractivePopUp, new ParameterWrapper<InteractablePopUp>(
			new ConfirmPopUp(
				controller.id,
				"Domestic Advisor",
				$"[i]{msg.city.name}[/i] will riot! Are you sure you want to end your turn?",
				Advisor.Domestic,
				Mood.Angry,
				"Yes, let them riot!",
				"No, you are right, I will handle it.",
				yesAction: () => { DoActualEndTurn(); },
				hSize: 350
			)
		));
	}

	private void EmitSelectGovernmentSignal(MsgSelectGovernment msg) {

		var options = new List<ButtonAction>();

		EngineStorage.ReadGameData(data => {
			var player = data.players.First(p => p.id == msg.controllerId);
			var governments = player.GetAvailableGovernments(data);
			var buttons = new List<ButtonAction>();
			foreach (Government g in governments) {
				var btn = new ButtonAction() {
					message = $"{g.name}",
					action = () => {
						new SelectGovernmentMsg(player, g).send();
					}
				};
				buttons.Add(btn);
			}

			options = buttons;
		});

		EmitSignal(SignalName.InteractivePopUp, new ParameterWrapper<InteractablePopUp>(
			new OptionsPopUp(
				controller.id,
				"Government Types",
				$"Select a new government type.",
				options,
				Advisor.None,
				Mood.None,
				350,
				margins: new Margins(top: 100)
			)
		));
	}

	private void EmitAlreadyInRevolutionSignal(MsgAlreadyInRevolution msg) {
		EmitSignal(SignalName.InteractivePopUp, new ParameterWrapper<InteractablePopUp>(
			new InformationPopup(
				controller.id,
				"Domestic Advisor",
				$"Sir! We are already undergoing a revolt..!\n\nDid you take your medication?",
				Advisor.Domestic,
				Mood.Angry,
				350
			)
		));
	}

	private void EmitDescendIntoAnarchySignal(MsgDescendIntoAnarchy msg) {
		EngineStorage.ReadGameData(data => {
			var player = data.players.First(p => p.id == msg.controllerId);
			EmitSignal(SignalName.InteractivePopUp, new ParameterWrapper<InteractablePopUp>(
				new InformationPopup(
					controller.id,
					"Domestic Advisor",
					$"Our people are overthrowing our {msg.government.name}. Our Civilization is descending into Anarchy!",
					Advisor.Domestic,
					Mood.Surprised,
					350
				)
			));
		});
	}

	private void EmitStartARevolutionSignal(MsgConfirmStartRevolution msgConfirm) {
		Player controller = null;
		Government controllerGov = null;
		EngineStorage.ReadGameData(data => {
			controller = data.players.First(p => p.id == msgConfirm.controllerId);
			controllerGov = controller.government;
		});
		EmitSignal(SignalName.InteractivePopUp, new ParameterWrapper<InteractablePopUp>(
			new ConfirmPopUp(
				controller.id,
				"Domestic Advisor",
				$"You say you want a Revolution?!",
				Advisor.Domestic,
				Mood.Happy,
				"Yes. You know it's gonna be alright.",
				"No. You can count me out.",
				yesAction: () => {
					new MsgDescendIntoAnarchy(controller.id, controllerGov).send();
				},
				yesCallback: async () => {
					await EngineStorage.WaitForMessageToEngine<MsgUiDisengaged>();
					new StartGovernmentTransitionMsg(controller).send();
				},
				hSize: 350
			)
		));
	}

	private void EmitConfirmAbandonCitySignal(MsgDisplayAbandonCityPopup msg) {
		EmitSignal(SignalName.InteractivePopUp, new ParameterWrapper<InteractablePopUp>(
			new ConfirmPopUp(
				controller.id,
				"Domestic Advisor",
				$"Are you sure you want to abandon {msg.city.name}?",
				Advisor.Domestic,
				Mood.Angry,
				"Yes, we don't want it anymore.",
				"No. Sorry.",
				yesAction: () => {
					CityInteractions.DestroyCity(msg.city);
				},
				hSize: 350
			)
		));
	}

	private void EmitQuitGameSignal() {
		EmitSignal(SignalName.InteractivePopUp, new ParameterWrapper<InteractablePopUp>(
			new ConfirmPopUp(
				controller.id,
				"Oh No!",
				$"Do you really want to quit?",
				Advisor.None,
				Mood.None,
				"Yes, immediately.",
				"No, not really",
				yesAction: () => {
					OnQuitTheGame();
				},
				hSize: 350,
				margins: new Margins(top: 100)
			)
		));
	}

	private void EmitNameCitySignal(MsgNameCity msg) {
		EmitSignal(SignalName.InteractivePopUp, new ParameterWrapper<InteractablePopUp>(
			new TextInputPopUp(
				controller.id,
				"Name this town?",
				"Name:",
				msg.nextCityName,
				callback: OnBuildCity,
				Advisor.Culture,
				Mood.Happy,
				hSize: 550
			)
		));
	}

	private void EmitTerrainImprovementReplacementConfirmationSignal(MsgReplaceTerrainImprovementConfirmation msg) {
		var endMsg = $"A previous terrain enhancement ({msg.terrainImprovement.key.Capitalize()}) will be replaced \nby this operation. Do you wish to continue?";

		var options = new List<ButtonAction>();

		var yesBtn = new ButtonAction() {
			message = "Do as I say!",
			action = () => new MsgStartWorkerJob(CurrentlySelectedUnit.id, msg.terraform).send(),
		};
		var noBtn = new ButtonAction() {
			message = "No, don't let all that hard work go to waste.",
		};

		options.Add(yesBtn);
		options.Add(noBtn);

		EmitSignal(SignalName.InteractivePopUp, new ParameterWrapper<InteractablePopUp>(
			new OptionsPopUp(
				controller.id,
				"Domestic Advisor",
				endMsg,
				options,
				Advisor.Domestic,
				Mood.Sad,
				hSize: 500
			)
		));
	}

	private void EmitDisbandUnitConfirmationSignal(MsgDisbandUnitConfirmation msg) {
		var endMsg = $"Disband {msg.mapUnit.name}? Pardon me but these are OUR people.\nDo you really want to disband them?";

		var options = new List<ButtonAction>();

		var yesBtn = new ButtonAction() {
			message = "Yes, we need to!",
			action = () => new ActionToEngineMsg(async () => await CurrentlySelectedUnit.Disband()).send(),
		};
		var noBtn = new ButtonAction() {
			message = "No. Maybe you are right, advisor."
		};

		options.Add(yesBtn);
		options.Add(noBtn);

		EmitSignal(SignalName.InteractivePopUp, new ParameterWrapper<InteractablePopUp>(
			new OptionsPopUp(
				controller.id,
				"Domestic Advisor",
				endMsg,
				options,
				Advisor.Domestic,
				Mood.Surprised,
				hSize: 500
			)
		));
	}

	private void EmitVictorySignal(MsgVictory msg) {
		var endMsg =
			$"The {msg.winner.civilization.noun} have won a {msg.victory.Header()} victory!\n"
			+ "This game is over: No further score will be entered.\n\n";

		var options = new List<ButtonAction>();

		var retireBtn = new ButtonAction() {
			message = "Good! I’m Done!",
			action = () => OnRetire()
		};
		var continueBtn = new ButtonAction() {
			message = "Wait, lemme just play a couple of more turns..."
		};

		options.Add(retireBtn);
		options.Add(continueBtn);

		EmitSignal(SignalName.InteractivePopUp, new ParameterWrapper<InteractablePopUp>(
			new OptionsPopUp(
				controller.id,
				"You Win!",
				endMsg,
				options,
				Advisor.None,
				Mood.None,
				hSize: 600,
				margins: new Margins(top: 100)
			)
		));
	}
}
