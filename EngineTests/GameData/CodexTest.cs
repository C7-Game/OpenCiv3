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
		Assert.Contains("[Workers](key:PRTO_Worker)", entry.Body);
		Assert.Contains("[radar towers](key:GCON_Radar_Towers)", entry.Body);
		Assert.DoesNotContain("$LINK", entry.Body);
		Assert.Contains("can build", entry.Body);
		Assert.Contains("**New Ability**", entry.Body);
		Assert.Contains("Long paragraph that is wrapped across several lines and should be rejoined as one paragraph.", entry.Body);
	}

	[Fact]
	public void TestBracketedTermsBecomeItalics() {
		const string sample = """
		#BLDG_University
		^
		^
		^[The University] increases the scientific research in its city by 50%.
		""";

		Codex codex = Codex.Parse(sample);
		CodexEntry entry = codex.GetEntry("BLDG_University");

		Assert.NotNull(entry);
		Assert.Contains("*The University* increases", entry.Body);
		Assert.DoesNotContain("[The University]", entry.Body);
	}

	[Fact]
	public void TestBracketedLinkBecomesLinkedItalics() {
		const string sample = """
		#GCON_Resources
		^A city can work [$LINK<Bonus Resources=GCON_ResourcesB>].
		""";

		Codex codex = Codex.Parse(sample);
		CodexEntry entry = codex.GetEntry("GCON_Resources");

		Assert.NotNull(entry);
		Assert.Contains("*[Bonus Resources](key:GCON_ResourcesB)*", entry.Body);
	}

	[Fact]
	public void TestBracedTermsBecomeBold() {
		const string sample = """
		#GCON_Resources
		^When a city {has} a luxury resource, it appears in the city's {Luxuries box}.
		""";

		Codex codex = Codex.Parse(sample);
		CodexEntry entry = codex.GetEntry("GCON_Resources");

		Assert.NotNull(entry);
		Assert.Contains("**has**", entry.Body);
		Assert.Contains("**Luxuries box**", entry.Body);
		Assert.DoesNotContain("{", entry.Body);
	}

	[Fact]
	public void TestWholeLineBraceBecomesHeading() {
		const string sample = """
		#GCON_Test
		^
		^{Section Title}
		^
		^A city {starves} when it has no food.
		""";

		Codex codex = Codex.Parse(sample);
		CodexEntry entry = codex.GetEntry("GCON_Test");

		Assert.NotNull(entry);
		Assert.Contains("## Section Title", entry.Body);
		Assert.Contains("**starves**", entry.Body);
		Assert.DoesNotContain("**Section Title**", entry.Body);
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
		Assert.Equal("A city with a Barracks produces veteran ground units completely in one turn.\n\nA city with a Barracks can be used to upgrade ground units.", entry.Body);
	}

	[Fact]
	public void TestParseDescBlockKeptSeparateFromBody() {
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
		Assert.Contains("Press the number keys to select a unit.", entry.Body);
		Assert.DoesNotContain("Press a number key to center the map", entry.Body);
		Assert.Contains("## General Unit Commands", entry.Description);
		Assert.DoesNotContain("{General Unit Commands}", entry.Description);
		Assert.Contains("Press a number key to center the map", entry.Description);
	}

	[Fact]
	public void TestParseColumnRowsPreservedInBody() {
		string sample =
			"#GCON_Happy_Faces\n" +
			"Happy Faces\n" +
			"^\n" +
			"^City improvements, wonders, entertainers, and luxuries produce faces.\n" +
			"^{These\t\t\t\t\t\t\t\tProduce this}\n" +
			"^* City improvements\t\t\t\t\t\tcontent faces\n" +
			"^* Wonders\t\t\t\t\t\t\t\tcontent faces\n";

		Codex codex = Codex.Parse(sample);
		CodexEntry entry = codex.GetEntry("GCON_Happy_Faces");

		Assert.NotNull(entry);
		Assert.Equal("Happy Faces", entry.DisplayName);
		Assert.Contains("**These\t\t\t\t\t\t\t\tProduce this**", entry.Body);
		Assert.Contains("* City improvements\t\t\t\t\t\tcontent faces", entry.Body);
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
		Assert.Contains("[enslave](key:GCON_Enslavement)", entry.Body);
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
		Assert.Contains("\u2019 and \u201Cquote\u201D and \u00C0", codex.GetEntry("GCON_Test").Body);
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
		Assert.Equal("Body text.", entry.Body);
	}

	[Fact]
	public void TestKeyLookupIsCaseInsensitive() {
		const string sample = """
		#PRTO_Nuke
		^Body text.
		""";

		Codex codex = Codex.Parse(sample);

		Assert.NotNull(codex.GetEntry("PRTO_Nuke"));
		Assert.Equal("Body text.", codex.GetEntry("PRTO_NUKE").Body);
	}

	[Fact]
	public void TestBodyAndDescriptionMergeWhenHeaderCaseDiffers() {
		const string sample = """
		#BLDG_Marketplace
		^Body text.
		#DESC_BLDG_MarketPlace
		^Description text.
		""";

		Codex codex = Codex.Parse(sample);
		CodexEntry entry = codex.GetEntry("BLDG_Marketplace");

		Assert.NotNull(entry);
		Assert.Equal("Body text.", entry.Body);
		Assert.Equal("Description text.", entry.Description);
	}

	[Fact]
	public void TestParagraphLineWithoutCaretAfterBlankLineIsBody() {
		const string sample = """
		#BLDG_Apollo_Project
		^
		^
		Completion of the Apollo Program.
		#DESC_BLDG_Apollo_Project
		^The program began in the 1960s.
		""";

		Codex codex = Codex.Parse(sample);
		CodexEntry entry = codex.GetEntry("BLDG_Apollo_Project");

		Assert.NotNull(entry);
		Assert.Null(entry.DisplayName);
		Assert.Equal("Completion of the Apollo Program.", entry.Body);
		Assert.Equal("The program began in the 1960s.", entry.Description);
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
	public void TestImportToleratesMissingPediaIcons() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		string scenarioPath = Path.Combine(Civ3Location.GetCiv3Path(), "Conquests/Conquests", "2 Rise of Rome.biq");
		Skip.If(!File.Exists(scenarioPath), "Rise of Rome scenario not present in this Civ3 installation.");

		EngineStorage.animationsEnabled = false;

		string missingPediaIcons = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "PediaIcons.txt");

		SaveGame save = ImportCiv3.ImportBiq(scenarioPath, Path.Combine(Civ3Location.GetCiv3Path(), "Conquests", "conquests.biq"),
			realm => missingPediaIcons,
			realm => Path.Combine(Civ3Location.GetCiv3Path(), "Conquests", "Text", "Civilopedia.txt"));

		Assert.NotEmpty(save.UnitPrototypes);
		Assert.NotEmpty(save.Buildings);
		Assert.NotEmpty(save.Civilizations);
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

		Assert.Contains("[Workers](key:PRTO_Worker)", codex.GetEntry("TECH_Advanced_Flight").Body);
		Assert.DoesNotContain("$LINK", codex.GetEntry("TECH_Advanced_Flight").Body);

		CodexEntry hotkeys = codex.GetEntry("GCON_Hotkeys_Units");
		Assert.NotNull(hotkeys);
		Assert.Contains("## Settler / Worker Actions", hotkeys.Body);
		Assert.Contains("## Air Missions", hotkeys.Body);
		Assert.DoesNotContain("## General Unit Commands", hotkeys.Body);
		Assert.Contains("## General Unit Commands", hotkeys.Description);
		Assert.DoesNotContain("{General Unit Commands}", hotkeys.Description);
		Assert.Contains("Hold (don't move)", hotkeys.Description);
		Assert.Contains("\t", hotkeys.Body);
		Assert.Contains("\t", hotkeys.Description);

		CodexEntry walls = codex.GetEntry("BLDG_Walls");
		Assert.NotNull(walls);
		Assert.Contains("land bombardment defense", walls.Body);
		Assert.Contains("50% defensive bonus", walls.Body);
		Assert.NotNull(walls.Description);
		Assert.Contains("city walls", walls.Description);

		CodexEntry happyFaces = codex.GetEntry("GCON_Happy_Faces");
		Assert.NotNull(happyFaces);
		Assert.Contains("**These", happyFaces.Body);
		Assert.Contains("\t", happyFaces.Body);

		Assert.DoesNotContain(codex.Entries.Keys, key => key != key.Trim());
		Assert.False(codex.Entries.ContainsKey("GAME_CONCEPTS"));
	}
}
