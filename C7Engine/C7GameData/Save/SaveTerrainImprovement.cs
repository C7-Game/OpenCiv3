using System.Collections.Generic;
using static C7GameData.Tile.TileOverlays;
using Layer = C7GameData.TerrainImprovement.Layer;

namespace C7GameData.Save;

public class SaveTerrainImprovement {
	// The following method is used to generate terrain improvement
	// data when loading a CIV3 SAV or BIQ file
	public static IEnumerable<SaveTerrainImprovement> Civ3Improvements() {
		yield return new(
			IRRIGATION,
			Layer.ResourceDevelopment,
			zIndex: 0);

		yield return new(
			MINE,
			Layer.ResourceDevelopment,
			zIndex: 2);

		yield return new(
			ROAD,
			Layer.Roads,
			movementCost: 1.0f / 3,
			zIndex: 1);

		yield return new(
			RAILROAD,
			Layer.Roads,
			upgradesFrom: ROAD,
			movementCost: 0,
			zIndex: 1,
			tileModifier: "terrain_improvements.railroad.tile_modifier"
		);

		yield return new(
			FORTRESS,
			Layer.Holdings,
			defenseBonus: new(FORTRESS, 0.5),
			zIndex: 6
		);
		yield return new(
			BARRICADE,
			Layer.Holdings,
			upgradesFrom: FORTRESS,
			defenseBonus: new(BARRICADE, 1),
			zIndex: 6
		);
		yield return new(
			RUINS,
			Layer.Ruins,
			zIndex: 2
		);
		yield return new(
			POLLUTION,
			Layer.Pollution,
			zIndex: 10
		);
		yield return new(
			CRATERS,
			Layer.Craters,
			zIndex: 5
		);
		yield return new(
			BARBARIAN_CAMP,
			Layer.BarbarianCamp,
			zIndex: 8
		);

		// TODO: Add colony, outpost, airfield, radar tower
	}

	public readonly string key;

	public readonly Layer layer;
	public readonly StrengthBonus? defenseBonus;

	public readonly int zIndex;

	public readonly float movementCost = -1;

	public readonly string upgradesFrom; // a key for another Terrain Improvement

	// The string in the key points to a Terrain Type
	public Dictionary<string, Dictionary<Tile.YieldType, int>> bonusYields = [];

	// Path to Lua function
	public readonly string tileModifier;

	public SaveTerrainImprovement(
		string key,
		Layer layer,
		string upgradesFrom = null,
		StrengthBonus? defenseBonus = null,
		float movementCost = -1,
		int zIndex = 0,
		string tileModifier = null
	) {
		this.key = key;
		this.layer = layer;
		this.defenseBonus = defenseBonus;
		this.upgradesFrom = upgradesFrom;
		this.movementCost = movementCost;
		this.zIndex = zIndex;
		this.tileModifier = tileModifier;
	}
}
