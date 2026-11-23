using System.Collections.Generic;
using System.Linq;

namespace C7GameData;

public class CultureVictory : IVictory {
	private readonly int _oneCityCultureLimit;
	private readonly int _allCitiesCultureLimit;
	private readonly bool _requireDoubleLead;

	public CultureVictory(int oneCityCultureLimit, int allCitiesCultureLimit, bool requireDoubleLead) {
		_oneCityCultureLimit = oneCityCultureLimit;
		_allCitiesCultureLimit = allCitiesCultureLimit;
		_requireDoubleLead = requireDoubleLead;
	}

	public string Header() => "Culture";

	public VictoryStatus Evaluate(Player player, GameData gameData) {
		int largestCityCulture = player.cities.Count == 0 ? 0 : player.cities.Max(c => c.GetCulture());
		int totalCulture = player.cities.Sum(c => c.GetCulture());
		int topRivalCulture = gameData.GetRivals(player)
			.Select(rival => rival.cities.Sum(c => c.GetCulture()))
			.DefaultIfEmpty(0)
			.Max();

		return new VictoryStatus {
			Player = player,
			LargestCityCulture = largestCityCulture,
			TotalCulture = totalCulture,
			TopRivalCulture = topRivalCulture,
		};
	}

	private bool WonInOneCity(VictoryStatus status) =>
		status.LargestCityCulture >= _oneCityCultureLimit;

	private bool WonAcrossEmpire(VictoryStatus status) =>
		status.TotalCulture >= _allCitiesCultureLimit &&
		(!_requireDoubleLead || status.TotalCulture >= 2L * status.TopRivalCulture);

	public bool HasVictory(VictoryStatus status) =>
		WonInOneCity(status) || WonAcrossEmpire(status);

	public string Detail(VictoryStatus status) {
		bool byCity = WonInOneCity(status);
		bool byEmpire = WonAcrossEmpire(status);
		if (byCity && byEmpire)
			return "city and empire";
		if (byCity)
			return "city";
		if (byEmpire)
			return "empire";
		return "";
	}

	public IEnumerable<string[]> GenerateStatusRows(VictoryStatus status, List<VictoryStatus> rivalStatuses) {
		yield return OneCityPrint(status, rivalStatuses);
		yield return AllCitiesPrint(status, rivalStatuses);
	}

	private string[] OneCityPrint(VictoryStatus status, List<VictoryStatus> rivalStatuses) {
		VictoryStatus topRival = rivalStatuses.OrderByDescending(r => r.LargestCityCulture).FirstOrDefault();
		string rivalName = topRival?.Player?.civilization?.name ?? "";
		int rivalCulture = topRival?.LargestCityCulture ?? 0;

		return [
			"Culture in one city:",
			$"{_oneCityCultureLimit}",
			"Your best city:",
			$"{status.LargestCityCulture}",
			rivalName,
			rivalCulture == 0 ? "" : $"{rivalCulture}"
		];
	}

	private string[] AllCitiesPrint(VictoryStatus status, List<VictoryStatus> rivalStatuses) {
		VictoryStatus topRival = rivalStatuses.OrderByDescending(r => r.TotalCulture).FirstOrDefault();
		string rivalName = topRival?.Player?.civilization?.name ?? "";
		int rivalCulture = topRival?.TotalCulture ?? 0;
		string limit = _requireDoubleLead ? $"{_allCitiesCultureLimit} (2x)" : $"{_allCitiesCultureLimit}";

		return [
			"Culture across civilization:",
			limit,
			"Your total culture:",
			$"{status.TotalCulture}",
			rivalName,
			rivalCulture == 0 ? "" : $"{rivalCulture}"
		];
	}
}
