using C7GameData;
using C7Engine;
using IniParser.Model;
using Xunit;

namespace EngineTests.GameData.Victory;

public class VictoryConditionsSettingsTest {
	[Fact]
	public void FromSettings_DefaultsMatchDominationAndRequireCultureLead() {
		IniData previousSettings = C7Settings.settings;
		try {
			C7Settings.settings = new IniData();

			VictoryConditions conditions = VictoryConditions.FromSettings();

			Assert.Equal(conditions.AllowDominationVictory, conditions.AllowCulturalVictory);
			Assert.True(conditions.CultureVictoryRequiresDoubleLead);
		} finally {
			C7Settings.settings = previousSettings;
		}
	}

	[Fact]
	public void FromSettings_PersistsCultureOptions() {
		IniData previousSettings = C7Settings.settings;
		try {
			C7Settings.settings = new IniData();
			C7Settings.SetBool(C7Settings.Rules.SectionName, C7Settings.Rules.AllowCulturalVictory, true);
			C7Settings.SetBool(C7Settings.Rules.SectionName, C7Settings.Rules.CultureVictoryRequiresDoubleLead, false);

			VictoryConditions conditions = VictoryConditions.FromSettings();

			Assert.True(conditions.AllowCulturalVictory);
			Assert.False(conditions.CultureVictoryRequiresDoubleLead);
		} finally {
			C7Settings.settings = previousSettings;
		}
	}
}
