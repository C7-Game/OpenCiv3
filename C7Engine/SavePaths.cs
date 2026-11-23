using System.IO;

namespace C7Engine {
	public static class SavePaths {
		private static string SettingsDirectory => Path.GetDirectoryName(Path.GetFullPath(C7Settings.SettingsFilePath));

		/// <summary>
		/// The folder for manual saves and autosaves.
		///
		/// Precedence: an explicit [locations] SaveFolder, then
		/// civ3InstallDir/Conquests/Saves, then Saves next to C7.ini.
		/// </summary>
		public static string SaveFolder {
			get {
				string configured = C7Settings.GetSettingsValueOrDefault(
					C7Settings.Locations.SectionName, C7Settings.Locations.SaveFolder, null);
				if (!string.IsNullOrEmpty(configured)) {
					return Resolve(configured);
				}

				if (!C7Settings.UseStandaloneMode()) {
					string civ3InstallDir = C7Settings.GetSettingsValueOrDefault(
						C7Settings.Locations.SectionName, C7Settings.Locations.Civ3InstallDir, null);
					if (!string.IsNullOrEmpty(civ3InstallDir)) {
						return Path.Combine(Resolve(civ3InstallDir), "Conquests", "Saves");
					}
				}

				return Path.Combine(SettingsDirectory, "Saves");
			}
		}

		public static string AutosaveFolder => Path.Combine(SaveFolder, "Auto");

		public static string LatestSaveFileName {
			get {
				string latest = C7Settings.GetLatestSave();
				if (string.IsNullOrEmpty(latest)) {
					return null;
				}
				string fileName = Path.GetFileName(latest);
				return string.IsNullOrEmpty(fileName) ? null : fileName;
			}
		}

		/// <summary>
		/// The folder holding the latest manual save. Falls back to the save
		/// folder when the configured save no longer exists.
		/// </summary>
		public static string LatestSaveDirectory {
			get {
				string latest = C7Settings.GetLatestSave();
				if (string.IsNullOrEmpty(latest)) {
					return SaveFolder;
				}

				string path = Path.IsPathRooted(latest) ? latest : Path.Combine(SaveFolder, latest);
				if (!File.Exists(path)) {
					return SaveFolder;
				}

				string directory = Path.GetDirectoryName(path);
				return string.IsNullOrEmpty(directory) ? SaveFolder : directory;
			}
		}

		public static void EnsureSaveFolderExists() {
			Directory.CreateDirectory(SaveFolder);
		}

		private static string Resolve(string path) {
			return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(SettingsDirectory, path));
		}
	}
}
