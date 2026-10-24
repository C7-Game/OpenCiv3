using System;
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

	public void SetDirectoryForLoading(string RelPath) {
		if (System.IO.Path.IsPathRooted(RelPath)) {
			CurrentDir = RelPath;
		} else {
			CurrentDir = Util.Civ3Root + "/" + RelPath;
		}
		FileMode = FileDialog.FileModeEnum.OpenFile;
	}

	public void SetDirectoryForLoadingAbsolute(string path) {
		CurrentDir = path;
		FileMode = FileDialog.FileModeEnum.OpenFile;
	}

	public void SetCurrentFileName(string fileName) {
		if (!string.IsNullOrEmpty(fileName)) {
			CurrentFile = fileName;
		}
		FileMode = FileDialog.FileModeEnum.OpenFile;
	}

	public void SetupForLoadWithLatestSave() {
		try {
			string saveDir = C7Engine.C7Settings.GetSaveFolder();
			if (!string.IsNullOrEmpty(saveDir)) {
				if (System.IO.Path.IsPathRooted(saveDir) && System.IO.Directory.Exists(saveDir)) {
					CurrentDir = saveDir;
				} else if (System.IO.Directory.Exists(saveDir)) {
					CurrentDir = saveDir;
				} else {
					try {
						System.IO.Directory.CreateDirectory(saveDir);
						CurrentDir = saveDir;
					} catch {
					}
				}
			}
			string latest = C7Engine.C7Settings.GetLatestSave();
			if (!string.IsNullOrEmpty(latest)) {
				if (System.IO.Path.IsPathRooted(latest)) {
					try {
						string latestDir = System.IO.Path.GetDirectoryName(latest);
						if (!string.IsNullOrEmpty(latestDir) && System.IO.Directory.Exists(latestDir)) {
							CurrentDir = latestDir;
						}
					} catch {
					}
					try {
						CurrentFile = System.IO.Path.GetFileName(latest);
					} catch {
						CurrentFile = latest;
					}
				} else {
					CurrentFile = latest;
				}
			}
			FileMode = FileDialog.FileModeEnum.OpenFile;
		} catch {
		}
	}

	public void SetDirectoryForSaving(string RelPath) {
		// If RelPath is an absolute or contains full path, use it directly
		if (System.IO.Path.IsPathRooted(RelPath)) {
			CurrentDir = RelPath;
		} else {
			CurrentDir = Util.Civ3Root + "/" + RelPath;
		}
		FileMode = FileDialog.FileModeEnum.SaveFile;
	}

	public void SetDirectoryForSavingAbsolute(string path) {
		CurrentDir = path;
		FileMode = FileDialog.FileModeEnum.SaveFile;
	}

	public void SetSaveFileName(string fileName) {
		if (!string.IsNullOrEmpty(fileName)) {
			CurrentFile = fileName;
		}
		FileMode = FileDialog.FileModeEnum.SaveFile;
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
			EngineStorage.ReadGameData((GameData gameData) => {
				C7GameData.Save.SaveGame.FromGameData(gameData).Save(path);
			});
			try {
				C7Engine.C7Settings.SetLatestSave(System.IO.Path.GetFileName(path));
			} catch {
			}
		}
	}
}
