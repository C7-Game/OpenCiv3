using System.Collections.Generic;
using C7Engine;

namespace C7.UIElements;

public enum CleanMapToggle {
	HideRiversFloodPlains,
	HideMountains,
	HideHills,
	HideForestsJungles,
	HideTerrainBuildings,
	HideRoadsRailroads,
	HideIrrigation,
	HideNaturalResources,
	HideFoodShieldIcons,
	HidePollution,
	HideTerritoryBoundaries,
	HideCities,
	HideUnits,
}

public record CleanMapOptionDefinition(CleanMapToggle Toggle, string Label, bool DefaultValue);

public static class CleanMapSettings {
	private const string SectionName = "Clean Map";

	public static bool Enabled { get; set; } = false;

	public static IReadOnlyList<CleanMapOptionDefinition> Options { get; } = [
		new(CleanMapToggle.HideRiversFloodPlains, "Hide Rivers/Flood Plains", false),
		new(CleanMapToggle.HideMountains, "Hide Mountains", false),
		new(CleanMapToggle.HideHills, "Hide Hills", false),
		new(CleanMapToggle.HideForestsJungles, "Hide Forests/Jungles", false),
		new(CleanMapToggle.HideTerrainBuildings, "Hide Terrain Buildings (Mines, Colonies, etc.)", true),
		new(CleanMapToggle.HideRoadsRailroads, "Hide Roads/Railroads", true),
		new(CleanMapToggle.HideIrrigation, "Hide Irrigation", true),
		new(CleanMapToggle.HideNaturalResources, "Hide Natural Resources", false),
		new(CleanMapToggle.HideFoodShieldIcons, "Hide Food/Shield Icons", false),
		new(CleanMapToggle.HidePollution, "Hide Pollution", true),
		new(CleanMapToggle.HideTerritoryBoundaries, "Hide Territory Boundaries", true),
		new(CleanMapToggle.HideCities, "Hide Cities", true),
		new(CleanMapToggle.HideUnits, "Hide Units", true),
	];

	private static readonly Dictionary<CleanMapToggle, bool> values = new();
	private static bool loaded = false;

	public static bool IsHiding(CleanMapToggle toggle) {
		return Enabled && Get(toggle);
	}

	public static bool Get(CleanMapToggle toggle) {
		EnsureLoaded();
		return values[toggle];
	}

	public static void Set(CleanMapToggle toggle, bool value) {
		EnsureLoaded();
		values[toggle] = value;
		C7Settings.SetBool(SectionName, toggle.ToString(), value);
		C7Settings.SaveSettings();
	}

	private static void EnsureLoaded() {
		if (loaded) {
			return;
		}

		foreach (CleanMapOptionDefinition option in Options) {
			values[option.Toggle] = C7Settings.GetBoolOrDefault(
				SectionName, option.Toggle.ToString(), option.DefaultValue);
		}
		loaded = true;
	}
}
