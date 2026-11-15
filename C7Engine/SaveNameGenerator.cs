using System;
using System.IO;
using C7GameData;

namespace C7Engine {
	public static class SaveNameGenerator {
		public static string GenerateSaveFileName(GameData gameData, Player player) {
			if (gameData == null) {
				return null;
			}

			string date = gameData.timeOptions?.GetDisplayTime(gameData.turn) ?? gameData.turn.ToString();
			return GenerateSaveFileName(player, gameData.turn, date);
		}

		public static string GenerateSaveFileName(Player player, int turn, string displayDate) {
			string leader = player?.civilization?.leader ?? "Leader";
			string noun = player?.civilization?.noun;
			if (string.IsNullOrEmpty(noun)) {
				noun = player?.civilization?.name ?? "People";
			}

			string name = $"{leader} of the {noun}-Turn {turn:000}-{displayDate}.json";
			return string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
		}
	}
}
