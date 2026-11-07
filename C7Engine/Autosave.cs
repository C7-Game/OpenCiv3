using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using C7GameData;
using Serilog;

namespace C7Engine {
	public static class Autosave {
		private static readonly ILogger log = Log.ForContext(typeof(Autosave));

		/// <summary>
		/// Saves the current turn, keeping the Turn 000 save and the most recent
		/// <paramref name="turnsToKeep"/> other autosaves.
		/// </summary>
		public static void Perform(GameData gameData, Player player, int turnsToKeep) {
			if (gameData == null) {
				return;
			}

			try {
				if (gameData.turn == 0 || turnsToKeep > 0) {
					string path = Path.Combine(SavePaths.AutosaveFolder, SaveNameGenerator.GenerateSaveFileName(gameData, player));
					SaveManager.Save(gameData, path);
					if (gameData.turn == 0) {
						C7Settings.SetLastWorldSave(path);
					}
				}
				Prune(turnsToKeep);
			} catch (Exception ex) {
				log.Error(ex, "Failed to autosave turn {Turn}", gameData.turn);
			}
		}

		public static void Clear() {
			try {
				if (!Directory.Exists(SavePaths.AutosaveFolder)) {
					return;
				}
				foreach (string path in Directory.GetFiles(SavePaths.AutosaveFolder, "*.json")) {
					File.Delete(path);
				}
			} catch (Exception ex) {
				log.Error(ex, "Failed to clear autosaves in {Path}", SavePaths.AutosaveFolder);
			}
		}

		/// <summary>
		/// Returns the autosave files that should be deleted. Turn 000 is always
		/// kept; other saves are ordered by their turn number.
		/// </summary>
		public static IEnumerable<string> SelectFilesToPrune(IEnumerable<string> paths, int turnsToKeep) {
			List<(string Path, int Turn)> autosaves = new();
			foreach (string path in paths) {
				int? turn = ParseTurn(path);
				if (turn.HasValue) {
					autosaves.Add((path, turn.Value));
				}
			}

			return autosaves
				.Where(file => file.Turn != 0)
				.OrderByDescending(file => file.Turn)
				.Skip(Math.Max(turnsToKeep, 0))
				.Select(file => file.Path)
				.ToList();
		}

		private static void Prune(int turnsToKeep) {
			if (!Directory.Exists(SavePaths.AutosaveFolder)) {
				return;
			}

			foreach (string path in SelectFilesToPrune(Directory.GetFiles(SavePaths.AutosaveFolder, "*.json"), turnsToKeep)) {
				File.Delete(path);
			}
		}

		private static int? ParseTurn(string path) {
			string name = Path.GetFileName(path);
			const string marker = "-Turn ";
			int start = name.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
			if (start < 0) {
				return null;
			}

			start += marker.Length;
			int end = name.IndexOf('-', start);
			if (end < 0) {
				return null;
			}

			return int.TryParse(name[start..end], out int turn) ? turn : null;
		}
	}
}
