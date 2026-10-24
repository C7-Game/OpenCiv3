using System;
using System.IO;
using System.Linq;
using C7GameData;

namespace C7Engine {
	public static class SaveNameGenerator {
		public static string GenerateSaveFileName(GameData gameData, Player player) {
			if (gameData == null) {
				return null;
			}

			Player human = player;
			if (human == null && gameData.players != null) {
				human = gameData.GetFirstHumanPlayer();
				if (human == null) {
					human = gameData.players.FirstOrDefault(p => !p.isBarbarians);
				}
			}

			string leader = human?.civilization?.leader ?? "Leader";
			string noun = human?.civilization?.noun;
			if (string.IsNullOrEmpty(noun)) {
				noun = human?.civilization?.name ?? "People";
			}

			int turn = TurnHandling.GetTurnNumber();
			string date = "";
			if (gameData.timeOptions != null) {
				try {
					date = gameData.timeOptions.GetDisplayTime(turn);
				} catch {
					date = turn.ToString();
				}
			}

			string name = $"{leader} of the {noun}-Turn {turn:000}-{date}.json";
			name = string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
			return name;
		}

		public static string GetSaveDirectory() {
			return C7Settings.GetSaveFolder();
		}

		public static string GetAutosaveDirectory() {
			string dir = Path.Combine(GetSaveDirectory(), "Auto");
			Directory.CreateDirectory(dir);
			return dir;
		}

		public static string GetAutosaveFileName(GameData gameData, Player player) {
			return GenerateSaveFileName(gameData, player);
		}
	}
}
