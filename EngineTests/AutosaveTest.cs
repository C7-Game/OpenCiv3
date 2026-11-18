using System.IO;
using System.Linq;
using C7Engine;
using Xunit;

namespace EngineTests;

public class AutosaveTest {
	private static string AutosaveName(int turn) {
		return Path.Combine("saves", "Auto", $"Bismark of the Germans-Turn {turn:000}-4000 BC.json");
	}

	[Fact]
	public void KeepsTurnZeroAndMostRecentTurns() {
		string[] files = [AutosaveName(0), AutosaveName(1), AutosaveName(2), AutosaveName(3), AutosaveName(4)];

		string[] pruned = Autosave.SelectFilesToPrune(files, 2).OrderBy(path => path).ToArray();

		Assert.Equal(new[] { files[1], files[2] }, pruned);
	}

	[Fact]
	public void ZeroTurnsKeepsOnlyTurnZero() {
		string[] files = [AutosaveName(0), AutosaveName(1), AutosaveName(2)];

		string[] pruned = Autosave.SelectFilesToPrune(files, 0).OrderBy(path => path).ToArray();

		Assert.Equal(new[] { files[1], files[2] }, pruned);
	}

	[Fact]
	public void IgnoresFilesWithoutATurnNumber() {
		string[] files = [AutosaveName(0), AutosaveName(1), Path.Combine("saves", "manual-save.json")];

		string[] pruned = Autosave.SelectFilesToPrune(files, 0).ToArray();

		Assert.Equal(new[] { files[1] }, pruned);
	}
}
