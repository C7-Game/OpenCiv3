using Godot;
using System;
using C7Engine;
using Serilog;

public partial class MainMenu : Node {
	private ILogger log;

	[Export]
	Civ3FileDialog LoadDialog;
	[Export]
	Control NoCiv3Options;
	[Export]
	FileDialog SetCiv3HomeDialog;
	[Export]
	Civ3FileDialog LoadScenarioDialog;
	[Export]
	MenuButtonContainer ButtonContainer;
	[Export]
	AudioStreamPlayer player;

	GlobalSingleton Global;

	public override void _Ready() {
		log = LogManager.ForContext<MainMenu>();
		log.Debug("enter MainMenu._Ready");

		DisplayServer.WindowSetTitle((string)ProjectSettings.GetSetting("application/config/name"));

		try {
			DisplayTitleScreen();
		} catch (Exception ex) {
			log.Error(ex, "Could not set up the main menu");
		}
	}

	private void DisplayTitleScreen() {
		// To pass data between scenes, putting path string in a global singleton and reading it later in createGame
		Global = GetNode<GlobalSingleton>("/root/GlobalSingleton");
		Global.ResetLoadGameFields();

		LoadDialog.SetDirectoryForLoading(@"Conquests/Saves");
		LoadScenarioDialog.SetDirectoryForLoading(@"Conquests/Scenarios");
		LoadScenarioDialog.GoToScenarioSetupAfterLoading = true;

		if (!C7Settings.UseStandaloneMode() && !ClassicGraphicsAvailable()) {
			NoCiv3Options.Visible = true;
			ButtonContainer.Visible = false;
			return;
		}

		ButtonContainer.Visible = true;
		ButtonContainer.CreateButtons();

		// TODO: enable buttons are features are implemented
		ButtonContainer.NewGame.Pressed += GoToWorldSetup;
		ButtonContainer.QuickStart.Pressed += QuickStartGame;
		ButtonContainer.Tutorial.Pressed += QuickStartGame;
		ButtonContainer.Tutorial.Visible = false;
		ButtonContainer.LoadGame.Pressed += LoadGame;
		ButtonContainer.LoadScenario.Pressed += LoadScenario;
		ButtonContainer.HallOfFame.Pressed += HallOfFame;
		ButtonContainer.HallOfFame.Visible = false;
		ButtonContainer.Preferences.Pressed += Preferences;
		ButtonContainer.Preferences.Visible = false;
		ButtonContainer.AudioPreferences.Pressed += Preferences;
		ButtonContainer.AudioPreferences.Visible = false;
		ButtonContainer.Credits.Pressed += showCredits;
		ButtonContainer.Exit.Pressed += _on_Exit_pressed;

		ButtonContainer.ToggleGraphics.Pressed += () => {
			Global.ToggleStandaloneMode();
			GetTree().ChangeSceneToFile("res://UIElements/MainMenu/main_menu.tscn");
		};
		SetToggleGraphicsText();

		// We can't toggle to using civ3 graphics in standalone mode.
		if (C7Settings.UseStandaloneMode() && !ClassicGraphicsAvailable()) {
			ButtonContainer.ToggleGraphics.Visible = false;
		}

		// Hide if valid path is present as proven by reaching this point in code
		NoCiv3Options.Visible = false;
	}

	private bool ClassicGraphicsAvailable() {
		if (string.IsNullOrEmpty(Util.Civ3Root)) {
			return false;
		}

		string[] basePaths = ["Conquests", "civ3PTW", ""];
		foreach (string basePath in basePaths) {
			string relPath = string.IsNullOrEmpty(basePath) ? "Art/buttonsFINAL.pcx" : $"{basePath}/Art/buttonsFINAL.pcx";
			if (Util.FileExistsIgnoringCase(Util.Civ3Root, relPath) != null) {
				return true;
			}
		}

		return false;
	}

	private void SetToggleGraphicsText() {
		if (C7Settings.UseStandaloneMode()) {
			ButtonContainer.ToggleGraphics.Text = "Import Civilization III Graphics";
		} else {
			ButtonContainer.ToggleGraphics.Text = "Use OpenCiv3 Graphics";
		}
	}

	public void GoToWorldSetup() {
		PlayButtonPressedSound();
		GetTree().ChangeSceneToFile("res://UIElements/NewGame/world_setup.tscn");
	}

	public void QuickStartGame() {
		log.Information("start game button pressed");
		PlayButtonPressedSound();
		QuickStartSetup.Init(Global);
		GetTree().ChangeSceneToFile("res://C7Game.tscn");
	}

	public void LoadGame() {
		log.Information("load game button pressed");
		PlayButtonPressedSound();
		LoadDialog.Popup();
	}

	public void LoadScenario() {
		log.Information("load scenario button pressed");
		PlayButtonPressedSound();
		LoadScenarioDialog.Popup();
	}

	public void showCredits() {
		log.Information("credits button pressed");
		GetTree().ChangeSceneToFile("res://Credits.tscn");
	}

	public void HallOfFame() {
		PlayButtonPressedSound();
	}

	public void Preferences() {
		PlayButtonPressedSound();
	}

	public void _on_Exit_pressed() {
		GetTree().Quit(); // no need to notify the scene tree
	}

	private void PlayButtonPressedSound() {
		AudioStream stream = AudioLoader.Load("buttons.button_1");

		if (stream == null)
			return;

		player.Stream = stream;
		player.Play();
	}

	private void _on_SetCiv3Home_pressed() {
		SetCiv3HomeDialog.Popup();
	}

	private void _on_SetCiv3HomeDialog_dir_selected(string path) {
		Util.Civ3Root = path;
		C7Settings.SetValue("locations", "civ3InstallDir", path);
		C7Settings.SaveSettings();
		// This function should only be reachable if DisplayTitleScreen failed on previous runs, so should be OK to run here
		DisplayTitleScreen();
	}

	private void UseStandaloneModePressed() {
		Global.ActivateGameMode(GamePaths.standalone);
		DisplayTitleScreen();
	}
}
