using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using Xunit;

namespace EngineTests.GameData.Victory;

public class ScoreVictoryTest {

	[Fact]
	public void HasVictory_AlwaysFalse_ScoreIsATiebreakerNotAWinCondition() {
		var victory = new ScoreVictory();

		Assert.False(victory.HasVictory(new VictoryStatus { Score = 999999f, TurnScore = 999999f }));
	}

	private static CitizenType DefaultCitizen() => new() { IsDefaultCitizen = true };
	private static CitizenType Specialist() => new() { IsDefaultCitizen = false };

	[Fact]
	public void ComputeTurnScore_CombinesTilesCitizensAndSpecialistsThenAppliesDifficultyFactor() {
		Player player = new() { civilization = new Civilization("Rome") };

		City city = new City(Tile.NONE, player, "Roma", ID.None("city"));
		city.residents.Add(new CityResident { citizenType = DefaultCitizen(), mood = CityResident.Mood.Happy });
		city.residents.Add(new CityResident { citizenType = DefaultCitizen(), mood = CityResident.Mood.Content });
		city.residents.Add(new CityResident { citizenType = DefaultCitizen(), mood = CityResident.Mood.Unhappy });
		city.residents.Add(new CityResident { citizenType = Specialist() });
		player.cities.Add(city);

		TerrainType land = new TerrainType { Key = "grassland" };
		Tile scoredTile1 = new Tile(ID.None("tile")) { baseTerrainType = land, owningCity = city };
		Tile scoredTile2 = new Tile(ID.None("tile")) { baseTerrainType = land, owningCity = city };
		player.tileKnowledge.knownTiles.Add(scoredTile1);
		player.tileKnowledge.knownTiles.Add(scoredTile2);

		var lowDifficulty = new Difficulty();
		var highDifficulty = new Difficulty();
		C7GameData.GameData gameData = new() {
			difficulties = new List<Difficulty> { lowDifficulty, highDifficulty },
			gameDifficulty = highDifficulty // index 1 -> factor of 2
		};

		float turnScore = ScoreVictory.ComputeTurnScore(player, gameData);

		// 2 scored tiles + 2*1 happy + 1 content + 1 specialist = 6, times the
		// difficulty factor of (index 1 + 1) = 2.
		Assert.Equal(12f, turnScore);
	}

	[Fact]
	public void ComputeTurnScore_UnrecognizedDifficulty_FallsBackToFactorOfOne() {
		Player player = new() { civilization = new Civilization("Rome") };
		player.cities.Add(new City(Tile.NONE, player, "Roma", ID.None("city")));

		C7GameData.GameData gameData = new() {
			difficulties = new List<Difficulty> { new Difficulty(), new Difficulty() },
			gameDifficulty = new Difficulty() // not present in the difficulties list
		};

		float turnScore = ScoreVictory.ComputeTurnScore(player, gameData);

		Assert.Equal(0f, turnScore); // no tiles/citizens, factor of 1 => still 0
	}

	[Fact]
	public void Evaluate_ReadsAccumulatedScoreFromHistory() {
		Player player = new() { civilization = new Civilization("Rome"), id = ID.FromString("player-1") };
		player.cities.Add(new City(Tile.NONE, player, "Roma", ID.None("city")));

		C7GameData.GameData gameData = new() {
			difficulties = new List<Difficulty> { new Difficulty() },
			gameDifficulty = new Difficulty(),
			history = new Dictionary<string, List<HistTurnRecord>> {
				[player.id.ToString()] = new() {
					new HistTurnRecord { Score = 10 },
					new HistTurnRecord { Score = 25 }
				}
			}
		};
		gameData.gameDifficulty = gameData.difficulties[0];

		var victory = new ScoreVictory();
		VictoryStatus status = victory.Evaluate(player, gameData);

		Assert.Equal(25, status.Score);
	}
}
