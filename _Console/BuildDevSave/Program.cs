using System;
using System.IO;
using QueryCiv3;
using C7GameData;
using C7GameData.Save;

namespace BuildDevSave {
	class Program {

		static string GetCiv3Path { get => Civ3Location.GetCiv3Path(); }
		static string C7DefaultSaveDir { get => @"../../C7/Text"; }

		static void Info(string path, SaveGame save) {
			Console.WriteLine($"generated save file from {path}:");
			Console.WriteLine($"\tmap dimensions: with = {save.Map.tilesWide}, height = {save.Map.tilesTall}");
			Console.WriteLine($"\tfound {save.Civilizations.Count} civilizations");
			Console.WriteLine($"\tfound {save.Players.Count} players");
			Console.WriteLine($"\tfound {save.Cities.Count} cities");
			Console.WriteLine($"\tfound {save.UnitPrototypes.Count} unit prototypes");
			Console.WriteLine($"\tfound {save.Units.Count} units");
		}

		static void Main(string[] args) {
			if (args.Length < 1) {
				Console.WriteLine("provide civ3 SAV absolute path as command line argument");
				return;
			}
			DateTime start = DateTime.Now;
			string fullSavePath = args[0];
			string outputPath = Path.Combine(C7DefaultSaveDir, "c7-static-map-save.json");
			SaveGame output = ImportCiv3.ImportSav(fullSavePath, GetCiv3Path + @"/Conquests/conquests.biq", (scenarioSearchPath) => {
				return GetCiv3Path + @"/Conquests/Text/PediaIcons.txt";
			}, (scenarioSearchPath) => {
				return GetCiv3Path + @"/Conquests/Text/Civilopedia.txt";
			});
			output.Save(outputPath);
			DateTime stop = DateTime.Now;
			int elapsed = (stop - start).Milliseconds;
			Console.WriteLine($"finished generating save in {elapsed} milliseconds");
			Info(fullSavePath, output);
		}
	}
}
