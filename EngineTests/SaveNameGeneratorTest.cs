using C7Engine;
using C7GameData;
using Xunit;

namespace EngineTests;

public class SaveNameGeneratorTest {
	private static Player CreatePlayer() {
		return new Player {
			civilization = new Civilization {
				leader = "Bismark",
				noun = "Germans",
				name = "Germany",
			},
		};
	}

	[Fact]
	public void UsesLeaderCivilizationAndTurn() {
		string name = SaveNameGenerator.GenerateSaveFileName(CreatePlayer(), 0, "4000 BC");
		Assert.Equal("Bismark of the Germans-Turn 000-4000 BC.json", name);
	}

	[Fact]
	public void PadsTurnNumber() {
		string name = SaveNameGenerator.GenerateSaveFileName(CreatePlayer(), 5, "3950 BC");
		Assert.Equal("Bismark of the Germans-Turn 005-3950 BC.json", name);
	}

	[Fact]
	public void ReplacesInvalidFileNameCharacters() {
		string name = SaveNameGenerator.GenerateSaveFileName(CreatePlayer(), 0, "4000/BC");
		Assert.Equal("Bismark of the Germans-Turn 000-4000_BC.json", name);
	}

	[Fact]
	public void FallsBackToCivilizationNameWhenNounIsMissing() {
		Player player = new() {
			civilization = new Civilization {
				leader = "Bismark",
				name = "Germany",
			},
		};

		string name = SaveNameGenerator.GenerateSaveFileName(player, 0, "4000 BC");
		Assert.Equal("Bismark of the Germany-Turn 000-4000 BC.json", name);
	}
}
