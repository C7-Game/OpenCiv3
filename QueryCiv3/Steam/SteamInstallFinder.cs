using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace QueryCiv3.Steam;

/// <summary>
/// Looks for a Steam install of Civ3.
/// Searches:
/// - "Common directory" for typical installs
/// - Common alternative install dirs, Steam roots
/// - Steam executable path hints
/// </summary>
public static class SteamInstallFinder {
	private static readonly string SteamRegistryKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam";

	private static string SteamCommonDir() {
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
			string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
			return Path.Join(programFilesX86, "Steam\\steamapps\\common");
		}
		string home = Civ3Location.GetHome();
		return home == null ? null : Path.Combine(home, "Library/Application Support/Steam/steamapps/common");
	}

	/// <summary>
	/// Find a Steam install of Civ3.
	/// </summary>
	public static bool Find(out string fullName) {
		fullName = null;

		// Check for a civ3 folder in steamapps/common
		string steam = SteamCommonDir();
		if (!string.IsNullOrEmpty(steam)) {
			DirectoryInfo root = new(steam);
			if (root.Exists) {
				foreach (DirectoryInfo di in root.GetDirectories()) {
					if (Civ3Location.FolderIsCiv3(di)) {
						fullName = di.FullName;
						return true;
					}
				}
			}
		}

		// Check the other Steam libraries (#444): a Steam install outside
		// the default folder, or Civ3 in a secondary library.
		string path = CheckSteamLibraries();
		if (path != null) {
			fullName = path;
			return true;
		}

		return false;
	}

	// Steam roots worth asking about libraries: the registry InstallPath
	// on Windows (Steam is not always in Program Files), then the usual
	// per-OS locations, including the Flatpak and Snap sandboxes on
	// Linux. There is no reliable way to ask Steam where its config
	// lives, so this is a list of the common places; the last resort is
	// the directory of a `steam` executable found on PATH. Anyone who
	// installed Steam somewhere stranger than all of these is assumed
	// to know it and to set CIV3_HOME.
	private static IEnumerable<string> SteamRootCandidates() {
		HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		string home = Civ3Location.GetHome();

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
			string installPath = (string)Microsoft.Win32.Registry.GetValue(SteamRegistryKey, "InstallPath", "");
			if (!string.IsNullOrEmpty(installPath) && seen.Add(installPath)) {
				yield return installPath;
			}
			foreach (Environment.SpecialFolder folder in new[] { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles }) {
				string programFiles = Environment.GetFolderPath(folder);
				if (!string.IsNullOrEmpty(programFiles)) {
					string root = Path.Join(programFiles, "Steam");
					if (seen.Add(root)) {
						yield return root;
					}
				}
			}
		} else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
			if (home != null) {
				yield return Path.Combine(home, "Library/Application Support/Steam");
			}
		} else {
			if (home != null) {
				yield return Path.Combine(home, ".steam/root");
				yield return Path.Combine(home, ".steam/debian-installation");
				yield return Path.Combine(home, ".local/share/Steam");
				yield return Path.Combine(home, ".var/app/com.valvesoftware.Steam/.steam/root");
				yield return Path.Combine(home, "snap/steam/common/.local/share/Steam");
			}
		}

		string steamExe = FindSteamExecutable();
		if (steamExe != null) {
			foreach (string root in RootsContainingSteamApps(Path.GetDirectoryName(steamExe))) {
				if (seen.Add(root)) {
					yield return root;
				}
			}
			// `steam` on PATH is often a symlink or a small launcher
			// script; if it is a symlink, try where it points as well.
			string linkTargetDir = null;
			try {
				FileSystemInfo target = File.ResolveLinkTarget(steamExe, returnFinalTarget: true);
				if (target != null) {
					linkTargetDir = Path.GetDirectoryName(target.FullName);
				}
			} catch (Exception) {
				// Not a link, or not resolvable; the direct path was tried.
			}
			if (linkTargetDir != null) {
				foreach (string root in RootsContainingSteamApps(linkTargetDir)) {
					if (seen.Add(root)) {
						yield return root;
					}
				}
			}
		}
	}

	private static string FindSteamExecutable() {
		string fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "steam.exe" : "steam";
		string pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
		foreach (string dir in pathVar.Split(Path.PathSeparator)) {
			if (string.IsNullOrWhiteSpace(dir)) {
				continue;
			}
			string candidate = Path.Combine(dir.Trim(), fileName);
			if (File.Exists(candidate)) {
				return candidate;
			}
		}
		return null;
	}

	// Starting at a directory (typically where a `steam` executable
	// lives), walk up a few levels collecting directories that actually
	// contain a steamapps folder. Internal so tests can exercise it.
	internal static IEnumerable<string> RootsContainingSteamApps(string startDir) {
		if (string.IsNullOrEmpty(startDir)) {
			yield break;
		}
		DirectoryInfo dir = new DirectoryInfo(startDir);
		for (int i = 0; i < 4 && dir != null; i++, dir = dir.Parent) {
			if (Directory.Exists(Path.Combine(dir.FullName, "steamapps"))) {
				yield return dir.FullName;
			}
		}
	}

	private static string ScanSteamAppsCommon(string commonDir) {
		if (string.IsNullOrEmpty(commonDir)) {
			return null;
		}
		DirectoryInfo root = new(commonDir);
		if (!root.Exists) {
			return null;
		}
		foreach (DirectoryInfo di in root.GetDirectories()) {
			if (Civ3Location.FolderIsCiv3(di)) {
				return di.FullName;
			}
		}
		return null;
	}

	// Issue #444 fallback: Steam libraries other than the default one.
	// Libraries that libraryfolders.vdf says contain Civ3 (app 3910) are
	// checked first; after that, every listed library and every legacy
	// BaseInstallFolder is scanned the same way the default folder is,
	// because an empty app list is not proof Civ3 isn't there.
	internal static string CheckSteamLibraries() {
		return CheckSteamLibraries(SteamRootCandidates());
	}

	// Internal so EngineTests can point it at fixture Steam roots.
	internal static string CheckSteamLibraries(IEnumerable<string> steamRoots) {
		foreach (string steamRoot in steamRoots) {
			if (!Directory.Exists(steamRoot)) {
				continue;
			}

			List<SteamLibrary> libraries = SteamLibraries.ReadLibraryFolders(steamRoot);
			foreach (SteamLibrary lib in libraries.OrderByDescending(l => l.Apps.Contains(SteamLibraries.Civ3AppId))) {
				string hit = ScanSteamAppsCommon(Path.Combine(lib.Path, "steamapps", "common"));
				if (hit != null) {
					return hit;
				}
			}

			foreach (string folder in SteamLibraries.ReadLegacyConfigFolders(steamRoot)) {
				string hit = ScanSteamAppsCommon(Path.Combine(folder, "steamapps", "common"));
				if (hit != null) {
					return hit;
				}
			}
		}
		return null;
	}
}
