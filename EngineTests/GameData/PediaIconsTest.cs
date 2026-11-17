using System.IO;
using C7GameData;
using EngineTests.Utils;
using QueryCiv3;
using Xunit;

namespace EngineTests.GameData;

public class PediaIconsTest {
	private static PediaIcons Parse(string contents) {
		string path = Path.GetTempFileName();
		File.WriteAllText(path, contents);
		try {
			return new PediaIcons(path);
		} finally {
			File.Delete(path);
		}
	}

	[Fact]
	public void BuildingIconsUseTheFirstEraVariant() {
		PediaIcons icons = Parse("""
		#ICON_BLDG_Barracks
		ERA
		1
		art\civilopedia\icons\buildings\barracksanclarge.pcx
		art\civilopedia\icons\buildings\barracksrenlarge.pcx
		art\civilopedia\icons\buildings\barracksindlarge.pcx
		art\civilopedia\icons\buildings\barracksmodlarge.pcx
		art\civilopedia\icons\buildings\barracksancsmall.pcx
		art\civilopedia\icons\buildings\barracksrensmall.pcx
		art\civilopedia\icons\buildings\barracksindsmall.pcx
		art\civilopedia\icons\buildings\barracksmodsmall.pcx
		""");

		Assert.Equal(@"art\civilopedia\icons\buildings\barracksanclarge.pcx", icons.GetCivilopediaIconPath("BLDG_Barracks"));
		Assert.Equal(@"art\civilopedia\icons\buildings\barracksancsmall.pcx", icons.GetCivilopediaSmallIconPath("BLDG_Barracks"));
	}

	[Fact]
	public void ResourceAndRaceIconsTakeLargeThenSmall() {
		PediaIcons icons = Parse("""
		#ICON_GOOD_Iron
		art\civilopedia\icons\resources\ironlarge.pcx
		art\civilopedia\icons\resources\ironsmall.pcx
		#ICON_RACE_AMERICAN
		art\civilopedia\icons\races\americanslarge.pcx
		art\civilopedia\icons\races\americanssmall.pcx
		""");

		Assert.Equal(@"art\civilopedia\icons\resources\ironlarge.pcx", icons.GetCivilopediaIconPath("GOOD_Iron"));
		Assert.Equal(@"art\civilopedia\icons\resources\ironsmall.pcx", icons.GetCivilopediaSmallIconPath("GOOD_Iron"));
		Assert.Equal(@"art\civilopedia\icons\races\americanslarge.pcx", icons.GetCivilopediaIconPath("RACE_AMERICAN"));
		Assert.Equal(@"art\civilopedia\icons\races\americanssmall.pcx", icons.GetCivilopediaSmallIconPath("RACE_AMERICAN"));
	}

	[Fact]
	public void UnitIconsAreAvailableForBothSizes() {
		PediaIcons icons = Parse("""
		#ICON_PRTO_Warrior
		art\civilopedia\icons\units\06warriorlarge.pcx
		art\civilopedia\icons\units\06warriorsmall.pcx
		""");

		PediaArt art = icons.GetUnitCivilopediaArt("PRTO_Warrior");
		Assert.Equal(@"art\civilopedia\icons\units\06warriorlarge.pcx", art.large);
		Assert.Equal(@"art\civilopedia\icons\units\06warriorsmall.pcx", art.small);
	}

	[Fact]
	public void TechIconsAreAvailableForBothSizes() {
		PediaIcons icons = Parse("""
		#TECH_Bronze_Working
		art\advisor\science_216.pcx
		#TECH_Bronze_Working_LARGE
		art\civilopedia\icons\techs\BronzeWorkinglarge.pcx
		""");

		Assert.Equal(@"art\advisor\science_216.pcx", icons.GetTechIconPath("TECH_Bronze_Working"));
		Assert.Equal(@"art\civilopedia\icons\techs\BronzeWorkinglarge.pcx", icons.GetTechCivilopediaIconPath("TECH_Bronze_Working"));
	}

	[Fact]
	public void IconsWithoutSizeInTheNameFallBackByPosition() {
		PediaIcons icons = Parse("""
		#ICON_GOOD_Wheat
		art\wheat1.pcx
		art\wheat2.pcx
		""");

		Assert.Equal(@"art\wheat1.pcx", icons.GetCivilopediaIconPath("GOOD_Wheat"));
		Assert.Equal(@"art\wheat2.pcx", icons.GetCivilopediaSmallIconPath("GOOD_Wheat"));
	}

	[Fact]
	public void IconLookupsIgnoreKeyCasing() {
		PediaIcons icons = Parse("""
		#ICON_RACE_MONGOLS
		art\civilopedia\icons\races\x_temujin civpedia lg.pcx
		art\civilopedia\icons\races\x_temujin civpedia sm.pcx
		""");

		Assert.Equal(@"art\civilopedia\icons\races\x_temujin civpedia lg.pcx", icons.GetCivilopediaIconPath("RACE_Mongols"));
		Assert.Equal(@"art\civilopedia\icons\races\x_temujin civpedia sm.pcx", icons.GetCivilopediaSmallIconPath("RACE_Mongols"));
	}

	[Fact]
	public void SingleVariantBuildingsParseWithoutARowNumber() {
		PediaIcons icons = Parse("""
		#ICON_BLDG_Apollo_Project
		SINGLE
		art\civilopedia\icons\buildings\apollolarge.pcx
		art\civilopedia\icons\buildings\apollosmall.pcx
		#ICON_BLDG_Aqueduct
		ERA
		12
		art\civilopedia\icons\buildings\aqueductlarge.pcx
		art\civilopedia\icons\buildings\aqueductsmall.pcx
		""");

		Assert.Equal(@"art\civilopedia\icons\buildings\apollolarge.pcx", icons.GetCivilopediaIconPath("BLDG_Apollo_Project"));
		Assert.Equal(12, icons.GetBuildingRowNumber("BLDG_Aqueduct"));
	}

	[SkippableFact]
	public void ParsesTheInstalledPediaIcons() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		string path = Path.Combine(Civ3Location.GetCiv3Path(), "Conquests", "Text", "PediaIcons.txt");
		PediaIcons icons = new(path);

		Assert.NotNull(icons.GetCivilopediaIconPath("BLDG_Barracks"));
		Assert.NotNull(icons.GetCivilopediaSmallIconPath("BLDG_Barracks"));
		Assert.NotNull(icons.GetCivilopediaIconPath("RACE_AMERICAN"));
		// Rulesets use mixed-case race keys for the Conquests civilizations.
		Assert.NotNull(icons.GetCivilopediaIconPath("RACE_Mongols"));
		Assert.NotNull(icons.GetCivilopediaSmallIconPath("RACE_Mongols"));
	}
}
