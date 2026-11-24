using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class GoldenAgeTriggerTest : MapBase {
	private const int Turn = 10;
	private const int Duration = 20;

	public GoldenAgeTriggerTest() {
		InitializeTestGameData();
		C7Engine.EngineStorage.gameData.turn = Turn;
	}

	private Player MakeCivPlayer(bool barbarian = false) {
		return new Player() {
			civilization = new Civilization() { isBarbarian = barbarian },
			rules = new Rules() { GoldenAgeDuration = Duration },
		};
	}

	private MapUnit MakeUnit(Player owner, bool startsGoldenAge, int defense = 1) {
		MapUnit unit = new(ID.None("")) { unitType = new UnitPrototype() { defense = defense }, owner = owner };
		unit.unitType.startsGoldenAge = startsGoldenAge;
		return unit;
	}

	[Fact]
	public void WinningAgainstAnotherCivStartsGoldenAge() {
		Player winnerOwner = MakeCivPlayer();
		MapUnit.StartGoldenAgeIfEarned(MakeUnit(winnerOwner, true), MakeUnit(MakeCivPlayer(), false));

		Assert.True(winnerOwner.HasHadGA);
		Assert.True(winnerOwner.IsInGA);
		Assert.Equal(Turn + Duration, winnerOwner.goldenAgeEnds);
	}

	[Fact]
	public void WinningAgainstBarbariansDoesNotStartGoldenAge() {
		Player winnerOwner = MakeCivPlayer();
		MapUnit.StartGoldenAgeIfEarned(MakeUnit(winnerOwner, true), MakeUnit(MakeCivPlayer(barbarian: true), false));

		Assert.False(winnerOwner.HasHadGA);
	}

	[Fact]
	public void KillingNonCombatUnitDoesNotStartGoldenAge() {
		Player winnerOwner = MakeCivPlayer();
		MapUnit settler = MakeUnit(MakeCivPlayer(), false, defense: 0);
		MapUnit.StartGoldenAgeIfEarned(MakeUnit(winnerOwner, true), settler);

		Assert.False(winnerOwner.HasHadGA);
	}

	[Fact]
	public void WinningWithoutTheFlagDoesNotStartGoldenAge() {
		Player winnerOwner = MakeCivPlayer();
		MapUnit.StartGoldenAgeIfEarned(MakeUnit(winnerOwner, false), MakeUnit(MakeCivPlayer(), false));

		Assert.False(winnerOwner.HasHadGA);
	}

	[Fact]
	public void SecondVictoryDoesNotRestartGoldenAge() {
		Player winnerOwner = MakeCivPlayer();
		MapUnit winner = MakeUnit(winnerOwner, true);
		MapUnit loser = MakeUnit(MakeCivPlayer(), false);

		MapUnit.StartGoldenAgeIfEarned(winner, loser);
		C7Engine.EngineStorage.gameData.turn = Turn + 5;
		MapUnit.StartGoldenAgeIfEarned(winner, loser);

		Assert.Equal(Turn + Duration, winnerOwner.goldenAgeEnds);
	}

	[Fact]
	public void FlagRoundTripsThroughSavePrototype() {
		UnitPrototype proto = new() { name = "Samurai" };
		proto.startsGoldenAge = true;

		Assert.Contains(SaveUnitPrototype.Flag.StartsGoldenAge, new SaveUnitPrototype(proto).flags);
	}
}
