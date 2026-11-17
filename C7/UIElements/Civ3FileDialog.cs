using System;
using System.IO;
using C7Engine;
using C7GameData;
using Godot;
using Serilog;

[GlobalClass]
public partial class Civ3FileDialog : FileDialog {
	// An object for passing information (like save file paths) between scenes.
	GlobalSingleton Global;
	private ILogger log;

	// If true, go to scenario setup after loading, for scenarios.
	public bool GoToScenarioSetupAfterLoading = false;

	public override void _Ready() {
		base._Ready();
		log = LogManager.ForContext<Civ3FileDialog>();

		FileMode = FileDialog.FileModeEnum.OpenFile;
		Access = AccessEnum.Filesystem;

		Global = GetNode<GlobalSingleton>("/root/GlobalSingleton");
		FileSelected += OnFileSelected;
	}

	public void SetDirectoryForLoading(string absoluteDir) {
		CurrentDir = absoluteDir;
		FileMode = FileDialog.FileModeEnum.OpenFile;
	}

	public void SetDirectoryForSaving(string absoluteDir) {
		CurrentDir = absoluteDir;
		FileMode = FileDialog.FileModeEnum.SaveFile;
	}

	public void PrefillFileName(string fileName) {
		if (!string.IsNullOrEmpty(fileName)) {
			CurrentFile = fileName;
		}
	}

	public void PrefillLoadWithLatestSave() {
		SetDirectoryForLoading(SavePaths.LatestSaveDirectory);
		PrefillFileName(SavePaths.LatestSaveFileName);
	}

	private void OnFileSelected(string path) {
		if (FileMode == FileDialog.FileModeEnum.OpenFile) {
			log.Information($"loading {path}");
			Global.LoadGamePath = path;
			if (GoToScenarioSetupAfterLoading) {
				GetTree().ChangeSceneToFile("res://UIElements/NewGame/scenario_setup.tscn");
			} else {
				GetTree().ChangeSceneToFile("res://C7Game.tscn");
			}
		} else {
			if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) {
				path = path + ".json";
			}

			log.Information($"Saving game to {path}");
			EngineStorage.ReadGameData((GameData gameData) => SaveManager.Save(gameData, path));
			C7Settings.SetLatestSave(Path.GetFileName(path));
		}
	}
}
