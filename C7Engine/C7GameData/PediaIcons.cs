
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;

namespace C7GameData {

	// A class for interacting with the PediaIcons.txt file used in scenarios.
	class PediaIcons {
		private static readonly ILogger log = Log.ForContext<PediaIcons>();

		// A mapping from the civilopedia entry name (like PRTO_Spearman or
		// PRTO_Legionary_II) to the name used in the art directory (Spearman
		// and Legionary)
		private readonly Dictionary<string, string> unitArtMapping = new();

		// A mapping from the civilopedia entry name (like TECH_Map_Making) to
		// the small icon used in the science advisor display.
		private readonly Dictionary<string, string> techSmallIconMapping = new();

		// A mapping from the all caps name of a race (BABYLON, GERMANS, 
		// RUSSIAN, etc) to the art file with happy/neutral/mad images of the
		// leader in each era, like `art\advisors\LZ_all.pcx`.
		private readonly Dictionary<string, string> raceToArtMapping = new();

		// A mapping from the building civilopedia name to the row within the
		// building icon art file.
		public readonly Dictionary<string, int> buildingToRowNumberMapping = new();

		private readonly Dictionary<string, string> civilopediaLargeIcons = new();
		private readonly Dictionary<string, string> civilopediaSmallIcons = new();

		private readonly Dictionary<string, int> unitSmallIconsIndex = new();

		private readonly string pediaIconsPath;

		public PediaIcons(string path) {
			pediaIconsPath = path;
			if (string.IsNullOrEmpty(path) || !File.Exists(path)) {
				log.Warning($"PediaIcons file '{path}' not found; importing without icon/art mappings");
				return;
			}

			string[] lines = File.ReadAllLines(path);

			string animNamePrefix = "#ANIMNAME_";
			for (int i = 0; i < lines.Length - 1; ++i) {
				if (lines[i].StartsWith(animNamePrefix)) {
					string civilopediaName = lines[i].Substring(animNamePrefix.Length);

					string artName = lines[i + 1];
					unitArtMapping.TryAdd(civilopediaName, artName);

					continue;
				}

				if (lines[i].StartsWith("#TECH") && !lines[i].EndsWith("_LARGE")) {
					// Drop the # from the line to get the civilopedia name
					// and then the next line is the icon path.
					techSmallIconMapping[lines[i].Substring(1)] = lines[i + 1];
				}

				if (lines[i].StartsWith("#RACE") && i + 2 < lines.Length) {
					// +2 because the line at +1 is the leaderheads neutral
					// victory image.
					raceToArtMapping[lines[i]] = lines[i + 2];
				}

				if (lines[i].StartsWith("#ICON_BLDG") && i + 2 < lines.Length) {
					// +2 because +1 specifies if the building has different
					// columns for culture groups or by era. We don't try to
					// support that yet.
					buildingToRowNumberMapping[lines[i].Substring(6)] = Int32.Parse(lines[i + 2]);
				}

				if (lines[i].StartsWith("#UNITICON_PRTO") && i + 1 < lines.Length) {
					unitSmallIconsIndex[lines[i].Substring(10)] = Int32.Parse(lines[i + 1]);
				}

				if (lines[i].StartsWith("#ICON_PRTO") && i + 2 < lines.Length) {
					civilopediaLargeIcons[lines[i].Substring(6)] = lines[i + 1];
					civilopediaSmallIcons[lines[i].Substring(6)] = lines[i + 2];
				}
			}
		}

		public string GetTechIconPath(string civilopediaEntry) {
			if (techSmallIconMapping.TryGetValue(civilopediaEntry, out string iconPath)) {
				return iconPath;
			}
			log.Warning($"Could not find #TECH_{civilopediaEntry} in PediaIcons file '{pediaIconsPath}'");
			return null;
		}

		public int GetBuildingRowNumber(string civilopediaEntry) {
			if (buildingToRowNumberMapping.TryGetValue(civilopediaEntry, out int row)) {
				return row;
			}
			log.Warning($"Could not find #ICON_BLDG_{civilopediaEntry} in PediaIcons file '{pediaIconsPath}'");
			return 0;
		}

		public MainArt GetUnitMainArt(string civilopediaEntry) {
			if (!unitArtMapping.TryGetValue(civilopediaEntry, out string artName)) {
				log.Warning($"Could not find #ANIMNAME_{civilopediaEntry} in PediaIcons file '{pediaIconsPath}'");
				string fallbackKey = unitArtMapping.Keys.FirstOrDefault(e => e.StartsWith(civilopediaEntry));
				if (fallbackKey is not null) {
					artName = unitArtMapping[fallbackKey].Replace($"{civilopediaEntry}_", "");
				}
			}

			var variations = unitArtMapping
				.Where(e => e.Key != civilopediaEntry && e.Key.StartsWith(civilopediaEntry))
				.ToDictionary(e => e.Key.Replace($"{civilopediaEntry}_", ""), e => e.Value);

			return new MainArt() {
				defaultName = artName,
				variations = variations.Count > 0 ? variations : null,
			};
		}

		public ThumbNailArt GetUnitThumbnailArt(string civilopediaEntry, int index) {
			var variations = unitSmallIconsIndex
				.Where(e => e.Key != civilopediaEntry && e.Key.StartsWith(civilopediaEntry))
				.ToDictionary(e => e.Key.Replace($"{civilopediaEntry}_", ""), e => e.Value);

			return new ThumbNailArt() {
				defaultIndex = index,
				variations = variations.Count > 0 ? variations : null,
			};
		}

		public PediaArt GetUnitCivilopediaArt(string civilopediaEntry) {
			return new PediaArt() {
				large = civilopediaLargeIcons.TryGetValue(civilopediaEntry, out string large) ? large : null,
				small = civilopediaSmallIcons.TryGetValue(civilopediaEntry, out string small) ? small : null,
			};
		}

		public string GetLeaderArtName(string civilopediaEntry) {
			string key = "#" + civilopediaEntry.ToUpper();
			if (!raceToArtMapping.ContainsKey(key)) {
				return null;
			}
			return raceToArtMapping[key];
		}
	}

}
