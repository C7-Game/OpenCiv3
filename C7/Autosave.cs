using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using C7Engine;
using C7.UIElements;
using C7GameData;

public static class Autosave {
	public static void ClearAutosaveDirectory() {
		try {
			string autoDir = SaveNameGenerator.GetAutosaveDirectory();
			if (Directory.Exists(autoDir)) {
				foreach (string file in Directory.GetFiles(autoDir, "*.json")) {
					try {
						File.Delete(file);
					} catch {
					}
				}
			}
		} catch {
		}
	}

	public static void PerformAutosave() {
		try {
			GameData gameData = EngineStorage.gameData;
			if (gameData == null) return;
			Player player = null;
			if (gameData.GetFirstHumanPlayer() != null) {
				player = gameData.GetFirstHumanPlayer();
			} else if (gameData.players != null) {
				player = gameData.players.FirstOrDefault(p => !p.isBarbarians);
			}
			int turn = TurnHandling.GetTurnNumber();
			int autosaveTurns = PreferencesSettings.GetAutosaveTurns();
			string autoDir = SaveNameGenerator.GetAutosaveDirectory();
			string fileName = SaveNameGenerator.GetAutosaveFileName(gameData, player);
			string fullPath = Path.Combine(autoDir, fileName);

			// Always save turn 0 even if disabled
			if (turn == 0 || autosaveTurns > 0) {
				// Ensure directory exists
				Directory.CreateDirectory(autoDir);
				try {
					SaveManager.Save(fullPath);
				} catch {
					try {
						C7GameData.Save.SaveGame saveGame = C7GameData.Save.SaveGame.FromGameData(gameData);
						saveGame.Save(fullPath);
					} catch {
					}
				}

			} else if (turn == 0) {
				Directory.CreateDirectory(autoDir);
				try {
					SaveManager.Save(fullPath);
				} catch {
					try {
						C7GameData.Save.SaveGame saveGame = C7GameData.Save.SaveGame.FromGameData(gameData);
						saveGame.Save(fullPath);
					} catch {
					}
				}

			}

			// Rotation: keep turn 000 always; keep x most recent for others
			if (autosaveTurns >= 0) {
				try {
					// Find all autosaves for this game
					List<FileInfo> files = Directory.GetFiles(autoDir, "*.json")
						.Select(p => new FileInfo(p))
						.Where(f => IsForSameGame(f.Name, fileName))
						.ToList();

					// Always protect Turn 000
					int keep = autosaveTurns;
					var others = files
						.Where(f => !f.Name.Contains("-Turn 000-"))
						.OrderByDescending(f => f.LastWriteTimeUtc)
						.ToList();

					if (keep < 0) keep = 0;
					var toDelete = others.Skip(keep).ToList();

					foreach (var f in toDelete) {
						try {
							if (f.Name.Contains("-Turn 000-")) {
								continue;
							}
							f.Delete();
						} catch {
						}
					}
				} catch {
				}
			}
		} catch {
		}
	}

	private static bool IsForSameGame(string candidate, string current) {
		// Group by leader of the noun prefix
		try {
			int idx1 = candidate.IndexOf(" of the ");
			int idx2 = current.IndexOf(" of the ");
			if (idx1 >= 0 && idx2 >= 0) {
				string prefix1 = candidate.Substring(0, idx1 + " of the ".Length);
				string prefix2 = current.Substring(0, idx2 + " of the ".Length);
				if (!string.IsNullOrEmpty(prefix1) && prefix1 == prefix2) {
					return true;
				}
			}
		} catch {
		}
		return candidate == current;
	}
}
