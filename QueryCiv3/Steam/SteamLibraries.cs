using System;
using System.Collections.Generic;
using System.IO;

namespace QueryCiv3.Steam;

public static class SteamLibraries {
	public const string Civ3AppId = "3910";

	// Reads <steamRoot>/steamapps/libraryfolders.vdf. Returns an empty
	// list when the file is missing or unusable; a broken config file
	// must never break the Civ3 lookup.
	public static List<SteamLibrary> ReadLibraryFolders(string steamRoot) {
		List<SteamLibrary> result = new List<SteamLibrary>();
		try {
			string file = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
			if (!File.Exists(file)) {
				return result;
			}
			VdfNode root = VdfParser.Parse(File.ReadAllText(file));
			VdfNode folders = root.Find("libraryfolders");
			if (folders == null) {
				return result;
			}
			foreach (VdfNode entry in folders.Children) {
				if (entry.Value != null) {
					// Pre-2021 format: plain "BaseInstallFolder_N" values
					// sitting directly under "LibraryFolders".
					if (entry.Name.StartsWith("BaseInstallFolder_", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(entry.Value)) {
						result.Add(new SteamLibrary { Path = entry.Value });
					}
					continue;
				}
				VdfNode pathNode = entry.Find("path");
				if (pathNode == null || string.IsNullOrEmpty(pathNode.Value)) {
					continue;
				}
				SteamLibrary lib = new SteamLibrary { Path = pathNode.Value };
				VdfNode apps = entry.Find("apps");
				if (apps != null) {
					foreach (VdfNode app in apps.Children) {
						lib.Apps.Add(app.Name);
					}
				}
				result.Add(lib);
			}
		} catch (Exception) {
			// Intentionally swallow: callers fall back to other locations.
			return new List<SteamLibrary>();
		}
		return result;
	}

	// Legacy fallback from the original issue: older Steam clients list
	// extra install folders as BaseInstallFolder_N entries in
	// <steamRoot>/config/config.vdf under
	// InstallConfigStore/Software/valve/Steam.
	public static List<string> ReadLegacyConfigFolders(string steamRoot) {
		List<string> result = new List<string>();
		try {
			string file = Path.Combine(steamRoot, "config", "config.vdf");
			if (!File.Exists(file)) {
				return result;
			}
			VdfNode node = VdfParser.Parse(File.ReadAllText(file));
			foreach (string step in new[] { "InstallConfigStore", "Software", "valve", "Steam" }) {
				node = node?.Find(step);
				if (node == null) {
					return result;
				}
			}
			foreach (VdfNode child in node.Children) {
				if (child.Value != null
					&& child.Name.StartsWith("BaseInstallFolder_", StringComparison.OrdinalIgnoreCase)
					&& !string.IsNullOrEmpty(child.Value)) {
					result.Add(child.Value);
				}
			}
		} catch (Exception) {
			return new List<string>();
		}
		return result;
	}
}
