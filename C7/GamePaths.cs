using Godot;
using System.IO;
using C7Engine.Lua;

public static class GamePaths {
	// Base directory for finding data files (Lua/, Text/, Assets/).
	// In the editor this is "res://". In exports, it's the directory
	// containing the executable — on macOS, navigated out of the .app bundle.
	private static string _baseDir;
	public static string BaseDir {
		get {
			if (_baseDir == null) {
				if (OS.HasFeature("editor")) {
					_baseDir = "";
				} else {
					string exeDir = OS.GetExecutablePath().GetBaseDir();
					// On macOS the exe is inside *.app/Contents/MacOS/;
					// data files live alongside the .app bundle.
					if (exeDir.Contains(".app/Contents/MacOS")) {
						_baseDir = Path.GetFullPath(Path.Combine(exeDir, "..", "..", "..")) + "/";
					} else {
						_baseDir = exeDir + "/";
					}
				}
			}
			return _baseDir;
		}
	}

	public static GameMode.Config GameMode {
		get => C7Engine.C7Settings.UseStandaloneMode() ? standalone : basic;
	}

	private static string _writableDir;

	/// <summary>
	/// A directory we can write user-specific files (C7.ini, log.txt) to.
	///
	/// We prefer the directory holding the game so that portable installs keep
	/// their settings and logs together with the game. If that directory isn't
	/// writable (e.g. the game lives in /Applications) we fall back to the
	/// per-user data directory that Godot provides.
	///
	/// This matters because the process' current working directory can't be
	/// used for this: a macOS app launched from Finder has "/" as its working
	/// directory, and Windows installs under Program Files are read-only.
	/// </summary>
	public static string WritableDir {
		get {
			if (_writableDir == null) {
				_writableDir = CanWriteTo(BaseDir) ? BaseDir : ProjectSettings.GlobalizePath("user://");
			}
			return _writableDir;
		}
	}

	private static bool CanWriteTo(string dir) {
		try {
			string probe = Path.Combine(dir, ".openciv3-write-probe");
			File.WriteAllText(probe, "");
			File.Delete(probe);
			return true;
		} catch (System.Exception) {
			return false;
		}
	}

	public static string SettingsFilePath {
		get {
			// A C7.ini shipped alongside the game always wins, so a
			// preconfigured install keeps its settings even if the directory
			// itself is read-only.
			string shipped = Path.Combine(BaseDir, "C7.ini");
			return File.Exists(shipped) ? shipped : Path.Combine(WritableDir, "C7.ini");
		}
	}

	public static string LogFilePath => Path.Combine(WritableDir, "log.txt");

	public static string GameModesDir => Path.Combine(BaseDir, "Lua");
	public static GameMode.Config basic = new("civ3");
	public static GameMode.Config standalone = new("civ3", ["standalone"]);

	// For now this needs to get passed to QueryCiv3 when importing.
	public static string DefaultBicPath { get => Util.GetCiv3Path() + "/Conquests/conquests.biq"; }
}
