using Godot;
using C7GameData;
using C7Engine;

[GlobalClass]
public partial class StatusMenu : Control {
	[Signal] public delegate void ShowGameViewEventHandler();

	[Export] ConsoleButton openDiplomacy;
	[Export] ConsoleButton openPalaceScreen;

	[Export] PopupOverlay popupOverlay;

	public override void _Ready() {
		openDiplomacy.Pressed += OpenDiplomacyPopup;
		openPalaceScreen.Pressed += () => {
			EmitSignal(SignalName.ShowGameView, C7Action.ShowPalaceView);
		};
	}

	public override void _Process(double delta) {
		EngineStorage.ReadGameData((GameData gD) => {
			if (gD.observerMode) {
				return;
			}

			Player player = gD.GetFirstHumanPlayer();

			// Only show the diplomacy button if we have civs to talk to.
			if (player.playerRelationships.Count > 0) {
				openDiplomacy.ShowButton();
			} else {
				// After meeting a civ and revealed the button,
				// if that civ gets destroyed and we don't have any more relationships
				// with other civs (haven't met them yet) we should hide the button again.
				// It will be shown again when we meet another civ.
				openDiplomacy.HideButton();
			}

			// TODO: Don't show the palace button if the player can't start building the palace
			openPalaceScreen.ShowButton();
		});
	}

	private void OpenDiplomacyPopup() {
		new MsgDiplomacyPopUp().send();
	}
}
