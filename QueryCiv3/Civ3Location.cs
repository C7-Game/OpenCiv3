using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using QueryCiv3.Steam;

namespace QueryCiv3;

public class Civ3Location {
	public static string GetHome() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

	public static bool FolderIsCiv3(DirectoryInfo di) {
		return di.EnumerateFiles().Any(f => f.Name == "civ3id.mb");
	}

	public static string GetCiv3Path() {
		if (EnvVarFinder.Find(out string civ3Path))
			return civ3Path;

		if (RegistryFinder.Find(out string regPath))
			return regPath;

		if (SteamInstallFinder.Find(out string steamPath))
			return steamPath;

		return "/civ3/path/not/found";
	}

	private static class EnvVarFinder {
		private static string ConvertUnixVarsToWindowsVars(string input) {
			if (string.IsNullOrEmpty(input)) return input;

			// Replace all instances of $VARIABLE with %VARIABLE%
			return Regex.Replace(input, @"\$(\w+)", match => $"%{match.Groups[1].Value}%");
		}

		private static string GetExpandedPath(string path) {
			bool isUnixLike = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
			if (isUnixLike && path[0] == '~') path = Civ3Location.GetHome() + path.Substring(1);
			if (isUnixLike) path = ConvertUnixVarsToWindowsVars(path);
			path = Environment.ExpandEnvironmentVariables(path);
			path = Path.GetFullPath(path);
			return path;
		}

		/// <summary>
		/// Use CIV3_HOME env var if present
		/// </summary>
		public static bool Find(out string civ3Path) {
			civ3Path = null;

			string envPath = Environment.GetEnvironmentVariable("CIV3_HOME");
			if (envPath != null) {
				civ3Path = GetExpandedPath(envPath);
				return true;
			}

			return false;
		}
	}

	private static class RegistryFinder {
		private static readonly string RegistryKey
			= @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Infogrames Interactive\Civilization III";

		public static bool Find(out string regPath) {
			regPath = null;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				// Look up in Windows registry if present
				regPath = (string)Microsoft.Win32.Registry.GetValue(RegistryKey, "install_path", "");
				if (!string.IsNullOrEmpty(regPath)) {
					return true;
				}
			}

			return false;
		}
	}
}
