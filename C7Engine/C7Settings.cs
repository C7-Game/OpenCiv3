using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IniParser.Model;
using IniParser.Exceptions;

namespace C7Engine {
	public class C7Settings {
		private const string SETTINGS_FILE_NAME = "C7.ini";

		/// <summary>
		/// The full path to the settings file. Callers that host the engine (the
		/// Godot layer) should set this to a writable location, since the
		/// process' current working directory is not reliable on all platforms.
		/// For example, a macOS app launched from Finder has "/" as its working
		/// directory, so a relative path cannot be read from or written to.
		/// </summary>
		public static string SettingsFilePath { get; set; } = SETTINGS_FILE_NAME;

		public static IniData settings;

		public static class Audio {
			public const string SectionName = nameof(Audio);
			public const string MasterVolume = nameof(MasterVolume);
			public const string MusicVolume = nameof(MusicVolume);
			public const string SfxAudioVolume = nameof(SfxAudioVolume);
			public const string UiAudioVolume = nameof(UiAudioVolume);
			public const string AmbienceAudioVolume = nameof(AmbienceAudioVolume);
		}

		public static class LastGame {
			public const string SectionName = nameof(LastGame);
			public const string WorldSize = nameof(WorldSize);
			public const string BarbarianActivity = nameof(BarbarianActivity);
			public const string Landform = nameof(Landform);
			public const string OceanCoverage = nameof(OceanCoverage);
			public const string Climate = nameof(Climate);
			public const string Temperature = nameof(Temperature);
			public const string Age = nameof(Age);
			public const string Civilization = nameof(Civilization);
			public const string Difficulty = nameof(Difficulty);
			public const string Opponents = nameof(Opponents);
			public const string LatestSave = nameof(LatestSave);
			public const string LastWorldSave = "LastWorldSave";
		}

		public static class Locations {
			public const string SectionName = nameof(Locations);
			public const string SaveFolder = nameof(SaveFolder);
			public const string Civ3InstallDir = "civ3InstallDir";
		}

		public static class Rules {
			public const string SectionName = nameof(Rules);
			public const string AllowDominationVictory = nameof(AllowDominationVictory);
			public const string AllowConquestVictory = nameof(AllowConquestVictory);
		}

		public static void LoadSettings() {
			try {
				settings = Util.GetFileIniDataParser().ReadFile(SettingsFilePath);
			} catch (ParsingException) {
				//First run.  The file doesn't exist.  That's okay.  We'll use sensible defaults.
				settings = new IniData();
				SaveSettings();
			}
		}

		public static void SaveSettings() {
			try {
				Util.GetFileIniDataParser().WriteFile(SettingsFilePath, settings);
			} catch (Exception ex) {
				// A read-only install directory shouldn't take the game down with
				// it; the in-memory settings still apply for this session.
				Console.Error.WriteLine($"Failed to save settings to {SettingsFilePath}: {ex.Message}");
			}
		}

		public static void SetValue(string section, string key, string value) {
			if (settings == null) {
				LoadSettings();
			}
			settings[section][key] = value;
		}

		public static string GetSettingValue(string section, string key) {
			if (settings == null) {
				LoadSettings();
			}
			return settings[section][key];
		}


		public static bool GetBoolOrDefault(string section, string key, bool defaultValue) {
			return GetSettingsValueOrDefault(section, key, defaultValue ? "true" : "false") == "true";
		}

		public static void SetBool(string section, string key, bool value) {
			SetValue(section, key, value ? "true" : "false");
		}

		public static int GetIntOrDefault(string section, string key, int defaultValue) {
			string value = GetSettingsValueOrDefault(section, key, defaultValue.ToString());
			if (int.TryParse(value, out int result)) {
				return result;
			}
			return defaultValue;
		}

		public static void SetInt(string section, string key, int value) {
			SetValue(section, key, value.ToString());
		}

		public static string GetSettingsValueOrDefault(string section, string key, string defaultValue) {
			if (settings == null) {
				LoadSettings();
			}
			if (settings[section] == null) {
				return defaultValue;
			}
			if (settings[section][key] == null) {
				return defaultValue;
			}
			return settings[section][key];
		}

		public static T GetTypedSettingOrDefault<T>(string section, string key, T defaultValue) where T : struct, Enum {
			string value = GetSettingValue(section, key);
			return Enum.TryParse(value, true, out T result) ? result : defaultValue;
		}

		public static bool UseStandaloneMode() {
			return GetSettingsValueOrDefault("locations", "useStandaloneMode", "false") == "true";
		}

		private static string FindIniFilePath() {
			string[] iniCandidates = new string[] {
				SettingsFilePath,
				Path.Combine(Directory.GetCurrentDirectory(), "C7.ini"),
				Path.Combine(Directory.GetCurrentDirectory(), "C7", "C7.ini"),
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "C7.ini"),
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "C7.ini"),
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "C7", "C7.ini"),
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "C7", "C7.ini"),
			};
			foreach (string iniPath in iniCandidates) {
				try {
					string full = Path.GetFullPath(iniPath);
					if (File.Exists(full)) {
						return full;
					}
				} catch {
				}
			}
			return null;
		}

		private static string ReadIniValue(string iniPath, string section, string key) {
			try {
				string[] lines = File.ReadAllLines(iniPath);
				string currentSection = "";
				foreach (string line in lines) {
					string trimmed = line.Trim();
					if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#")) {
						continue;
					}
					if (trimmed.StartsWith("[") && trimmed.EndsWith("]")) {
						currentSection = trimmed.Substring(1, trimmed.Length - 2).ToLower();
						continue;
					}
					int eq = trimmed.IndexOf('=');
					if (eq > 0) {
						string k = trimmed.Substring(0, eq).Trim();
						string v = trimmed.Substring(eq + 1).Trim();
						if (currentSection == section.ToLower() && string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) {
							return v;
						}
					}
				}
			} catch {
			}
			return null;
		}

		public static string GetSaveFolder() {
			if (settings == null) {
				LoadSettings();
			}

			string iniPath = FindIniFilePath();

			// If configured in settings, resolve relative to ini if needed
			string configured = GetSettingsValueOrDefault(Locations.SectionName, Locations.SaveFolder, null);
			if (!string.IsNullOrEmpty(configured)) {
				try {
					if (Path.IsPathRooted(configured)) {
						string resolved = Path.GetFullPath(configured);
						return resolved;
					}
					if (iniPath != null) {
						string iniDir = Path.GetDirectoryName(Path.GetFullPath(iniPath));
						string resolved = Path.GetFullPath(Path.Combine(iniDir, configured));
						SetValue(Locations.SectionName, Locations.SaveFolder, resolved);
						SaveSettings();
						return resolved;
					}
					string cwdResolved = Path.GetFullPath(configured);
					return cwdResolved;
				} catch {
				}
			}

			if (iniPath != null) {
				string saveFolderFromIni = ReadIniValue(iniPath, "locations", "SaveFolder");
				if (string.IsNullOrEmpty(saveFolderFromIni)) {
					saveFolderFromIni = ReadIniValue(iniPath, "Locations", "SaveFolder");
				}
				if (!string.IsNullOrEmpty(saveFolderFromIni)) {
					try {
						string resolved;
						if (Path.IsPathRooted(saveFolderFromIni)) {
							resolved = Path.GetFullPath(saveFolderFromIni);
						} else {
							string iniDir = Path.GetDirectoryName(Path.GetFullPath(iniPath));
							resolved = Path.GetFullPath(Path.Combine(iniDir, saveFolderFromIni));
						}
						SetValue(Locations.SectionName, Locations.SaveFolder, resolved);
						SaveSettings();
						return resolved;
					} catch {
					}
				}

				bool standalone = GetSettingsValueOrDefault("locations", "useStandaloneMode", "false") == "true";
				if (string.IsNullOrEmpty(GetSettingsValueOrDefault("locations", "useStandaloneMode", null)) &&
					string.IsNullOrEmpty(GetSettingsValueOrDefault("Locations", "useStandaloneMode", null))) {
					// Check ini directly
					string su = ReadIniValue(iniPath, "locations", "useStandaloneMode");
					if (string.IsNullOrEmpty(su)) {
						su = ReadIniValue(iniPath, "Locations", "useStandaloneMode");
					}
					standalone = !string.IsNullOrEmpty(su) && su.Trim().ToLower() == "true";
				}

				string iniDirBase = Path.GetDirectoryName(Path.GetFullPath(iniPath));
				if (standalone) {
					string defaultPath = Path.GetFullPath(Path.Combine(iniDirBase, "Saves"));
					SetValue(Locations.SectionName, Locations.SaveFolder, defaultPath);
					SaveSettings();
					return defaultPath;
				}

				string civ3FromIni = ReadIniValue(iniPath, "locations", "civ3InstallDir");
				if (string.IsNullOrEmpty(civ3FromIni)) {
					civ3FromIni = ReadIniValue(iniPath, "Locations", "civ3InstallDir");
				}
				if (string.IsNullOrEmpty(civ3FromIni)) {
					civ3FromIni = GetSettingsValueOrDefault("locations", "civ3InstallDir", null);
				}
				if (string.IsNullOrEmpty(civ3FromIni)) {
					civ3FromIni = GetSettingsValueOrDefault("Locations", "civ3InstallDir", null);
				}
				if (!string.IsNullOrEmpty(civ3FromIni)) {
					try {
						if (!Path.IsPathRooted(civ3FromIni)) {
							civ3FromIni = Path.GetFullPath(Path.Combine(iniDirBase, civ3FromIni));
						} else {
							civ3FromIni = Path.GetFullPath(civ3FromIni);
						}
						string defaultPath = Path.Combine(civ3FromIni, "Conquests", "Saves");
						SetValue(Locations.SectionName, Locations.SaveFolder, defaultPath);
						SaveSettings();
						return defaultPath;
					} catch {
					}
				}

				string defaultPathFallback = Path.GetFullPath(Path.Combine(iniDirBase, "Saves"));
				SetValue(Locations.SectionName, Locations.SaveFolder, defaultPathFallback);
				SaveSettings();
				return defaultPathFallback;
			}

			// Last resort
			bool standaloneLastResort = GetSettingsValueOrDefault("locations", "useStandaloneMode", "false") == "true";
			string lastResort;
			if (standaloneLastResort) {
				lastResort = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "C7", "Saves"));
			} else {
				string civ3 = GetSettingsValueOrDefault("locations", "civ3InstallDir", null);
				if (!string.IsNullOrEmpty(civ3)) {
					lastResort = Path.Combine(civ3, "Conquests", "Saves");
				} else {
					lastResort = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "C7", "Saves"));
				}
			}
			SetValue(Locations.SectionName, Locations.SaveFolder, lastResort);
			SaveSettings();
			return lastResort;
		}

		public static void SetSaveFolder(string path) {
			SetValue(Locations.SectionName, Locations.SaveFolder, path);
			SaveSettings();
		}

		public static string GetLatestSave() {
			return GetSettingsValueOrDefault(LastGame.SectionName, LastGame.LatestSave, null);
		}

		public static void SetLatestSave(string path) {
			SetValue(LastGame.SectionName, LastGame.LatestSave, path);
			SaveSettings();
		}

		public static string GetLastWorldSave() {
			return GetSettingsValueOrDefault(LastGame.SectionName, LastGame.LastWorldSave, null);
		}

		public static void SetLastWorldSave(string path) {
			SetValue(LastGame.SectionName, LastGame.LastWorldSave, path);
			SaveSettings();
		}
	}
}
