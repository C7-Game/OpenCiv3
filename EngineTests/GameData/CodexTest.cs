using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using QueryCiv3;
using Xunit;

namespace EngineTests.GameData;

public class CodexTest {
	[Fact]
	public void TestParseConvertsLinksToMarkdown() {
		const string sample = """
		; comments are ignored

		#TECH_Advanced_Flight
		^
		^
		^{New Ability} $LINK<Workers=PRTO_Worker> can build $LINK<radar towers=GCON_Radar_Towers>.
		^
		^Long paragraph that is wrapped across
		several lines and should be rejoined as one paragraph.
		""";

		Codex codex = Codex.Parse(sample);
		CodexEntry entry = codex.GetEntry("TECH_Advanced_Flight");

		Assert.NotNull(entry);
		Assert.Single(entry.Pages);
		Assert.Contains("[Workers](key:PRTO_Worker)", entry.Pages[0].Body);
		Assert.Contains("[radar towers](key:GCON_Radar_Towers)", entry.Pages[0].Body);
		Assert.DoesNotContain("$LINK", entry.Pages[0].Body);
		Assert.Contains("can build", entry.Pages[0].Body);
		Assert.Contains("{New Ability}", entry.Pages[0].Body);
		Assert.Null(entry.Pages[0].Title);
		Assert.Contains("Long paragraph that is wrapped across several lines and should be rejoined as one paragraph.", entry.Pages[0].Body);
	}

	[Fact]
	public void TestParseMarksEachCaretLineAsNewParagraph() {
		const string sample = """
		#BLDG_Barracks
		^
		^A city with a Barracks produces veteran ground units
		completely in one turn.
		^A city with a Barracks can be used to upgrade ground units.
		""";

		Codex codex = Codex.Parse(sample);
		CodexEntry entry = codex.GetEntry("BLDG_Barracks");

		Assert.NotNull(entry);
		Assert.Single(entry.Pages);
		Assert.Equal("A city with a Barracks produces veteran ground units completely in one turn.\n\nA city with a Barracks can be used to upgrade ground units.", entry.Pages[0].Body);
	}

	[Fact]
	public void TestParseSecondaryPages() {
		const string sample = """
		#GCON_Hotkeys_Units
		Unit Hotkeys
		^
		^Press the number keys to select a unit.
		#DESC_GCON_Hotkeys_Units
		^{General Unit Commands}
		^
		^Press a number key to center the map on that unit.
		""";

		Codex codex = Codex.Parse(sample);
		CodexEntry entry = codex.GetEntry("GCON_Hotkeys_Units");

		Assert.NotNull(entry);
		Assert.Equal("Unit Hotkeys", entry.DisplayName);
		Assert.Equal(2, entry.Pages.Count);
		Assert.Equal("General Unit Commands", entry.Pages[1].Title);
		Assert.Contains("Press a number key to center the map", entry.Pages[1].Body);
	}

	[Fact]
	public void TestParseTrimsWhitespaceFromKeys() {
		const string sample = """
		#GCON_Enslavement 
		Enslavement
		^
		^Barbarian units can capture workers.
		""";

		Codex codex = Codex.Parse(sample);
		CodexEntry entry = codex.GetEntry("GCON_Enslavement ");

		Assert.NotNull(entry);
		Assert.Single(codex.Entries);
	}

	[Fact]
	public void TestParseLinkToTrailingSpaceKeyResolves() {
		const string sample = """
		#GCON_Enslavement 
		Enslavement
		^
		^Warriors may $LINK<enslave=GCON_Enslavement >.
		""";

		Codex codex = Codex.Parse(sample);
		CodexEntry entry = codex.GetEntry("GCON_Enslavement");

		Assert.NotNull(entry);
		Assert.Contains("[enslave](key:GCON_Enslavement)", entry.Pages[0].Body);
	}

	[Fact]
	public void TestParseGameConceptKeysBlock() {
		const string sample = """
		; header
		#GAME_CONCEPTS_KEYS
		GCON_Corruption
		GCON_Combat

		#GAME_CONCEPTS
		^
		#GCON_Combat
		Combat
		^
		^Combat resolves attacks.
		""";

		Codex codex = Codex.Parse(sample);

		Assert.Contains("GCON_Corruption", codex.GameConceptKeys);
		Assert.Contains("GCON_Combat", codex.GameConceptKeys);
		Assert.NotNull(codex.GetEntry("GCON_Combat"));
		Assert.Null(codex.GetEntry("GAME_CONCEPTS"));
	}

	[Fact]
	public void TestParseWindows1252Characters() {
		Encoding windows1252 = Encoding.GetEncoding(1252);
		byte[] bytes = windows1252.GetBytes("#GCON_Test\nConcept\n^\n^Dash \u2019 and \u201Cquote\u201D and \u00C0.\n");
		Codex codex = Codex.Parse(windows1252.GetString(bytes));

		Assert.NotNull(codex.GetEntry("GCON_Test"));
		Assert.Contains("\u2019 and \u201Cquote\u201D and \u00C0", codex.GetEntry("GCON_Test").Pages[0].Body);
	}

	[Fact]
	public void TestMissingEntryReturnsNull() {
		Codex codex = Codex.Parse("");
		Assert.Null(codex.GetEntry("TECH_Nonexistent"));
	}

	[Fact]
	public void TestBlankLineDoesNotSetDisplayName() {
		const string sample = """
		#TECH_Test
		^
		^Body text.
		""";

		Codex codex = Codex.Parse(sample);
		CodexEntry entry = codex.GetEntry("TECH_Test");

		Assert.NotNull(entry);
		Assert.Null(entry.DisplayName);
	}

	[SkippableFact]
	public void TestImportAttachesResolvableKeys() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		string scenarioPath = Path.Combine(Civ3Location.GetCiv3Path(), "Conquests/Conquests", "2 Rise of Rome.biq");
		Skip.If(!File.Exists(scenarioPath), "Rise of Rome scenario not present in this Civ3 installation.");

		EngineStorage.animationsEnabled = false;

		Func<string, string> getTextPath = relativeModPath => {
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				relativeModPath = relativeModPath.Replace("\\conquests\\", "/Conquests/");
			}
			return Path.GetFullPath(Path.Combine(Civ3Location.GetCiv3Path(), "Conquests/Conquests", relativeModPath, "Text"));
		};

		SaveGame save = ImportCiv3.ImportBiq(scenarioPath, Path.Combine(Civ3Location.GetCiv3Path(), "Conquests", "conquests.biq"),
			realm => Path.Combine(getTextPath(realm), "PediaIcons.txt"),
			realm => Path.Combine(Civ3Location.GetCiv3Path(), "Conquests", "Text", "Civilopedia.txt"));

		Codex codex = save.Codex;
		Assert.NotNull(codex);
		Assert.NotEmpty(save.UnitPrototypes);
		Assert.NotEmpty(save.Buildings);
		Assert.NotEmpty(save.Civilizations);

		int resolvedUnits = 0;
		foreach (SaveUnitPrototype proto in save.UnitPrototypes) {
			Assert.NotEmpty(proto.civilopediaEntry);
			if (codex.GetEntry(proto.civilopediaEntry) is not null) {
				resolvedUnits++;
			}
		}
		foreach (SaveBuilding building in save.Buildings) {
			Assert.NotEmpty(building.civilopediaEntry);
		}
		foreach (Civilization civ in save.Civilizations) {
			Assert.NotEmpty(civ.civilopediaEntry);
		}

		Assert.True(resolvedUnits > save.UnitPrototypes.Count / 2, "most units should resolve to a civilopedia entry");
		Assert.NotNull(codex.GetEntry("PRTO_Settler"));
		Assert.NotNull(codex.GetEntry("BLDG_Barracks"));
		Assert.NotNull(codex.GetEntry("RACE_AMERICAN"));
		Assert.Null(codex.GetEntry("PRTO_Fire_Catapult"));
	}

	[SkippableFact]
	public void TestParseRealCivilopediaFile() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		string path = Path.Combine(Civ3Location.GetCiv3Path(), "Conquests", "Text", "Civilopedia.txt");
		Codex codex = new Codex(path);

		Assert.NotEmpty(codex.Entries);
		Assert.True(codex.Entries.Count > 400, $"parsed {codex.Entries.Count} entries");
		Assert.NotEmpty(codex.GameConceptKeys);
		Assert.NotNull(codex.GetEntry("TECH_Advanced_Flight"));
		Assert.NotNull(codex.GetEntry("PRTO_Settler"));
		Assert.NotNull(codex.GetEntry("GCON_Enslavement"));
		Assert.Equal("Enslavement", codex.GetEntry("GCON_Enslavement").DisplayName);
		Assert.NotNull(codex.GetEntry("RACE_AMERICAN"));

		Assert.Contains("[Workers](key:PRTO_Worker)", codex.GetEntry("TECH_Advanced_Flight").Pages[0].Body);
		Assert.DoesNotContain("$LINK", codex.GetEntry("TECH_Advanced_Flight").Pages[0].Body);

		CodexEntry hotkeys = codex.GetEntry("GCON_Hotkeys_Units");
		Assert.NotNull(hotkeys);
		Assert.True(hotkeys.Pages.Count > 1);
		Assert.Equal("General Unit Commands", hotkeys.Pages[1].Title);

		Assert.DoesNotContain(codex.Entries.Keys, key => key != key.Trim());
		Assert.False(codex.Entries.ContainsKey("GAME_CONCEPTS"));
	}
}
