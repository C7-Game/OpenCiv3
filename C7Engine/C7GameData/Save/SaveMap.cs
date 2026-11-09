using System.Collections.Generic;
using System.Linq;

namespace C7GameData.Save {

	public class SaveMap {
		public int tilesWide, tilesTall;
		public bool wrapHorizontally, wrapVertically;
		public int techRate;
		public int optimalNumberOfCities;
		public List<SaveTile> tiles = new List<SaveTile>();
		public List<SaveTile> startingLocations = new();
		public SaveMap() { }

		public SaveMap(GameMap map) {
			tilesWide = map.numTilesWide;
			tilesTall = map.numTilesTall;
			wrapHorizontally = map.wrapHorizontally;
			wrapVertically = map.wrapVertically;
			techRate = map.techRate;
			optimalNumberOfCities = map.optimalNumberOfCities;
			tiles = map.tiles.ConvertAll(tile => new SaveTile(tile));
			startingLocations = map.startingLocations.ConvertAll(x => new SaveTile(x));
		}
		public GameMap ToGameMap(GameData gd) {
			GameMap gameMap = new GameMap{
				numTilesWide = tilesWide,
				numTilesTall = tilesTall,
				wrapHorizontally = wrapHorizontally,
				wrapVertically = wrapVertically,
				techRate = techRate,
				optimalNumberOfCities = optimalNumberOfCities,
				tiles = tiles.ConvertAll(tile => tile.ToTile(gd.terrainTypes, gd.Resources, gd.terrainImprovements)),
			};
			foreach (SaveTile st in startingLocations) {
				gameMap.startingLocations.Add(gameMap.tiles.Find(t => t.XCoordinate == st.X && t.YCoordinate == st.Y));
			}
			gameMap.computeNeighbors();
			gameMap.computeCardinalRiverFlags();
			gameMap.barbarianCamps = gameMap.tiles.Where(tile => tile.hasBarbarianCamp).ToList();
			return gameMap;
		}
	}

}
