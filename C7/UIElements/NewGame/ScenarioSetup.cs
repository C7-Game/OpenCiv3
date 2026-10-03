using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using C7GameData;
using C7Engine;
using C7GameData.Save;
using Serilog;

public partial class ScenarioSetup : Control {
	private static ILogger log = LogManager.ForContext<ScenarioSetup>();

	[Export] TextureRect background;

	[Export] GridContainer playerListContainer;
	List<Civilization> civilizations = new();
	Civilization civilization = null;
	ButtonGroup playerListButtonGroup = new();
	TextureRect leaderHead = new();
	[Export] Label civLabel;

	[Export] GridContainer difficultyContainer;

	Difficulty difficulty = null;
	ButtonGroup difficultyButtonGroup = new();

	[Export] TextureButton confirm;
	[Export] TextureButton cancel;

	[Export] Label loadingLabel;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		SaveGame save = GetSave();

		// Set up buttons for the civs the player can play as.
		civilizations = save.Civilizations;
		playerListContainer.Columns = (int)Math.Ceiling(civilizations.Count / 12.0);

		List<SavePlayer> pickablePlayers = save.Players.Where(p => p.canBePicked).ToList();
		string initiallySelectedCiv = pickablePlayers.First(p => p.canBePicked).civilization;

		foreach (SavePlayer player in pickablePlayers) {
			Civilization civ = civilizations.Find(c => c.name == player.civilization);

			Civ3MenuButton button = new() {
				Text = civ.name,
				FontSize = 12,
				textPosition = Civ3MenuButton.TextPosition.TextLeftOfIcon,
				ButtonGroup = playerListButtonGroup,
				ToggleMode = true,
			};
			button.Pressed += () => {
				this.civilization = civ;
				DisplaySelectedLeader();
			};
			playerListContainer.AddChild(button);

			if (civ.name == initiallySelectedCiv) {
				button.ButtonPressed = true;
				this.civilization = civ;
			}
		}
		background.AddChild(leaderHead);
		DisplaySelectedLeader();

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
			button.Pressed += () => { this.difficulty = difficulty; };
			container.AddChild(button);
			if (difficulty.Name == initiallySelectedDifficulty) {
				button.ButtonPressed = true;
				this.difficulty = difficulty;
			}
			container.CustomMinimumSize = new Vector2(843.0f / difficultyContainer.Columns, 0);
		}

		confirm.Pressed += CreateGame;
		cancel.Pressed += BackToMainMenu;
	}

	private void BackToMainMenu() {
		GetTree().ChangeSceneToFile("res://UIElements/MainMenu/main_menu.tscn");
	}

	private void DisplaySelectedLeader() {
		leaderHead.Texture = TextureLoader.Load("leader_heads", civilization);
		leaderHead.Scale = new Vector2(1.7f, 1.7f);
		leaderHead.SetPosition(new Vector2(414, 46));

		string traits = string.Join(", ", civilization.traits);
		civLabel.Text = $"{civilization.leader} of the {civilization.noun}\n({traits})";
	}

	private SaveGame GetSave() {
		string loadGamePath = GetNode<GlobalSingleton>("/root/GlobalSingleton").LoadGamePath;
		return SaveManager.LoadSave(loadGamePath,
									GamePaths.DefaultBicPath,
									(string scenarioSearchPath) => {
										// See corresponding logic in Game.cs
										Util.setModPath(scenarioSearchPath);
										log.Debug("RelativeModPath ", scenarioSearchPath);
										return Util.Civ3MediaPath("Text/PediaIcons.txt");
									},
									(string scenarioSearchPath) => {
										Util.setModPath(scenarioSearchPath);
										return Util.Civ3MediaPath("Text/Civilopedia.txt");
									});
	}

	private void CreateGame() {
		GlobalSingleton global = GetNode<GlobalSingleton>("/root/GlobalSingleton");
		loadingLabel.Visible = true;
		SaveGame save = GetSave();

		// World generation can take a bit of time if multiple attempts are
		// needed, so we don't want to tie up the UI thread.
		Thread thread = new(() => { UpdateSaveAndStartGame(save, global); });
		thread.Start();
	}

	private void UpdateSaveAndStartGame(SaveGame save, GlobalSingleton global) {
		foreach (SavePlayer sp in save.Players) {
			sp.human = sp.civilization == this.civilization.name;
		}
		save.GameDifficulty = difficulty;

		log.Information("saving updated scenario");
		global.SaveGame = save;

		log.Information("opening map");
		CallDeferred("StartGame");
	}

	private void StartGame() {
		GetTree().ChangeSceneToFile("res://C7Game.tscn");
	}
}
