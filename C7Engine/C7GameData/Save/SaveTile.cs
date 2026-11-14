using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Linq;
using System.Reflection;

namespace C7GameData.Save {

	public class SaveTile {
		public SaveTile() { }

		public SaveTile(Tile tile) {
			id = tile.Id;
			extraInfo = tile.ExtraInfo;
			X = tile.XCoordinate;
			Y = tile.YCoordinate;
			continent = tile.continent;
			isFreshWater = tile.isFreshWater;
			baseTerrain = tile.baseTerrainType.Key;
			overlayTerrain = tile.overlayTerrainType.Key;
			if (tile.Resource != Resource.NONE) {
				resource = tile.ResourceKey;
			}
			if (tile.isBonusShield) {
				features.Add("bonusShield");
			}
			if (tile.isSnowCapped) {
				features.Add("snowCapped");
			}
			if (tile.isPineForest) {
				features.Add("pineForest");
			}
			foreach (FieldInfo fi in tile.GetType().GetFields()) {
				if (fi.Name.StartsWith("river") && fi.FieldType == typeof(bool) && (bool)fi.GetValue(tile)) {
					features.Add(fi.Name);
				}
			}
			if (tile.hasBarbarianCamp) {
				features.Add(Tile.TileOverlays.BARBARIAN_CAMP);
			}
			if (tile.hasGoodyHut) {
				features.Add(Tile.TileOverlays.GOODY_HUT);
			}
			if (tile.hasHadForestCleared) {
				features.Add("hasHadForestCleared");
			}

			overlays.AddRange(tile.overlays.GetImprovements().Select(i => i.key));
		}

		// TODO: if this is slow, features can be read from JSON and then hashed so the Contains check is faster
		public Tile ToTile(List<TerrainType> terrainTypes, List<Resource> resources, List<TerrainImprovement> improvements) {
			Tile tile = new Tile(id){
				ExtraInfo = extraInfo,
				XCoordinate = X,
				YCoordinate = Y,
				continent = continent,
				isFreshWater = isFreshWater,
				baseTerrainType = terrainTypes.Find(tt => tt.Key == baseTerrain),
				overlayTerrainType = terrainTypes.Find(tt => tt.Key == overlayTerrain),
				hasBarbarianCamp = features.Contains(Tile.TileOverlays.BARBARIAN_CAMP),
				hasGoodyHut = features.Contains(Tile.TileOverlays.GOODY_HUT),
				hasHadForestCleared = features.Contains("hasHadForestCleared"),
				// TODO: load working tile
				ResourceKey = resource is null ? Resource.NONE.Key : resource,
				riverNorth = features.Contains("riverNorth"),
				riverNortheast = features.Contains("riverNortheast"),
				riverEast = features.Contains("riverEast"),
				riverSoutheast = features.Contains("riverSoutheast"),
				riverSouth = features.Contains("riverSouth"),
				riverSouthwest = features.Contains("riverSouthwest"),
				riverWest = features.Contains("riverWest"),
				riverNorthwest = features.Contains("riverNorthwest"),
				isBonusShield = features.Contains("bonusShield"),
				isSnowCapped = features.Contains("snowCapped"),
				isPineForest = features.Contains("pineForest"),
			};

			tile.Resource = tile.ResourceKey == Resource.NONE.Key ? Resource.NONE : resources.Find(r => r.Key == tile.ResourceKey);
			overlays.ForEach(key => tile.overlays.Add(improvements.Find(ti => ti.key == key)));

			return tile;
		}
		public Civ3ExtraInfo extraInfo;

		public ID id;
		public int X;
		public int Y;
		public int continent;
		public bool isFreshWater;
		[JsonRequired]
		public string baseTerrain;
		public string overlayTerrain;
		public string resource;
		public List<string> features = new List<string>();
		public List<string> overlays = new List<string>();
	}

}
