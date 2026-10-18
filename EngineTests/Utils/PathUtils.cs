using System;
using System.IO;
using System.Linq;
using QueryCiv3;

namespace EngineTests.Utils;

public class PathUtils {
	private static readonly string C7GameDataTestsFolderName = "EngineTests";

	public static string getBasePath(string file) => Path.Combine(testDirectory, file);

	public static string getDataPath(string file) => Path.Combine(testDirectory, "data", file);

	public static string GameModesDir => getBasePath("../C7/Lua/");

	public static string defaultBicPath {
		get => Path.Combine(Civ3Location.GetCiv3Path(), "Conquests", "conquests.biq");
	}

	public static string defaultPediaIconsPath {
		get => Path.Combine(Civ3Location.GetCiv3Path(), "Conquests", "Text", "PediaIcons.txt");
	}

	public static string defaultCivilopediaTextPath {
		get => Path.Combine(Civ3Location.GetCiv3Path(), "Conquests", "Text", "Civilopedia.txt");
	}

	public static string testDirectory {
		get {
			string[] parts = AppDomain.CurrentDomain.BaseDirectory.Split(Path.DirectorySeparatorChar);
			int pos = parts.Reverse().ToList().FindIndex(s => s == C7GameDataTestsFolderName);
			string up = string.Concat("..", Path.DirectorySeparatorChar);
			string relativePath = string.Concat(Enumerable.Repeat(up, pos - 1));
			return Path.GetFullPath(relativePath);
		}
	}
}
