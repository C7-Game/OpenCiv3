using System.Collections.Generic;
using System.Linq;
using C7Engine;
using Serilog;
using static C7GameData.TerrainImprovement;
using static C7GameData.Tile.TileOverlays;

namespace C7GameData;

public partial class Tile {
	public static void TryAddRoad(Tile tile, bool hasRoad, bool hasRailroad) {
		var shouldConnectToToNetwork = !hasRailroad && tile.neighbors.Any(p => p.Value.HasRoad());
		if (shouldConnectToToNetwork || hasRoad) {
			var roadTerraform = ToTerraform(ROAD);
			if (roadTerraform != null && tile.cityAtTile.owner.HasTech(roadTerraform.RequiredTech)) {
				tile.overlays.Add(roadTerraform.Improvement);
			}
		}
	}
	public static void TryAddRailroad(Tile tile, bool hasRailroad) {
		var shouldConnectToToNetwork = !hasRailroad && tile.neighbors.Any(p => p.Value.HasRailroad());
		if (shouldConnectToToNetwork || hasRailroad) {
			var railroadTerraform = ToTerraform(RAILROAD);
			if (railroadTerraform != null && tile.cityAtTile.owner.HasTech(railroadTerraform.RequiredTech)) {
				tile.overlays.Add(railroadTerraform.Improvement);
			}
		}
	}
	public static void TryAddRuins(Tile tile) {
		var ruins =
			EngineStorage.gameData.terrainImprovements.FirstOrDefault(i => i.key == RUINS);
		if (ruins != null)
			tile.overlays.Add(ruins);
	}
	public static void TryAddCraters(Tile tile) {
		var craters =
			EngineStorage.gameData.terrainImprovements.FirstOrDefault(i => i.key == CRATERS);
		if (craters != null)
			tile.overlays.Add(craters);
	}

	public class TileOverlays {
		public const string ROAD = "road";
		public const string RAILROAD = "railroad";
		public const string IRRIGATION = "irrigation";
		public const string MINE = "mine";
		public const string FORTRESS = "fortress";
		public const string BARRICADE = "barricade";
		public const string BARBARIAN_CAMP = "barbarianCamp";
		public const string GOODY_HUT = "goodyHut";
		public const string RUINS = "ruins";
		public const string POLLUTION = "pollution";
		public const string CRATERS = "craters";

		private readonly Tile tile;
		public Dictionary<Layer, TerrainImprovement> terrainImprovementByLayer { get; private set; } = [];

		public TileOverlays(Tile tile) {
			this.tile = tile;
		}

		public void Add(TerrainImprovement improvement) {
			if (improvement == null) {
				return;
			}
			terrainImprovementByLayer.TryGetValue(improvement.layer, out TerrainImprovement replacedImprovement);

			terrainImprovementByLayer[improvement.layer] = improvement;

			ApplyTerrainImprovementChange(replacedImprovement, improvement);
		}

		public void Remove(TerrainImprovement improvement) {
			if (!terrainImprovementByLayer.Remove(improvement.layer))
				Log.Warning("Failed to remove terrain improvement.");

			ApplyTerrainImprovementChange(improvement, null);
		}

		private static void ApplyTerrainImprovementChange(TerrainImprovement oldImprovement, TerrainImprovement newImprovement) {
			var roadCreated = oldImprovement == null && newImprovement?.layer == Layer.Roads;
			var roadRemoved = oldImprovement?.layer == Layer.Roads && newImprovement == null;

			// If there's a change in road coverage, invalidate the cached trade network
			if (roadCreated || roadRemoved) {
				EngineStorage.gameData.InvalidateCachedTradeNetwork();
			}
		}

		public TerrainImprovement ImprovementAtLayer(Layer layer) {
			terrainImprovementByLayer.TryGetValue(layer, out TerrainImprovement ti);
			return ti;
		}

		// Returns an existing improvement that would be replaced by the given terraform.
		// Returns null if there is no such improvement,
		// or the new improvement upgrades from the existing one (upgrades don't count as replacements)
		public TerrainImprovement GetReplacementTarget(Terraform terraform) {
			var newImp = terraform.Improvement;
			if (newImp == null)
				return null;

			var current = ImprovementAtLayer(newImp.layer);
			if (current == null)
				return null;

			return newImp.upgradesFrom != current ? current : null;
		}

		public bool HasImprovement(TerrainImprovement improvement) {
			return terrainImprovementByLayer.TryGetValue(improvement.layer, out TerrainImprovement val) && val == improvement;
		}

		public IEnumerable<TerrainImprovement> GetImprovements() {
			return terrainImprovementByLayer.Values;
		}
		public IEnumerable<TerrainImprovement> GetManMadeImprovements() {
			return GetImprovements().Where(
				i => i.layer != Layer.Craters
					 && i.layer != Layer.Pollution
					 && i.layer != Layer.Ruins
				);
		}
		public IEnumerable<TerrainImprovement> GetEffectImprovements() {
			return GetImprovements().Where(
				i => i.layer == Layer.Craters
					 || i.layer == Layer.Pollution
					 || i.layer == Layer.Ruins
			);
		}

		public bool CanAdd(Tile targetTile, TerrainImprovement improvement) {
			var hasCity = targetTile.HasCity();

			// we can't ever add anything on a city tile, except roading
			if (hasCity && improvement.layer != Layer.Roads)
				return false;

			var hasImprovement = terrainImprovementByLayer.TryGetValue(improvement.layer, out var current);
			var canBeReplaced = hasImprovement && current.CanBeReplacedBy(improvement);

			if (hasCity && improvement.layer == Layer.Roads && canBeReplaced)
				return true;

			if (!hasImprovement)
				return improvement.upgradesFrom == null;

			return canBeReplaced;
		}

		// Will return a -1 if the tile movement cost is unaffected by the improvements
		public float MovementCost() {
			if (terrainImprovementByLayer.TryGetValue(Layer.Roads, out TerrainImprovement road)) {
				return road.movementCost;
			}

			// since we added roads & railroads on city tiles, this is probably not needed
			// I am only leaving this here, because it doesn't seem to affect the game
			// and it's also related to the tile path algorithm, and I think I want to favourite 
			// going through cities if possible, for better defense.
			if (tile.HasCity()) {
				return 0;
			}

			return -1;
		}

		public int GetBaseYieldBonus(YieldType type) {
			return terrainImprovementByLayer.Values.Sum(ti => ti.GetYieldBonus(tile.overlayTerrainType, type));
		}

		public bool HasBeenImproved() {
			return GetManMadeImprovements().Any();
		}

		public IEnumerable<StrengthBonus> GetDefenseBonuses() {
			return terrainImprovementByLayer.Values
				.Select(ti => ti.defenseBonus)
				.Where(v => v.HasValue)
				.Select(v => v.Value);
		}

		public void Clear() {
			terrainImprovementByLayer.Clear();
		}
	}
}
