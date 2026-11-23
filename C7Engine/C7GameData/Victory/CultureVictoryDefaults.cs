using System;
using System.Collections.Generic;

namespace C7GameData;

public static class CultureVictoryDefaults {
	public const int OneCityCulture = 20000;
	public const int AllCitiesCulture = 100000;

	private static readonly Dictionary<string, float> AllCitiesMultipliers = new(StringComparer.OrdinalIgnoreCase) {
		["Tiny"] = 0.6f,
		["Small"] = 0.8f,
		["Standard"] = 1.0f,
		["Large"] = 1.3f,
		["Huge"] = 1.6f,
	};

	public static (int OneCity, int AllCities) EffectiveLimits(int importedOneCity, int importedAllCities, string worldSizeName) {
		int oneCity = importedOneCity > 0 ? importedOneCity : OneCityCulture;
		int allCitiesBase = importedAllCities > 0 ? importedAllCities : AllCitiesCulture;
		return (oneCity, ScaleAllCities(allCitiesBase, worldSizeName));
	}

	public static int ScaleAllCities(int allCitiesBase, string worldSizeName) {
		float multiplier = AllCitiesMultipliers.TryGetValue(worldSizeName ?? "", out float value) ? value : 1.0f;
		return (int)Math.Round(allCitiesBase * multiplier, MidpointRounding.AwayFromZero);
	}
}
