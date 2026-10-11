using System.Collections.Generic;
using System.Linq;

namespace C7GameData;

public class DominationVictory : IVictory {
	private readonly float _dominationAreaLimit;
	private readonly float _dominationPopulationLimit;

	public DominationVictory(float dominationAreaLimit, float dominationPopulationLimit) {
		_dominationAreaLimit = dominationAreaLimit;
		_dominationPopulationLimit = dominationPopulationLimit;
	}

	public string Header() => "Domination";

	public VictoryStatus Evaluate(Player player, GameData gameData) {
		float worldPopulation = gameData.cities.Sum(c => c.residents.Count) * 1.0f;
		float population = player.cities.Sum(c => c.residents.Count) * 1.0f;

		List<Tile> allMapTiles = gameData.map.tiles;
		List<Tile> dominatedTiles = player.tileKnowledge.DominationTiles();
		List<Tile> allDominationTiles = allMapTiles.Where(t => t.IsCountedForDomination()).ToList();

		return new VictoryStatus {
			Player = player,
			DominationArea = float.Floor(dominatedTiles.Count * 100f / allDominationTiles.Count),
			DominationPopulation = float.Floor(population * 100f / worldPopulation)
		};
	}

	public bool HasVictory(VictoryStatus status) {
		return status.DominationArea >= _dominationAreaLimit
			   && status.DominationPopulation >= _dominationPopulationLimit;
	}

	public IEnumerable<string[]> GenerateStatusRows(VictoryStatus status, List<VictoryStatus> rivalStatuses) {
		yield return DominationAreaPrint(status, rivalStatuses);
		yield return DominationPopulationPrint(status, rivalStatuses);
	}

	private string[] DominationAreaPrint(VictoryStatus status, List<VictoryStatus> rivalStatuses) {
		VictoryStatus topRivalByDominationArea = rivalStatuses
			.OrderByDescending(r => r.DominationArea).FirstOrDefault();

		string topAreaRival = topRivalByDominationArea?.Player?.civilization?.name ?? "";
		float topAreaRivalValue = topRivalByDominationArea?.DominationArea ?? float.NaN;

		return [
			"% of world area:",
			$"{_dominationAreaLimit}",
			"Your % of world area:",
			float.IsNaN(status.DominationArea) ? "" : $"{status.DominationArea:F0}",
			topAreaRival,
			float.IsNaN(topAreaRivalValue) ? "" : $"{topAreaRivalValue:F0}"
		];
	}

	private string[] DominationPopulationPrint(VictoryStatus status, List<VictoryStatus> rivalStatuses) {
		VictoryStatus topRivalByDominationPopulation = rivalStatuses
			.OrderByDescending(r => r.DominationPopulation).FirstOrDefault();

		string topPopRival = topRivalByDominationPopulation?.Player?.civilization?.name ?? "";
		float topPopRivalValue = topRivalByDominationPopulation?.DominationPopulation ?? float.NaN;

		return [
			"% of world population:",
			$"{_dominationPopulationLimit}",
			"Your % of world population:",
			float.IsNaN(status.DominationPopulation) ? "" : $"{status.DominationPopulation:F0}",
			topPopRival,
			float.IsNaN(topPopRivalValue) ? "" : $"{topPopRivalValue:F0}"
		];
	}
}
