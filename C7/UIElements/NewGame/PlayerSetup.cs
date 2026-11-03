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
		string fallbackCiv = save.Civilizations.Any(x => x.name == "Netherlands") ? "Netherlands" : save.Civilizations[1].name;
		string lastCiv = C7Settings.GetSettingsValueOrDefault(C7Settings.LastGame.SectionName, C7Settings.LastGame.Civilization, fallbackCiv);
		string initiallySelectedCiv = save.Civilizations.Any(x => x.name == lastCiv) ? lastCiv : fallbackCiv;
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
		string fallbackDifficulty = save.Difficulties.Any(x => x.Name == "Regent") ? "Regent" : save.Difficulties[0].Name;
		string lastDifficulty = C7Settings.GetSettingsValueOrDefault(C7Settings.LastGame.SectionName, C7Settings.LastGame.Difficulty, fallbackDifficulty);
		string initiallySelectedDifficulty = save.Difficulties.Any(x => x.Name == lastDifficulty) ? lastDifficulty : fallbackDifficulty;
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
		victoryConditions = VictoryConditions.FromSettings();

		rulesContainer.Columns = 2;
		rulesContainer.AddThemeConstantOverride("v_separation", 0);

		// TODO: Add Civ3Checkbox in rulesContainer for each victory condition, wire up to victoryConditions

		var dominationVictory = new Civ3Checkbox() { Text = "Allow Domination Victory" };
		dominationVictory.SetPressed(victoryConditions.AllowDominationVictory);
		dominationVictory.Toggled += (state) => { victoryConditions.AllowDominationVictory = state; };
		rulesContainer.AddChild(dominationVictory);

		var conquestVictory = new Civ3Checkbox() { Text = "Allow Conquest Victory" };
		conquestVictory.SetPressed(victoryConditions.AllowConquestVictory);
		conquestVictory.Toggled += (state) => { victoryConditions.AllowConquestVictory = state; };
		rulesContainer.AddChild(conquestVictory);
	}

	private void BackToMainMenu() {
		GetTree().ChangeSceneToFile("res://UIElements/MainMenu/main_menu.tscn");
	}

	private void AddOpponentSelectors(int numberOfCivs) {
		int numOpponents = numberOfCivs - 1;
		// Two columns on larger maps, like the original game; Standard's
		// 7 rivals and fewer stay in a single column.
		int columns = numOpponents > 7 ? 2 : 1;
		opponentListContainer.Columns = columns;
		int rows = (numOpponents + columns - 1) / columns;
		List<SelectedOpponent> lastOpponents = GameSetup.OpponentsFromSettings(save.Civilizations, numOpponents);

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

			container.CustomMinimumSize = new Vector2(312.0f / columns, 312.0f / rows);
			optionButton.CustomMinimumSize = new Vector2(290.0f / columns, optionButton.CustomMinimumSize.Y);

			optionButton.AddItem(SelectedOpponent.NONE);
			optionButton.AddItem(SelectedOpponent.RANDOM);
			foreach (Civilization civ in save.Civilizations) {
				if (civ.isBarbarian) {
					continue;
				}
				optionButton.AddItem(civ.name);
			}

			// Restore the selection of the last game, defaulting to Random.
			SelectedOpponent opponent = lastOpponents[i];
			string itemName = opponent switch {
				{ isNone: true } => SelectedOpponent.NONE,
				{ isRandom: true } => SelectedOpponent.RANDOM,
				_ => opponent.Name
			};
			int itemIndex = ItemIndexOf(optionButton, itemName);
			optionButton.Select(itemIndex >= 0 ? itemIndex : ItemIndexOf(optionButton, SelectedOpponent.RANDOM));

			for (int k = 0; k < popup.ItemCount; ++k) {
				popup.SetItemAsRadioCheckable(k, false);
				popup.SetItemAsCheckable(k, false);
			}

			optionButton.ItemSelected += (long i) => { UpdateOpponentSelectors(); };
		}

		AddOpponentQuickButtons();
		UpdateOpponentSelectors();
	}

	private void AddOpponentQuickButtons() {
		HBoxContainer container = new() {
			Position = new Vector2(630, 437),
			Size = new Vector2(321, 24),
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		background.AddChild(container);

		Civ3MenuButton allNone = new() { Text = "All NONE" };
		allNone.Pressed += () => SetAllOpponents(SelectedOpponent.NONE);
		container.AddChild(allNone);

		Civ3MenuButton allRandom = new() { Text = "All Random" };
		allRandom.Pressed += () => SetAllOpponents(SelectedOpponent.RANDOM);
		container.AddChild(allRandom);
	}

	private void SetAllOpponents(string name) {
		foreach (OptionButton ob in opponentSelectors) {
			ob.Select(ItemIndexOf(ob, name));
		}
		UpdateOpponentSelectors();
	}

	private static int ItemIndexOf(OptionButton ob, string text) {
		for (int i = 0; i < ob.ItemCount; ++i) {
			if (ob.GetItemText(i) == text) {
				return i;
			}
		}
		return -1;
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
				ob.Select(ItemIndexOf(ob, SelectedOpponent.RANDOM));
			} else if (selection != SelectedOpponent.RANDOM && selection != SelectedOpponent.NONE) {
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

			opponents.Add(selectedName switch {
				SelectedOpponent.NONE => new SelectedOpponent { isNone = true },
				SelectedOpponent.RANDOM => new SelectedOpponent { isRandom = true },
				_ => new SelectedOpponent { Name = selectedName },
			});
		}

		return opponents;
	}

	private void CreateGame() {
		List<SelectedOpponent> opponents = CollectSelectedOpponents();

		loadingLabel.Text = "Loading...";
		loadingLabel.Visible = true;

		GlobalSingleton global = GetNode<GlobalSingleton>("/root/GlobalSingleton");

		GameSetup gameSetup = new() {
			playerCivilization = selectedCivilization,
			difficulty = selectedDifficulty,
			worldCharacteristics = global.WorldCharacteristics,
			opponents = opponents,
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
			string opponentsValue = string.Join("|", ops.Select(o => o switch {
				{ isNone: true } => SelectedOpponent.NONE,
				{ isRandom: true } => SelectedOpponent.RANDOM,
				_ => o.Name
			}));
			C7Settings.SetValue(C7Settings.LastGame.SectionName, C7Settings.LastGame.Opponents, opponentsValue);

			PersistVictoryConditions(gameSetup.victoryConditions);

			C7Settings.SaveSettings();
		} catch (Exception e) {
			log.Error(e, "Failed to persist game settings to C7.ini");
		}
	}

	private void PersistVictoryConditions(VictoryConditions vcs) {
		C7Settings.SetValue(C7Settings.Rules.SectionName, C7Settings.Rules.AllowDominationVictory, vcs.AllowDominationVictory.ToString());
		C7Settings.SetValue(C7Settings.Rules.SectionName, C7Settings.Rules.AllowConquestVictory, vcs.AllowConquestVictory.ToString());
	}

	private void StartGame() {
		GetTree().ChangeSceneToFile("res://C7Game.tscn");
	}
}
