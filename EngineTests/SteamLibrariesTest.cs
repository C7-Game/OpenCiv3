using System;
using System.Collections.Generic;
using System.IO;
using QueryCiv3;
using Xunit;

namespace EngineTests {
	public class SteamLibrariesTest {
		private const string ModernLibraryFolders = @"
""libraryfolders""
{
	""0""
	{
		""path""		""C:\\Program Files (x86)\\Steam""
		""label""		""""
		""apps""
		{
			""228980""		""123456""
		}
	}
	""1""
	{
		""path""		""D:\\Games\\Steam Library""
		""label""		""""
		""apps""
		{
			""3910""		""987654""
		}
	}
}
";

		private const string LegacyConfig = @"
""InstallConfigStore""
{
	""Software""
	{
		""valve""
		{
			""Steam""
			{
				""BaseInstallFolder_1""		""D:\\Games\\Steam Library""
				""BaseInstallFolder_2""		""G:\\Games\\Steam""
			}
		}
	}
}
";

		[Fact]
		public void ModernLibraryFolders_ParsesPathsAndApps() {
			string root = MakeSteamRoot(ModernLibraryFolders, null);
			List<SteamLibrary> libs = SteamLibraries.ReadLibraryFolders(root);

			Assert.Equal(2, libs.Count);
			Assert.Equal(@"C:\Program Files (x86)\Steam", libs[0].Path);
			Assert.DoesNotContain(SteamLibraries.Civ3AppId, libs[0].Apps);
			Assert.Equal(@"D:\Games\Steam Library", libs[1].Path);
			Assert.Contains(SteamLibraries.Civ3AppId, libs[1].Apps);
		}

		[Fact]
		public void LegacyConfig_ParsesBaseInstallFoldersInOrder() {
			string root = MakeSteamRoot(null, LegacyConfig);
			List<string> folders = SteamLibraries.ReadLegacyConfigFolders(root);

			Assert.Equal(new[] { @"D:\Games\Steam Library", @"G:\Games\Steam" }, folders);
		}

		[Fact]
		public void MissingOrBrokenFiles_ReturnEmptyInsteadOfThrowing() {
			string root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
			Assert.Empty(SteamLibraries.ReadLibraryFolders(root));
			Assert.Empty(SteamLibraries.ReadLegacyConfigFolders(root));

			Directory.CreateDirectory(Path.Combine(root, "steamapps"));
			File.WriteAllText(Path.Combine(root, "steamapps", "libraryfolders.vdf"), "\"libraryfolders\" { \"0\" { \"path\"");
			Assert.Empty(SteamLibraries.ReadLibraryFolders(root));
		}

		[Fact]
		public void CheckSteamLibraries_FindsCiv3InSecondaryLibrary() {
			string baseDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
			string steamRoot = Path.Combine(baseDir, "steam");
			string library = Path.Combine(baseDir, "library");
			string civ3Dir = Path.Combine(library, "steamapps", "common", "Sid Meier's Civilization III Complete");
			Directory.CreateDirectory(civ3Dir);
			File.WriteAllText(Path.Combine(civ3Dir, "civ3id.mb"), "");

			string vdf = "\"libraryfolders\" { \"0\" { \"path\" \"" + library.Replace("\\", "\\\\") + "\" \"apps\" { \"3910\" \"1\" } } }";
			Directory.CreateDirectory(Path.Combine(steamRoot, "steamapps"));
			File.WriteAllText(Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"), vdf);

			string found = Civ3Location.CheckSteamLibraries(new[] { steamRoot });
			Assert.Equal(civ3Dir, found);
		}

		[Fact]
		public void RootsContainingSteamApps_WalksUpFromExecutableDir() {
			string baseDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
			string steamRoot = Path.Combine(baseDir, "steam");
			string binDir = Path.Combine(steamRoot, "bin");
			Directory.CreateDirectory(Path.Combine(steamRoot, "steamapps"));
			Directory.CreateDirectory(binDir);

			Assert.Contains(steamRoot, Civ3Location.RootsContainingSteamApps(binDir));
			Assert.Empty(Civ3Location.RootsContainingSteamApps(baseDir));
		}

		private static string MakeSteamRoot(string libraryFoldersVdf, string configVdf) {
			string root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
			if (libraryFoldersVdf != null) {
				Directory.CreateDirectory(Path.Combine(root, "steamapps"));
				File.WriteAllText(Path.Combine(root, "steamapps", "libraryfolders.vdf"), libraryFoldersVdf);
			}
			if (configVdf != null) {
				Directory.CreateDirectory(Path.Combine(root, "config"));
				File.WriteAllText(Path.Combine(root, "config", "config.vdf"), configVdf);
			}
			return root;
		}
	}
}
