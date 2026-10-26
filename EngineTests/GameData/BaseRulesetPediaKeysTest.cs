using System.Text.Json;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class BaseRulesetPediaKeysTest {
	[Fact]
	public void BaseRulesetUnitsCarryCivilopediaEntry() {
		AssertEntriesHavePediaKey("unitPrototypes", "PRTO_");
	}

	[Fact]
	public void BaseRulesetBuildingsCarryCivilopediaEntry() {
		AssertEntriesHavePediaKey("buildings", "BLDG_");
	}

	[Fact]
	public void BaseRulesetCivilizationsCarryCivilopediaEntry() {
		AssertEntriesHavePediaKey("civilizations", "RACE_");
	}

	private static void AssertEntriesHavePediaKey(string property, string expectedPrefix) {
		using JsonDocument ruleset = JsonUtils.LoadBaseRuleset();
		foreach (JsonElement entry in ruleset.RootElement.GetProperty(property).EnumerateArray()) {
			string name = entry.GetProperty("name").GetString();
			string key = entry.GetProperty("civilopediaEntry").GetString();

			Assert.False(string.IsNullOrWhiteSpace(key), $"{property} '{name}' has no civilopediaEntry, so its Codex article cannot load.");
			Assert.StartsWith(expectedPrefix, key);
		}
	}
}
