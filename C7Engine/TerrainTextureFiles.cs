namespace C7Engine {
	using System;
	using System.Collections.Generic;
	using System.Runtime.InteropServices;
	using System.Linq;
	using System.Diagnostics;
	using C7GameData;

	// A class encapsulating the details of how terrain texture file images are
	// mapped to tiles.
	public class TerrainTextureFiles {
		// The texture file ids. A leading "x" means it is a land file, and the
		// following three letters are the terrain types (t=tundra, g=grass,
		// p=plains, c=coast, d= desert). A leading "w" is water, coast/sea/ocean.
		private enum TextureFile {
			xtgc = 0,
			xpgc = 1,
			xdgc = 2,
			xdpc = 3,
			xdgp = 4,
			xggc = 5,
			wCSO = 6,
			wSSS = 7,
			wOOO = 8,
		}

		// Assigns the texture file and index to all the tiles in the provided map.
		public static void AssignTextureDetails(Random rand, List<TerrainType> terrainTypes, GameMap m) {
			foreach (Tile t in m.tiles) {
				t.ExtraInfo = new();

				Dictionary<string, int> terrainCounts = new();
				foreach (TerrainType tt in terrainTypes) {
					terrainCounts.Add(tt.Key, 0);
				}

				// Count the terrain types of ourself and our neighbors. Flood
				// plains use the desert graphics, so they count as desert here.
				string[] neighbors = {
					GetNeighborTerrain(t, TileDirection.NORTH),
					GetNeighborTerrain(t, TileDirection.NORTHWEST),
					GetNeighborTerrain(t, TileDirection.NORTHEAST),
					TextureTerrainKey(t),
				};
				foreach (string s in neighbors) {
					terrainCounts[s] += 1;
				}

				// Count the land and water neighbors.
				int waterNeighbors = terrainCounts["ocean"] + terrainCounts["sea"] + terrainCounts["coast"];
				int uniqueNeighbors = 0;
				foreach (var (key, value) in terrainCounts) {
					if (value > 0) {
						++uniqueNeighbors;
					}
				}

				if (waterNeighbors == 4) {
					if (terrainCounts["ocean"] == 4) {
						// All ocean, so use the ocean file with a random texture.
						t.ExtraInfo.BaseTerrainFileID = (int)TextureFile.wOOO;
						t.ExtraInfo.BaseTerrainImageID = rand.Next(81);
					} else if (terrainCounts["sea"] == 4) {
						// All sea, so use the ocean file with a random texture.
						t.ExtraInfo.BaseTerrainFileID = (int)TextureFile.wSSS;
						t.ExtraInfo.BaseTerrainImageID = rand.Next(81);
					} else {
						SetExtraInfo(t, neighbors, TextureFile.wCSO);
					}
				} else if (terrainCounts["tundra"] > 0) {
					// Fun fact - tundra can only border grassland and coast, so
					// this is sufficient for all tundra cases.
					SetExtraInfo(t, neighbors, TextureFile.xtgc);
				} else if (uniqueNeighbors >= 3) {
					// 3 distinct types (must be combinations of D, P, G, C)
					bool hasCoast = terrainCounts["coast"] > 0;
					bool hasDesert = terrainCounts["desert"] > 0;
					bool hasPlains = terrainCounts["plains"] > 0;
					bool hasGrass = terrainCounts["grassland"] > 0;

					if (!hasCoast) { // D/G/P only
						SetExtraInfo(t, neighbors, TextureFile.xdgp);
					} else if (!hasGrass) { // D/P/C
						SetExtraInfo(t, neighbors, TextureFile.xdpc);
					} else if (!hasDesert) { // P/G/C
						SetExtraInfo(t, neighbors, TextureFile.xpgc);
					} else { // D/G/C (Plains must be absent)
						SetExtraInfo(t, neighbors, TextureFile.xdgc);
					}
				} else if (uniqueNeighbors == 2) {
					// 2 distinct types (D/P, D/G, P/G, D/C, P/C, G/C)
					bool hasDesert = terrainCounts["desert"] > 0;
					bool hasPlains = terrainCounts["plains"] > 0;
					bool hasGrass = terrainCounts["grassland"] > 0;
					bool hasCoast = terrainCounts["coast"] > 0;

					if (hasGrass && hasCoast) SetExtraInfo(t, neighbors, TextureFile.xpgc);
					else if (hasPlains && hasCoast) SetExtraInfo(t, neighbors, TextureFile.xpgc);
					else if (hasDesert && hasCoast) SetExtraInfo(t, neighbors, TextureFile.xdpc);
					else if (hasGrass && hasPlains) SetExtraInfo(t, neighbors, TextureFile.xpgc);
					else if (hasGrass && hasDesert) SetExtraInfo(t, neighbors, TextureFile.xdgc);
					else if (hasPlains && hasDesert) SetExtraInfo(t, neighbors, TextureFile.xdpc);
					else {
						throw new Exception($"Unexpected possibility: {hasDesert} {hasPlains} {hasGrass} {hasCoast} {string.Join(",", terrainCounts)}");
					}
				} else if (uniqueNeighbors == 1) {
					if (terrainCounts["grassland"] > 0) SetExtraInfo(t, neighbors, TextureFile.xdgc);
					else if (terrainCounts["plains"] > 0) SetExtraInfo(t, neighbors, TextureFile.xpgc);
					else if (terrainCounts["desert"] > 0) SetExtraInfo(t, neighbors, TextureFile.xdgp);
					else {
						throw new Exception($"Unexpected possibility: {string.Join(",", terrainCounts)}");
					}
				} else {
					throw new Exception($"Weird number of unique neighbors: {uniqueNeighbors}");
				}
			}
		}

		private static void SetExtraInfo(Tile t, string[] neighbors, TextureFile textureFile) {
			t.ExtraInfo.BaseTerrainFileID = (int)textureFile;
			t.ExtraInfo.BaseTerrainImageID = GetTextureFileIndex(neighbors, textureFile);
		}

		// A given texture file contains the intersections for 3 different 
		// terrain types. For example, xtgc has intersections for tundra,
		// grassland, and coast tiles.
		//
		// To figure out which of the 81 images in a texture file a tile should
		// use, we need to look at the terrain types of the tile itself and the
		// neighbors to the N/NW/NE.
		//
		// The index within the file (0 to 80 inclusive) is determined by weights
		// that differ based on the neighbor and the terrain type. Again for the
		// xtgc example, tundra tiles get the "start" weights, since t is the
		// first terrain type. Grassland gets the "middle" weights" as the
		// second terrain type, and coast gets the "end" weights.
		//
		// Using those weights, we then look up which neighbor/tile has that
		// terrain. So for an example intersection where N=grass, NW=coast,
		// NE=tundra, and center=coast, we would have:
		//
		//   middle[0] + end[1] + start[2] + end[3]
		//    = 1      + 6      + 0        + 54
		//    = 61
		//    
		private static int GetTextureFileIndex(string[] neighbors, TextureFile textureFile) {
			// Cumulative weights based on position index {0, 1, 2, 3} -> {N, NW, NE, Center}
			int[] cumulativeStartWeights = { 0, 0, 0, 0};
			int[] cumulativeMiddleWeights = { 1, 3, 9, 27 };
			int[] cumulativeEndWeights = { 2, 6, 18, 54 };

			int location = 0;
			for (int i = 0; i < 4; i++) {
				string currentType = neighbors[i];
				bool useMiddleWeights = false;
				bool useStartWeights = false;

				// For a given file with terrain types XYZ, X and Z use weight
				// set B, Y uses set A.
				switch (textureFile) {
					case TextureFile.xtgc: // File 0 (T/G/C)
						useMiddleWeights = (currentType == "grassland");
						useStartWeights = (currentType == "tundra");
						break;
					case TextureFile.xpgc: // File 1 (P/G/C)
						useMiddleWeights = (currentType == "grassland");
						useStartWeights = (currentType == "plains");
						break;
					case TextureFile.xdgc: // File 2 (D/G/C)
						useMiddleWeights = (currentType == "grassland");
						useStartWeights = (currentType == "desert");
						break;
					case TextureFile.xdpc: // File 3 (D/P/C)
						useMiddleWeights = (currentType == "plains");
						useStartWeights = (currentType == "desert");
						break;
					case TextureFile.xdgp: // File 4 (D/G/P)
						useMiddleWeights = (currentType == "grassland");
						useStartWeights = (currentType == "desert");
						break;
					case TextureFile.wCSO:
						useMiddleWeights = (currentType == "sea");
						useStartWeights = (currentType == "coast");
						break;
					default:
						throw new Exception($"Unexpected textureFile type: {textureFile}");
						break;
				}

				// Add the cumulative weight for the current position based on the chosen set
				if (useStartWeights) {
					location += cumulativeStartWeights[i];
				} else if (useMiddleWeights) {
					location += cumulativeMiddleWeights[i];
				} else {
					location += cumulativeEndWeights[i];
				}
			}
			return location;
		}

		private static string GetNeighborTerrain(Tile t, TileDirection dir) {
			Tile neighbor = t.neighbors[dir];
			if (neighbor == Tile.NONE) {
				return "coast";
			}
			return TextureTerrainKey(neighbor);
		}

		// The Civ3 terrain a tile renders as, for the purposes of the base
		// terrain textures. Flood plains only appear next to rivers, which
		// Civ3 draws with the desert terrain graphics - and the triple sheets
		// have no flood plain entry - so they use the desert files.
		private static string TextureTerrainKey(Tile t) {
			return t.baseTerrainType.Key == "flood plain" ? "desert" : t.baseTerrainType.Key;
		}
	}
}
