using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using C7GameData;
using C7Engine;
using C7Engine.Lua;
using C7GameData.Save;
using Serilog;

public partial class PlayerSetup : Control {
	private static ILogger log = LogManager.ForContext<PlayerSetup>();

	[Export] TextureRect background;

	[Export] GridContainer playerListContainer;
	ButtonGroup playerListButtonGroup = new();
	TextureRect leaderHead = new();
	[Export] Label civLabel;

	[Export] GridContainer opponentListContainer;
	List<OptionButton> opponentSelectors = new();

	[Export] GridContainer rulesContainer;

	[Export] GridContainer difficultyContainer;

	ButtonGroup difficultyButtonGroup = new();

	[Export] TextureButton confirm;
	[Export] TextureButton cancel;

	[Export] Label loadingLabel;

	SaveGame save;

	Civilization selectedCivilization;
	Difficulty selectedDifficulty;
	VictoryConditions victoryConditions;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		GlobalSingleton global = GetNode<GlobalSingleton>("/root/GlobalSingleton");
		save = global.SaveGame;

		// Set up buttons for the civs the player can play as.
		playerListContainer.Columns = (int)Math.Ceiling(save.Civilizations.Count / 12.0);
		string initiallySelectedCiv = save.Civilizations.Any(x => x.name == "Netherlands") ? "Netherlands" : save.Civilizations[1].name;
		foreach (Civilization civ in save.Civilizations) {
			if (civ.isBarbarian) {
				continue;
			}

			Civ3MenuButton button = new() {
				Text = civ.name,
				FontSize = 12,
				textPosition = Civ3MenuButton.TextPosition.TextLeftOfIcon,
				ButtonGroup = playerListButtonGroup,
				ToggleMode = true,
			};
			button.Pressed += () => {
				selectedCivilization = civ;
				UpdateOpponentSelectors();
				DisplaySelectedLeader();
			};
			playerListContainer.AddChild(button);

			if (civ.name == initiallySelectedCiv) {
				button.ButtonPressed = true;
				selectedCivilization = civ;
			}
		}
		background.AddChild(leaderHead);
		DisplaySelectedLeader();

		// Set up the options for opponents.
		AddOpponentSelectors(global.WorldCharacteristics.worldSize.numberOfCivs);

		// Set up game rules
		AddRules();

		// Set up the difficulty buttons
		difficultyContainer.Columns = save.Difficulties.Count;
		string initiallySelectedDifficulty = save.Difficulties.Any(x => x.Name == "Regent") ? "Regent" : save.Difficulties[0].Name;
		foreach (Difficulty difficulty in save.Difficulties) {
			CenterContainer container = new();
			difficultyContainer.AddChild(container);

			Civ3MenuButton button = new(Civ3MenuButton.TextPosition.TextAboveIcon) {
				Text = difficulty.Name,
				ButtonGroup = difficultyButtonGroup,
				ToggleMode = true,
			};
			button.Pressed += () => { selectedDifficulty = difficulty; };
			container.AddChild(button);
			if (difficulty.Name == initiallySelectedDifficulty) {
				button.ButtonPressed = true;
				selectedDifficulty = difficulty;
			}
			container.CustomMinimumSize = new Vector2(843.0f / difficultyContainer.Columns, 0);
		}

		confirm.Pressed += CreateGame;
		cancel.Pressed += BackToMainMenu;
	}

	private void AddRules() {
		victoryConditions = new VictoryConditions();
		rulesContainer.Columns = 2;
		rulesContainer.AddThemeConstantOverride("v_separation", 0);

		// TODO: Add Civ3Checkbox in rulesContainer for each victory condition, wire up to victoryConditions
	}

	private void BackToMainMenu() {
		GetTree().ChangeSceneToFile("res://UIElements/MainMenu/main_menu.tscn");
	}

	private void AddOpponentSelectors(int numberOfCivs) {
		int numOpponents = numberOfCivs - 1;

		for (int i = 0; i < numOpponents; ++i) {
			OptionButton optionButton = new();
			StyleBoxFlat styleBox = new() {
				BorderColor = Color.Color8(150, 150, 150, 220),
				BgColor = Color.Color8(255, 255, 255, 0),
				BorderWidthBottom = 2,
				BorderWidthLeft = 2,
				BorderWidthRight = 2,
				BorderWidthTop = 2,
				ContentMarginLeft = 4,
				ContentMarginRight = 4,
				ContentMarginTop = 4,
				ContentMarginBottom = 4
			};
			optionButton.AddThemeStyleboxOverride("normal", styleBox);
			optionButton.AddThemeStyleboxOverride("hover", styleBox); ;
			optionButton.AddThemeStyleboxOverride("pressed", styleBox);

			PopupMenu popup = optionButton.GetPopup();
			popup.AddThemeStyleboxOverride("panel", styleBox);
			popup.MaxSize = new Vector2I(popup.MaxSize.X, 300);
			popup.SetTransparentBackground(false);
			opponentSelectors.Add(optionButton);

			CenterContainer container = new();
			opponentListContainer.AddChild(container);
			container.AddChild(optionButton);

			container.CustomMinimumSize = new Vector2(312.0f / opponentListContainer.Columns, 315.0f / numOpponents);
			optionButton.CustomMinimumSize = new Vector2(290.0f / opponentListContainer.Columns, optionButton.CustomMinimumSize.Y);

			foreach (Civilization civ in save.Civilizations) {
				if (civ.isBarbarian) {
					continue;
				}
				optionButton.AddItem(civ.name);
			}

			// Add (and default to) random for each opponent.
			optionButton.AddItem("Random");
			optionButton.Select(popup.ItemCount - 1);

			for (int k = 0; k < popup.ItemCount; ++k) {
				popup.SetItemAsRadioCheckable(k, false);
				popup.SetItemAsCheckable(k, false);
			}

			optionButton.ItemSelected += (long i) => { UpdateOpponentSelectors(); };
		}

		UpdateOpponentSelectors();
	}

	private void UpdateOpponentSelectors() {
		HashSet<string> civsTaken = new();
		civsTaken.Add(selectedCivilization.name);

		foreach (OptionButton ob in opponentSelectors) {
			string selection = ob.GetItemText(ob.Selected);

			// If the player decides to play as civ X and one of the opponent
			// selectors has X selected, change it to random. Similarly if one
			// of the previous option buttons has selected this civ.
			if (civsTaken.Contains(selection)) {
				ob.Select(ob.GetPopup().ItemCount - 1);
			} else if (selection != "Random") {
				civsTaken.Add(selection);
			}

			for (int i = 0; i < ob.GetPopup().ItemCount; ++i) {
				ob.SetItemDisabled(i, civsTaken.Contains(ob.GetItemText(i)));
			}
		}
	}

	private void DisplaySelectedLeader() {
		Civilization civilization = selectedCivilization;

		leaderHead.Texture = TextureLoader.Load("leader_heads", civilization);
		leaderHead.Scale = new Vector2(1.7f, 1.7f);
		leaderHead.SetPosition(new Vector2(414, 46));

		string traits = string.Join(", ", civilization.traits);
		civLabel.Text = $"{civilization.leader} of the {civilization.noun}\n({traits})";
	}

	private List<SelectedOpponent> CollectSelectedOpponents() {
		List<SelectedOpponent> opponents = [];

		foreach (OptionButton ob in opponentSelectors) {
			string selectedName = ob.GetItemText(ob.Selected);

			if (selectedName == "Random") {
				opponents.Add(new SelectedOpponent { isRandom = true });
			} else {
				opponents.Add(new SelectedOpponent { isRandom = false, Name = selectedName });
			}
		}

		return opponents;
	}

	private void CreateGame() {
		loadingLabel.Visible = true;

		GlobalSingleton global = GetNode<GlobalSingleton>("/root/GlobalSingleton");

		GameSetup gameSetup = new() {
			playerCivilization = selectedCivilization,
			difficulty = selectedDifficulty,
			worldCharacteristics = global.WorldCharacteristics,
			opponents = CollectSelectedOpponents(),
			victoryConditions = victoryConditions
		};

		PersistGameSettings(gameSetup);

		// World generation can take a bit of time if multiple attempts are
		// needed, so we don't want to tie up the UI thread.
		Thread thread = new(() => {
			gameSetup.Populate(save);

			log.Information("opening map");
			CallDeferred(nameof(StartGame));
		});
		thread.Start();
	}

	private void PersistGameSettings(GameSetup gameSetup) {
		try {
			C7Settings.SetValue(C7Settings.LastGame.SectionName, C7Settings.LastGame.WorldSize, gameSetup.worldCharacteristics.worldSize.name);
			C7Settings.SetValue(C7Settings.LastGame.SectionName, C7Settings.LastGame.BarbarianActivity, gameSetup.worldCharacteristics.barbarianActivity.ToString());
			C7Settings.SetValue(C7Settings.LastGame.SectionName, C7Settings.LastGame.Landform, gameSetup.worldCharacteristics.landform.ToString());
			C7Settings.SetValue(C7Settings.LastGame.SectionName, C7Settings.LastGame.OceanCoverage, gameSetup.worldCharacteristics.oceanCoverage.ToString());
			C7Settings.SetValue(C7Settings.LastGame.SectionName, C7Settings.LastGame.Climate, gameSetup.worldCharacteristics.climate.ToString());
			C7Settings.SetValue(C7Settings.LastGame.SectionName, C7Settings.LastGame.Temperature, gameSetup.worldCharacteristics.temperature.ToString());
			C7Settings.SetValue(C7Settings.LastGame.SectionName, C7Settings.LastGame.Age, gameSetup.worldCharacteristics.age.ToString());
			C7Settings.SetValue(C7Settings.LastGame.SectionName, C7Settings.LastGame.Civilization, gameSetup.playerCivilization.name);
			C7Settings.SetValue(C7Settings.LastGame.SectionName, C7Settings.LastGame.Difficulty, gameSetup.difficulty.Name);

			List<SelectedOpponent> ops = gameSetup.opponents;
			string opponentsValue = string.Join("|", ops.Select(o => o.isRandom ? "Random" : o.Name));
			C7Settings.SetValue(C7Settings.LastGame.SectionName, C7Settings.LastGame.Opponents, opponentsValue);

			C7Settings.SaveSettings();
		} catch (Exception e) {
			log.Error(e, "Failed to persist game settings to C7.ini");
		}
	}

	private void StartGame() {
		GetTree().ChangeSceneToFile("res://C7Game.tscn");
	}
}
