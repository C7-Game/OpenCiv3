using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using C7Engine;
using C7GameData.Save;
using QueryCiv3;
using Xunit;

namespace EngineTests.Utils;

public class RemoteSaveLoader {
	protected static readonly string C7GameDataTestsFolderName = "EngineTests";
	private static string getDataPath(string file) => Path.Combine(testDirectory, "data", file);
	private static string defaultBicPath => Path.Combine(Civ3Location.GetCiv3Path(), "Conquests", "conquests.biq");
	private static string defaultPediaIconsPath => Path.Combine(Civ3Location.GetCiv3Path(), "Conquests", "Text", "PediaIcons.txt");
	private static string defaultCivilopediaTextPath => Path.Combine(Civ3Location.GetCiv3Path(), "Conquests", "Text", "Civilopedia.txt");

	private static string testDirectory {
		get {
			string[] parts = AppDomain.CurrentDomain.BaseDirectory.Split(Path.DirectorySeparatorChar);
			int pos = parts.Reverse().ToList().FindIndex(s => s == C7GameDataTestsFolderName);
			string up = string.Concat("..", Path.DirectorySeparatorChar);
			string relativePath = string.Concat(Enumerable.Repeat(up, pos - 1));
			return Path.GetFullPath(relativePath);
		}
	}

	protected static async Task<(SaveGame game, Exception ex, string savePath)> LoadGameAndData(string saveName, string savesFolder, string uri, string biqPath = "default", string pediaPath = "default") {
		string savesPath = getDataPath(savesFolder);
		Directory.CreateDirectory(savesPath);

		string savePath = Path.Combine(savesPath, saveName);
		using HttpClient client = new();
		byte[] fileData = await client.GetByteArrayAsync($"{uri}");
		if (uri.Contains("dropbox") && uri.EndsWith("&dl=0"))
			throw new Exception("Change the dl=0 to dl=1 at the end of the url to able to download the file, " +
								"otherwise we will get an error from the .biq not being able to find the correct file headers");
		await File.WriteAllBytesAsync(savePath, fileData);

		FileInfo saveFile = new DirectoryInfo(savesPath).GetFiles().First(f => f.Name == saveName);

		SaveGame game = null;
		Exception ex = Record.Exception(() => {
			game = SaveManager.LoadSave(saveFile.FullName, biqPath == "default" ? defaultBicPath : biqPath,
				(relativeModePath) => { return pediaPath == "default" ? defaultPediaIconsPath : pediaPath; },
				(relativeModePath) => { return defaultCivilopediaTextPath; });
		});

		return (game, ex, savePath);
	}
}
