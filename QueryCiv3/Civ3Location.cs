using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using QueryCiv3.Steam;

namespace QueryCiv3 {
	public class Civ3Location {
		public static readonly string RegistryKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Infogrames Interactive\Civilization III";

		private static string SteamCommonDir() {
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
				return Path.Join(programFilesX86, "Steam\\steamapps\\common");
			}
			string home = GetHome();
			return home == null ? null : Path.Combine(home, "Library/Application Support/Steam/steamapps/common");
		}

		private static string GetHome() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

		private static bool FolderIsCiv3(DirectoryInfo di) {
			return di.EnumerateFiles().Any(f => f.Name == "civ3id.mb");
		}

		private static string ConvertUnixVarsToWindowsVars(string input) {
			if (string.IsNullOrEmpty(input)) return input;

			// Replace all instances of $VARIABLE with %VARIABLE%
			return Regex.Replace(input, @"\$(\w+)", match => $"%{match.Groups[1].Value}%");
		}

		private static string GetExpandedPath(string path) {
			bool isUnixLike = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
			if (isUnixLike && path[0] == '~') path = GetHome() + path.Substring(1);
			if (isUnixLike) path = ConvertUnixVarsToWindowsVars(path);
			path = Environment.ExpandEnvironmentVariables(path);
			path = Path.GetFullPath(path);
			return path;
		}

		public static string GetCiv3Path() {
			// Use CIV3_HOME env var if present
			string path = Environment.GetEnvironmentVariable("CIV3_HOME");
			if (path != null) { return GetExpandedPath(path); }

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				// Look up in Windows registry if present
				path = (string)Microsoft.Win32.Registry.GetValue(RegistryKey, "install_path", "");
				if (!string.IsNullOrEmpty(path)) {
					return path;
				}
			}

			// Check for a civ3 folder in steamapps/common
			string steam = SteamCommonDir();
			if (!string.IsNullOrEmpty(steam)) {
				DirectoryInfo root = new(steam);
				if (root.Exists) {
					foreach (DirectoryInfo di in root.GetDirectories()) {
						if (FolderIsCiv3(di)) {
							return di.FullName;
						}
					}
				}
			}

			// Check the other Steam libraries (#444): a Steam install outside
			// the default folder, or Civ3 in a secondary library.
			path = SteamInstallFinder.CheckSteamLibraries();
			if (path != null) {
				return path;
			}

			return "/civ3/path/not/found";
		}
	}
}
