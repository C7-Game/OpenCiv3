using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using MoonSharp.Interpreter;
using QueryCiv3;
using QueryCiv3.Biq;
using Xunit;

namespace EngineTests.GameData;

public class LeaderDataTest : IClassFixture<SaveGameFixture> {
	SaveGameFixture fixture;

	public LeaderDataTest(SaveGameFixture fixture) {
		this.fixture = fixture;
	}

	[Fact]
	public void StandaloneRulesExposeScientificLeaderDefaults() {
		Assert.Equal(20, fixture.standaloneSaveGame.Rules.GoldenAgeDuration);
		Assert.True(fixture.standaloneSaveGame.Rules.AllowScientificLeaders);
		Assert.Equal(.03f, fixture.standaloneSaveGame.Rules.ScientificLeaderChance);
		Assert.Equal(.05f, fixture.standaloneSaveGame.Rules.ScientificTraitLeaderChance);
		Assert.Equal(2, fixture.standaloneSaveGame.Rules.MaxScientificLeaders);
		Assert.Equal(1, fixture.standaloneSaveGame.Rules.MaxMilitaryLeaders);
	}

	[Fact]
	public void BaseRulesetDefinesScientificLeaderRules() {
		using JsonDocument ruleset = JsonUtils.LoadBaseRuleset();
		JsonElement rules = ruleset.RootElement.GetProperty("rules");

		Assert.True(rules.GetProperty("allowScientificLeaders").GetBoolean());
		Assert.Equal(20, rules.GetProperty("goldenAgeDuration").GetInt32());
		Assert.Equal(0.03, rules.GetProperty("scientificLeaderChance").GetDouble());
		Assert.Equal(0.05, rules.GetProperty("scientificTraitLeaderChance").GetDouble());
		Assert.Equal(2, rules.GetProperty("maxScientificLeaders").GetInt32());
		Assert.Equal(1, rules.GetProperty("maxMilitaryLeaders").GetInt32());
	}

	[Fact]
	public void RulesDefaultToCommunitySeededLeaderValues() {
		Rules rules = new Rules();

		Assert.True(rules.AllowScientificLeaders);
		Assert.Equal(.03f, rules.ScientificLeaderChance);
		Assert.Equal(.05f, rules.ScientificTraitLeaderChance);
		Assert.Equal(2, rules.MaxScientificLeaders);
		Assert.Equal(1, rules.MaxMilitaryLeaders);
	}

	[Fact]
	public void BaseRulesetTagsLeaderPrototypeAsMilitary() {
		using JsonDocument ruleset = JsonUtils.LoadBaseRuleset();
		JsonElement leader = ruleset.RootElement.GetProperty("unitPrototypes")
			.EnumerateArray()
			.First(proto => proto.GetProperty("name").GetString() == "Leader");

		List<string> attributes = leader.GetProperty("attributes")
			.EnumerateArray()
			.Select(attribute => attribute.GetString())
			.ToList();

		Assert.Contains(UnitPrototype.MILITARY_LEADER_ATTRIBUTE, attributes);
	}

	[Fact]
	public void BaseLeaderPrototypeLoadsAsMilitaryLeader() {
		// Standalone mode has no Leader prototype: the standalone addon drops
		// every prototype that has no art replacement (see Lua/standalone/ruleset.lua).
		SaveUnitPrototype leader = fixture.saveGame.UnitPrototypes
			.Find(proto => proto.name == "Leader");

		Assert.NotNull(leader);
		Assert.Contains(SaveUnitPrototype.Flag.Leader, leader.flags);
		Assert.Contains(UnitPrototype.MILITARY_LEADER_ATTRIBUTE, leader.attributes);
	}

	[Fact]
	public void LeaderKindTagSurvivesConversionToGameData() {
		// The running game reads prototypes from GameData, not from the save, so
		// the tag has to make it across that conversion.
		UnitPrototype leader = fixture.saveGame.ToGameData(fixture.behaviors).unitPrototypes
			.Find(proto => proto.name == "Leader");

		Assert.NotNull(leader);
		Assert.Contains(SaveUnitPrototype.Flag.Leader, leader.flags);
		Assert.True(leader.HasAttribute(UnitPrototype.MILITARY_LEADER_ATTRIBUTE));
	}

	[Fact]
	public void PrototypeAttributesRoundTripThroughSave() {
		// A mod can add a leader kind the engine has never heard of, because the
		// kind is a tag rather than an enum member.
		SaveGame save = new SaveGame();
		save.UnitPrototypes.Add(new() { name = "Leader", attributes = ["theologicalLeader"] });

		SaveGame clone = save.Clone();

		Assert.Contains("theologicalLeader", clone.UnitPrototypes.First(p => p.name == "Leader").attributes);
	}

	[Fact]
	public void PrototypeAttributesAreQueryableFromLua() {
		// The texture layer reads prototype tags, so a ruleset script must be able
		// to call the accessor. Guards against the call silently never matching.
		(Script script, _) = GameMode.Load(PathUtils.GameModesDir, new GameMode.Config("civ3")).textures;
		UnitPrototype leader = new() {
			name = "Leader",
			attributes = [UnitPrototype.SCIENTIFIC_LEADER_ATTRIBUTE],
		};
		script.Globals["proto"] = UserData.Create(leader);

		DynValue hasTag = script.DoString($"return proto:HasAttribute('{UnitPrototype.SCIENTIFIC_LEADER_ATTRIBUTE}')");
		DynValue lacksTag = script.DoString($"return proto:HasAttribute('{UnitPrototype.MILITARY_LEADER_ATTRIBUTE}')");

		Assert.True(hasTag.Boolean);
		Assert.False(lacksTag.Boolean);
	}

	[Fact]
	public void ScientificLeaderResolvesSciArtRegardlessOfEra() {
		MapUnit unit = MakeLeaderArtUnit("ERAS_Middle_Ages", UnitPrototype.SCIENTIFIC_LEADER_ATTRIBUTE);

		Assert.Equal("SciLeader", unit.GetArtName());
	}

	[Fact]
	public void MilitaryLeaderUsesEraArt() {
		MapUnit unit = MakeLeaderArtUnit("ERAS_Middle_Ages", UnitPrototype.MILITARY_LEADER_ATTRIBUTE);

		Assert.Equal("Leader Middle Ages", unit.GetArtName());
	}

	[Fact]
	public void NonLeaderUnitStillUsesEraArt() {
		MapUnit unit = MakeLeaderArtUnit(era: "ERAS_Middle_Ages");

		Assert.Equal("Leader Middle Ages", unit.GetArtName());
	}

	[Fact]
	public void ScientificLeaderFallsBackToDefaultArtWhenSciVariationMissing() {
		MapUnit unit = MakeLeaderArtUnit("ERAS_Ancient_Times", UnitPrototype.SCIENTIFIC_LEADER_ATTRIBUTE);
		unit.unitType.art.mainArt.variations.Remove("SCI");

		Assert.Equal("Leader Ancient Times", unit.GetArtName());
	}

	[SkippableFact]
	public void SavImportReadsScientificLeaderData() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		string savPath = Path.Combine(PathUtils.getDataPath("saves"), "12345.SAV");
		Skip.If(!File.Exists(savPath), "Sample save 12345.SAV not present; run LoadSampleSaves to fetch it.");

		// The BIQ and sample SAV are Blast-compressed; Util.ReadFile handles the decompression.
		SavData savData = new SavData(Util.ReadFile(savPath), Util.ReadFile(PathUtils.defaultBicPath));
		SaveGame save = ImportCiv3.ImportSav(savPath, PathUtils.defaultBicPath, (_) => PathUtils.defaultPediaIconsPath);

		// The SAV's own GAME section is authoritative for the toggle, and the
		// campaign's RULE section provides the Age of Science duration.
		Assert.Equal(savData.Game.AllowScientificLeaders, save.Rules.AllowScientificLeaders);
		Assert.Equal(savData.Bic.Rule[0].GoldenAgeDuration, save.Rules.GoldenAgeDuration);

		// The pool comes from the campaign BIQ's RACE section, with one civ per
		// race in the same order, sized by RACE.NumberOfScientificLeaders.
		Assert.Equal(savData.Bic.Race.Length, save.Civilizations.Count);
		int totalScientificLeaders = 0;
		for (int i = 0; i < savData.Bic.Race.Length; ++i) {
			Assert.Equal(savData.Bic.Race[i].NumberOfScientificLeaders, save.Civilizations[i].scientificLeaderNames.Count);
			totalScientificLeaders += savData.Bic.Race[i].NumberOfScientificLeaders;
		}
		Assert.True(totalScientificLeaders > 0);
		Assert.Contains(save.Civilizations, civ => civ.scientificLeaderNames.Any(name => name.Length > 0));
	}

	[SkippableFact]
	public void SavImportMarksLeaderPrototypeFromPrtoLeaderFlag() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		string savPath = Path.Combine(PathUtils.getDataPath("saves"), "12345.SAV");
		Skip.If(!File.Exists(savPath), "Sample save 12345.SAV not present; run LoadSampleSaves to fetch it.");

		SavData savData = new SavData(Util.ReadFile(savPath), Util.ReadFile(PathUtils.defaultBicPath));
		SaveGame save = ImportCiv3.ImportSav(savPath, PathUtils.defaultBicPath, (_) => PathUtils.defaultPediaIconsPath);

		// A PRTO flagged Leader imports as a military leader prototype: the BIQ has
		// a single prototype and only ever spawns military leaders from it.
		List<PRTO> leaderProtos = savData.Bic.Prto.Where(prto => prto.Leader).ToList();
		Assert.NotEmpty(leaderProtos);

		foreach (PRTO prto in leaderProtos) {
			SaveUnitPrototype imported = save.UnitPrototypes.Find(proto => proto.name == prto.Name);
			Assert.NotNull(imported);
			Assert.Contains(SaveUnitPrototype.Flag.Leader, imported.flags);
			Assert.Contains(UnitPrototype.MILITARY_LEADER_ATTRIBUTE, imported.attributes);
		}

		// Prototypes without the flag stay ordinary units.
		SaveUnitPrototype warrior = save.UnitPrototypes.Find(proto => proto.name == "Warrior");
		Assert.DoesNotContain(SaveUnitPrototype.Flag.Leader, warrior.flags);
		Assert.DoesNotContain(UnitPrototype.MILITARY_LEADER_ATTRIBUTE, warrior.attributes);
	}

	private static MapUnit MakeLeaderArtUnit(string era, params string[] attributes) {
		Player player = new Player() {
			civilization = new Civilization("Rome"),
			eraCivilopediaName = era,
		};
		return new(ID.None("leader")) {
			name = "Great Scientist",
			unitType = new UnitPrototype {
				name = "Leader",
				attributes = new HashSet<string>(attributes),
				art = new Art {
					mainArt = new MainArt {
						defaultName = "Leader Ancient Times",
						variations = new Dictionary<string, string> {
							["ERAS_Ancient_Times"] = "Leader Ancient Times",
							["ERAS_Middle_Ages"] = "Leader Middle Ages",
							["SCI"] = "SciLeader",
						},
					},
				},
			},
			owner = player,
		};
	}
}
