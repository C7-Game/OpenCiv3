namespace C7Engine {
	using System;
	using System.Collections.Generic;
	using System.Runtime.InteropServices;
	using System.Linq;
	using Serilog;
	using System.Diagnostics;
	using C7GameData;
	using C7GameData.Save;

	public class WorldCharacteristics {
		public enum Climate {
			Arid,
			Normal,
			Wet,
		}
		public Climate climate;

		public enum Landform {
			Archipelago,
			Continents,
			Pangaea,
		}
		public Landform landform;

		public enum OceanCoverage {
			Percent_60 = 60,
			Percent_70 = 70,
			Percent_80 = 80,
		}
		public OceanCoverage oceanCoverage;

		public enum Temperature {
			Cool,
			Temperate,
			Warm,
		}
		public Temperature temperature;

		public enum Age {
			Billion_3,
			Billion_4,
			Billion_5,
		}
		public Age age;

		public BarbarianActivity barbarianActivity;

		public int mapSeed = -1;
		public WorldSize worldSize;
		public List<TerrainType> terrainTypes;
		public List<Resource> resources = new();
		public Government defaultGovernment = new();

		public int maxRankOfWorkableTiles;
		public int maxRankOfBarbarianCampTiles;

		public WorldCharacteristics() { }

		public WorldCharacteristics(SaveGame save) {
			terrainTypes = save.TerrainTypes;
			resources = save.Resources;
			defaultGovernment = save.Governments.Find(g => g.defaultType);

			maxRankOfWorkableTiles = save.Rules.MaxRankOfWorkableTiles;
			maxRankOfBarbarianCampTiles = save.Rules.MaxRankOfBarbarianCampTiles;

			barbarianActivity = save.BarbarianInfo.barbarianActivity;
		}
	}

	public class MapGenerator {
		private static ILogger log = Log.ForContext<MapGenerator>();

		private const int MIN_TILES_PER_PLAYER_ISLAND = 81;

		// The entry point to the overall map generation process.
		public static GameMap GenerateMap(WorldCharacteristics wc) {
			if (wc.mapSeed == -1) {
				log.Information("Random seed is not specified, generating...");
				wc.mapSeed = new Random().Next(int.MaxValue);
			}
			log.Information("Seed: " + wc.mapSeed);

			// Step 0: Sanitize world characteristics
			SanitizeWorldCharacteristics(wc);

			// Step 1: generate the general shape of the terrain.
			GameMap gameMap = GenerateTerrainShape(wc);

			// Step 2: ensure we have a proper continental shelf around each
			// landmass, with land->coast->sea->ocean transitions.
			FixContinentalShelf(wc, gameMap);

			// Step 3: add hills and mountains.
			AddHillsAndMountains(wc, gameMap);

			// Step 4: use random walks to split each landmass up into potential
			// biome zones, which we will then assign in step 4 based on the
			// world characteristics.
			SplitContinentsIntoPotentialBiomes(wc, gameMap);

			// Step 5: Using the temperature information, assign each potential
			// biome region a specific biome: grassland, plains, desert, tundra
			AssignBiomes(wc, gameMap);

			// Step 6: add vegetation where appropriate.
			AddVegetation(wc, gameMap);

			// Step 7: Add rivers that start from high points and flow to low
			// points.
			AddRivers(wc, gameMap);

			// Step 7.5: Convert desert to flood plains. Civ3 does this after
			// rivers are placed, so a flood plain always has a river running
			// through it. Doing it here also means resources get placed on
			// flood plains using the flood plain rules (e.g. only wheat).
			AddFloodPlains(wc, gameMap);

			// Step 8: Add resources (luxury/strategic/bonus).
			AddResources(wc, gameMap);

			// Step 9: Add bonus grassland. We do this after assigning resources
			// to make sure resources get placed - we don't want to steal spots
			// for resources.
			AddBonusGrasslands(wc, gameMap);

			// Step 10: Place player starting locations
			// Placing this before barb camps so barbs don't spawn <5 tiles away from players
			// Tried placing barbs then players, but caused all players to spawn together
			DetermineStartingLocations(wc, gameMap);

			// Step 11: Add barb camps. We do this towards the end to make sure
			// that we don't have barb camps on top of resources.
			// Previously step 10
			AddBarbarianCamps(wc, gameMap);

			// TODO: Goody huts, barbarian camps.

			// Last step: Assign the terrain file and image ids to each tile so
			// we know which texture to use when displaying them.
			TerrainTextureFiles.AssignTextureDetails(new Random(wc.mapSeed + 0xebac), wc.terrainTypes, gameMap);

			return gameMap;
		}

		private static void SanitizeWorldCharacteristics(WorldCharacteristics wc) {
			// TODO: Supporting maps of odd dimensions should be doable, but beyond current tiling implementation 
			// As a mitigation, we simply shrink the map dimensions a bit 

			if (wc.worldSize.width % 2 == 1) {
				log.Warning("Uneven map width. Shrinking by one.");
				wc.worldSize.width -= 1;
			}

			if (wc.worldSize.height % 2 == 1) {
				log.Warning("Uneven map height. Shrinking by one.");
				wc.worldSize.height -= 1;
			}
		}

		private static GameMap GenerateTerrainShape(WorldCharacteristics wc) {
			int width = wc.worldSize.width;
			int height = wc.worldSize.height;
			WorldCharacteristics.OceanCoverage oceanCoverage = wc.oceanCoverage;
			WorldCharacteristics.Landform landform = wc.landform;
			int mapSeed = wc.mapSeed;

			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();

			int maxAttempts = 30;
			for (int attempt = 0; attempt < maxAttempts; ++attempt) {
				HeightMap hm = new(seed: mapSeed + 0x1234 * attempt, width:width, height:height, scale:GetNoiseScale(landform));
				GameMap m = ToLandAndWaterGameMap(wc, hm, oceanCoverage);

				if (MapIsAcceptable(wc, m)) {
					stopwatch.Stop();
					log.Information($"Map gen took {attempt} attempts and {stopwatch.ElapsedMilliseconds} milliseconds");
					return m;
				}

				if (attempt == maxAttempts - 1) {
					log.Information($"Bailing out of generating a {landform} map");
					return m;
				}
			}

			return null;
		}

		private static GameMap ToLandAndWaterGameMap(WorldCharacteristics wc, HeightMap hm, WorldCharacteristics.OceanCoverage oceanCoverage) {
			TerrainType grassland = wc.terrainTypes.Find(x => x.Key == "grassland");
			TerrainType coast = wc.terrainTypes.Find(x => x.Key == "coast");
			TerrainType sea = wc.terrainTypes.Find(x => x.Key == "sea");
			TerrainType ocean = wc.terrainTypes.Find(x => x.Key == "ocean");

			// Set up the game map with basic land and water tiles.
			GameMap m = new GameMap();
			m.numTilesTall = hm.mapHeight;
			m.numTilesWide = hm.mapWidth;
			m.wrapHorizontally = hm.wrapX;
			m.wrapVertically = hm.wrapY;
			m.optimalNumberOfCities = wc.worldSize.optimalNumberOfCities;
			m.techRate = wc.worldSize.techRate;

			ID.Factory factory = new();
			int seaLevel = hm.FindSeaLevel((int)oceanCoverage);
			int mediumWaterLevel = hm.FindSeaLevel((int)((int)oceanCoverage * .9));
			int deepWaterLevel = hm.FindSeaLevel((int)((int)oceanCoverage * .7));

			for (int Y = 0; Y < m.numTilesTall; Y++) {
				for (int X = Y % 2; X < m.numTilesWide; X += 2) {
					Tile newTile = new Tile(factory.CreateID("tile"));
					newTile.XCoordinate = X;
					newTile.YCoordinate = Y;

					int height = hm.GetHeight(X, Y);
					if (height > seaLevel) {
						newTile.baseTerrainType = grassland;
					} else if (height > mediumWaterLevel) {
						newTile.baseTerrainType = coast;
						newTile.overlayTerrainType = coast;
					} else if (height > deepWaterLevel) {
						newTile.baseTerrainType = sea;
						newTile.overlayTerrainType = sea;
					} else {
						newTile.baseTerrainType = ocean;
						newTile.overlayTerrainType = ocean;
					}
					m.tiles.Add(newTile);
				}
			}

			// Calculate neighbors and continents.
			m.computeNeighbors();
			m.recomputeContinents();

			return m;
		}

		private static double GetNoiseScale(WorldCharacteristics.Landform landform) {
			return landform switch {
				WorldCharacteristics.Landform.Archipelago => 0.2,
				WorldCharacteristics.Landform.Continents => 0.05,
				WorldCharacteristics.Landform.Pangaea => 0.02,
			};
		}

		private static bool MapIsAcceptable(WorldCharacteristics wc, GameMap m) {
			WorldCharacteristics.OceanCoverage oceanCoverage = wc.oceanCoverage;
			WorldCharacteristics.Landform landform = wc.landform;
			int totalTiles = m.tiles.Count;
			int expectedLandTiles = (int)(totalTiles * (1 - (int)oceanCoverage/100.0));

			// Count the tiles that are too close to the poles.
			int tilesInTopOrBottom10Percent = 0;
			foreach (Tile t in m.tiles) {
				if (!t.IsLand()) {
					continue;
				}
				if (t.YCoordinate <= .1 * m.numTilesTall || t.YCoordinate >= .9 * m.numTilesTall) {
					++tilesInTopOrBottom10Percent;
				}
			}

			if (landform == WorldCharacteristics.Landform.Continents) {
				// Ensure that we have at least 3 land continents, that the
				// largest isn't more than 2/3rds the land, and the second largest
				// isn't less than 1/3 the land. This should promote cases
				// where we have 2 large land masses.
				List<HashSet<Tile>> landContinents = m.continents.Where(x => x.First().IsLand()).ToList();
				if (landContinents.Count < 3) {
					return false;
				}

				if (landContinents[0].Count > 1.3 * landContinents[1].Count) {
					return false;
				}

				if (tilesInTopOrBottom10Percent > .1 * expectedLandTiles) {
					return false;
				}

				return true;
			}

			if (landform == WorldCharacteristics.Landform.Pangaea) {
				// Ensure that the largest landmass has at least 80% of the land.
				List<HashSet<Tile>> landContinents = m.continents.Where(x => x.First().IsLand()).ToList();
				if (landContinents[0].Count < .8 * expectedLandTiles) {
					return false;
				}

				// Don't allow maps where the land clumps at the north and south
				// poles.
				if (tilesInTopOrBottom10Percent > .2 * expectedLandTiles) {
					return false;
				}

				return true;
			}

			if (landform == WorldCharacteristics.Landform.Archipelago) {
				// Ensure that we have more land continents than players.
				List<HashSet<Tile>> landContinents = m.continents.Where(x => x.First().IsLand()).ToList();
				if (landContinents.Count < wc.worldSize.numberOfCivs * 1.2) {
					return false;
				}

				// Don't have any islands so large that we'd have more than 3
				// players on them.
				if (landContinents[0].Count > MIN_TILES_PER_PLAYER_ISLAND * 3) {
					return false;
				}

				// Give each player at least the minimum tiles number of . Make
				// sure that we have enough islands for that.
				int playersLeft = wc.worldSize.numberOfCivs;
				int islandsWithMultiplePlayers = 0;
				foreach (var continent in landContinents) {
					playersLeft -= continent.Count / MIN_TILES_PER_PLAYER_ISLAND;
					if (continent.Count / MIN_TILES_PER_PLAYER_ISLAND > 1) {
						++islandsWithMultiplePlayers;
					}
				}
				if (playersLeft > 0) {
					return false;
				}

				// Make sure most players are by themselves on an island.
				if (islandsWithMultiplePlayers > wc.worldSize.numberOfCivs * .4) {
					return false;
				}

				return true;
			}

			return true;
		}

		private static void AddHillsAndMountains(WorldCharacteristics wc, GameMap m) {
			// Generate a new height map that has lots of smaller blobs, to avoid
			// having one giant mountain range.
			HeightMap hm = new(seed: wc.mapSeed + 0xfeed, width:wc.worldSize.width, height:wc.worldSize.height, scale:.2);
			Random random = new Random(wc.mapSeed + 0xface);

			TerrainType hills = wc.terrainTypes.Find(x => x.Key == "hills");
			TerrainType mountains = wc.terrainTypes.Find(x => x.Key == "mountains");
			TerrainType volcano = wc.terrainTypes.Find(x => x.Key == "volcano");

			// Figure out the height bands for hills and mountains. We use bands
			// so that we can have "elevated grasslands" and so that we don't
			// get large chunks of the map just being hills/mountains, which
			// isn't useful for cities.
			int hillLowerBound, hillUpperBound;
			int mountainLowerBound, mountainUpperBound;

			// The percentage of mountains that will become volcanos.
			int volcanoPercent;

			switch (wc.age) {
				case WorldCharacteristics.Age.Billion_3:
					hillLowerBound = hm.FindSeaLevel(15);
					hillUpperBound = hm.FindSeaLevel(30);
					mountainLowerBound = hm.FindSeaLevel(60);
					mountainUpperBound = hm.FindSeaLevel(75);
					volcanoPercent = 5;
					break;
				case WorldCharacteristics.Age.Billion_4:
					hillLowerBound = hm.FindSeaLevel(20);
					hillUpperBound = hm.FindSeaLevel(30);
					mountainLowerBound = hm.FindSeaLevel(65);
					mountainUpperBound = hm.FindSeaLevel(75);
					volcanoPercent = 3;
					break;
				case WorldCharacteristics.Age.Billion_5:
					hillLowerBound = hm.FindSeaLevel(23);
					hillUpperBound = hm.FindSeaLevel(30);
					mountainLowerBound = hm.FindSeaLevel(68);
					mountainUpperBound = hm.FindSeaLevel(75);
					volcanoPercent = 1;
					break;
				default:
					throw new Exception($"Unknown age: {wc.age}");
			}

			foreach (Tile t in m.tiles) {
				if (!t.IsLand()) {
					continue;
				}

				int height = hm.GetHeight(t.XCoordinate, t.YCoordinate);
				if (height >= hillLowerBound && height < hillUpperBound) {
					t.overlayTerrainType = hills;
					continue;
				}
				if (height >= mountainLowerBound && height < mountainUpperBound) {
					if (random.Next(100) < volcanoPercent) {
						t.overlayTerrainType = volcano;
					} else {
						t.overlayTerrainType = mountains;
					}
				}
			}
		}

		// Assigns a biome id to each land tile, creating potential biome zones
		// that will later be assigned to actual biomes.
		private static void SplitContinentsIntoPotentialBiomes(WorldCharacteristics wc, GameMap m) {
			// Randomize the order we iterate through the tiles.
			Random rand = new(wc.mapSeed + 0xba11);
			List<int> tileIndicies = Enumerable.Range(0, m.tiles.Count).ToList();
			rand.Shuffle<int>(CollectionsMarshal.AsSpan(tileIndicies));

			int nextBiomeId = 0;
			foreach (int tileIndex in tileIndicies) {
				Tile t = m.tiles[tileIndex];

				// Skip tile that have been handled or tiles that already have
				// their final assignment.
				if (t.biomeRegion != -1 || !t.IsLand() || t.overlayTerrainType.isHilly()) {
					continue;
				}

				// Do a random walk to find tiles that haven't been assigned and
				// then we "bump out" in each direction from those tiles by one,
				// with some probability.
				HashSet<Tile> updated = RandomWalkAndAssignBiomeId(rand, t, walkLength:16, biomeId:nextBiomeId);
				SemiRandomFloodFillExpansion(rand, updated, nextBiomeId);
				++nextBiomeId;
			}

			// Fix up the biome regions to avoid 1 tile biomes.
			foreach (int tileIndex in tileIndicies) {
				Tile t = m.tiles[tileIndex];
				if (!t.IsLand() || t.overlayTerrainType.isHilly()) {
					continue;
				}

				Tile nw = t.neighbors[TileDirection.NORTHWEST];
				Tile sw = t.neighbors[TileDirection.SOUTHWEST];
				Tile ne = t.neighbors[TileDirection.NORTHEAST];
				Tile se = t.neighbors[TileDirection.SOUTHEAST];

				if ((t.biomeRegion == nw.biomeRegion && nw.biomeRegion != -1)
					|| (t.biomeRegion == ne.biomeRegion && ne.biomeRegion != -1)
					|| (t.biomeRegion == sw.biomeRegion && sw.biomeRegion != -1)
					|| (t.biomeRegion == se.biomeRegion && se.biomeRegion != -1)) {
					continue;
				}

				List<Tile> validNeighbors = new();
				if (nw != Tile.NONE && nw.biomeRegion != -1) validNeighbors.Add(nw);
				if (sw != Tile.NONE && sw.biomeRegion != -1) validNeighbors.Add(sw);
				if (ne != Tile.NONE && ne.biomeRegion != -1) validNeighbors.Add(ne);
				if (se != Tile.NONE && se.biomeRegion != -1) validNeighbors.Add(se);

				// This could be an island.
				if (validNeighbors.Count == 0) {
					continue;
				}

				t.biomeRegion = validNeighbors[rand.Next(validNeighbors.Count)].biomeRegion;
			}
		}

		private static HashSet<Tile> RandomWalkAndAssignBiomeId(Random rand, Tile t, int walkLength, int biomeId) {
			HashSet<Tile> result = new();

			for (int i = 0; i < walkLength; ++i) {
				if (t.biomeRegion != -1 || !t.IsLand() || t.overlayTerrainType.isHilly()) {
					continue;
				}
				t.biomeRegion = biomeId;
				result.Add(t);

				TileDirection[] options = {
					TileDirection.NORTH,
					TileDirection.SOUTH,
					TileDirection.EAST,
					TileDirection.WEST,
				};
				rand.Shuffle<TileDirection>(options);

				// Try to move in a random direction to a tile that hasn't been
				// processed.
				bool moved = false;
				foreach (TileDirection dir in options) {
					Tile neighbor = t.neighbors[dir];
					if (neighbor.biomeRegion != -1 || !neighbor.IsLand() || neighbor.overlayTerrainType.isHilly() || neighbor == Tile.NONE) {
						continue;
					}
					t = neighbor;
					moved = true;
					break;
				}

				if (!moved) {
					return result;
				}
			}
			return result;
		}

		private static void SemiRandomFloodFillExpansion(Random rand, HashSet<Tile> tiles, int biomeId) {
			List<Tile> toUpdate = new();

			foreach (Tile t in tiles) {
				foreach (Tile neighbor in t.neighbors.Values) {
					if (neighbor.biomeRegion != -1 || !neighbor.IsLand() || neighbor.overlayTerrainType.isHilly() || t == Tile.NONE) {
						continue;
					}

					if (rand.Next(100) < 50) {
						toUpdate.Add(neighbor);
					}
				}
			}

			foreach (Tile t in toUpdate) {
				t.biomeRegion = biomeId;
			}
		}

		private static void AssignBiomes(WorldCharacteristics wc, GameMap m) {
			TerrainType tundra = wc.terrainTypes.Find(x => x.Key == "tundra");
			TerrainType grassland = wc.terrainTypes.Find(x => x.Key == "grassland");
			TerrainType desert = wc.terrainTypes.Find(x => x.Key == "desert");
			TerrainType plains = wc.terrainTypes.Find(x => x.Key == "plains");

			// Like with mountains, use a very blobby scale for our temperature map.
			HeightMap hm = new(seed: wc.mapSeed + 0xdad, width:wc.worldSize.width, height:wc.worldSize.height, scale:.4);

			// Tundra also has a latitude threshold, not just a temperature
			// check, to prevent equatorial tundras.
			int tundraLatitudeThreshold;

			int plainsLowerBound, plainsUpperBound;
			int desertLowerBound, desertUpperBound;
			int tundraLowerBound, tundraUpperBound;

			// While biomes are mostly affected by temperature, give plains and
			// deserts a boost when there is less moisture.
			int aridPlainsBoost = wc.climate == WorldCharacteristics.Climate.Arid ? 8 : 0;
			int aridDesertBoost = wc.climate == WorldCharacteristics.Climate.Arid ? 5 : 0;

			switch (wc.temperature) {
				case WorldCharacteristics.Temperature.Cool:
					// About where Minneapolis is - in the last ice age glaciers
					// went lower, but we don't want all tundra.
					tundraLatitudeThreshold = 45;

					// For all the bounds here we're picking arbitrary temperature
					// bounds. So roughly 23% of the map is eligible for being
					// tundra here, though the latitude threshold also comes into
					// play later.
					tundraLowerBound = hm.FindSeaLevel(70);
					tundraUpperBound = hm.FindSeaLevel(93);

					// Roughly 15% of the map is eligible for plains, though this
					// can be boosted for arid maps.
					plainsLowerBound = hm.FindSeaLevel(15);
					plainsUpperBound = hm.FindSeaLevel(30 + aridPlainsBoost);

					// Another 15% for deserts.
					desertLowerBound = hm.FindSeaLevel(45);
					desertUpperBound = hm.FindSeaLevel(60 + aridDesertBoost);
					break;
				case WorldCharacteristics.Temperature.Temperate:
					// About where Stockholm is.
					tundraLatitudeThreshold = 59;

					tundraLowerBound = hm.FindSeaLevel(77);
					tundraUpperBound = hm.FindSeaLevel(93);

					plainsLowerBound = hm.FindSeaLevel(15);
					plainsUpperBound = hm.FindSeaLevel(35 + aridPlainsBoost);

					desertLowerBound = hm.FindSeaLevel(45);
					desertUpperBound = hm.FindSeaLevel(65 + aridDesertBoost);
					break;
				case WorldCharacteristics.Temperature.Warm:
					// About where the arctic circle is today.
					tundraLatitudeThreshold = 66;

					tundraLowerBound = hm.FindSeaLevel(83);
					tundraUpperBound = hm.FindSeaLevel(93);

					plainsLowerBound = hm.FindSeaLevel(15);
					plainsUpperBound = hm.FindSeaLevel(40 + aridPlainsBoost);

					desertLowerBound = hm.FindSeaLevel(45);
					desertUpperBound = hm.FindSeaLevel(70 + aridDesertBoost);
					break;
				default:
					throw new Exception($"Unknown temperature: {wc.temperature}");
			}

			// Randomize the order we iterate through the tiles.
			Random rand = new(wc.mapSeed + 0xba5e);
			List<int> tileIndicies = Enumerable.Range(0, m.tiles.Count).ToList();
			rand.Shuffle<int>(CollectionsMarshal.AsSpan(tileIndicies));

			foreach (int tileIndex in tileIndicies) {
				Tile t = m.tiles[tileIndex];

				// Skip water tiles and tiles that already have their biome
				// assigned.
				if (!t.IsLand() || t.overlayTerrainType != TerrainType.NONE) {
					continue;
				}

				int height = hm.GetHeight(t.XCoordinate, t.YCoordinate);

				// Figure out the latitude of this tile.
				double normalizedY = (double)t.YCoordinate / (m.numTilesTall - 1.0);
				double latitude = Math.Abs(90.0 - (normalizedY * 180.0));

				if (height >= plainsLowerBound && height < plainsUpperBound) {
					FillBiomeRegion(m, t, plains, plains);
				} else if (height >= desertLowerBound && height < desertUpperBound) {
					FillBiomeRegion(m, t, desert, desert);
				} else if (height >= tundraLowerBound && height < tundraUpperBound && latitude > tundraLatitudeThreshold) {
					FillBiomeRegion(m, t, tundra, tundra);
				} else {
					FillBiomeRegion(m, t, grassland, grassland);
				}
			}

			// Do one more pass to ensure that tundra tiles never border land
			// tiles that aren't grassland - the terrain textures don't support
			// anything else.
			foreach (Tile t in m.tiles) {
				if (t.baseTerrainType.Key != "tundra") {
					continue;
				}

				foreach (Tile neighbor in t.neighbors.Values) {
					// We can border water, other tundra, and hills/mountains
					// just fine.
					if (neighbor.baseTerrainType.Key == "tundra" || !neighbor.IsLand() || neighbor == Tile.NONE || neighbor.overlayTerrainType.isHilly()) {
						continue;
					}

					// But anything else has to be grassland.
					neighbor.baseTerrainType = grassland;
					neighbor.overlayTerrainType = grassland;
				}
			}
		}

		private static void FillBiomeRegion(GameMap m, Tile seed, TerrainType baseType, TerrainType overlayType) {
			foreach (Tile t in m.tiles) {
				if (t.biomeRegion != seed.biomeRegion) {
					continue;
				}
				t.baseTerrainType = baseType;
				t.overlayTerrainType = overlayType;
			}
		}

		private static void AddVegetation(WorldCharacteristics wc, GameMap m) {
			TerrainType forest = wc.terrainTypes.Find(x => x.Key == "forest");
			TerrainType jungle = wc.terrainTypes.Find(x => x.Key == "jungle");
			TerrainType marsh = wc.terrainTypes.Find(x => x.Key == "marsh");

			HeightMap hm = new(seed: wc.mapSeed + 0xabcde, width:wc.worldSize.width, height:wc.worldSize.height, scale:.3);

			// For jungle we also rely on latitude, not just moisture.
			int jungleLatitudeThreshold;

			int forestLowerBound, forestUpperBound, forestProbability;
			int jungleLowerBound, jungleUpperBound;
			int marshLowerBound, marshUpperBound;

			switch (wc.climate) {
				case WorldCharacteristics.Climate.Wet:
					jungleLatitudeThreshold = 30;

					forestLowerBound = hm.FindSeaLevel(60);
					forestUpperBound = hm.FindSeaLevel(93);
					forestProbability = 30;

					jungleLowerBound = hm.FindSeaLevel(15);
					jungleUpperBound = hm.FindSeaLevel(35);

					marshLowerBound = hm.FindSeaLevel(55);
					marshUpperBound = hm.FindSeaLevel(60);
					break;
				case WorldCharacteristics.Climate.Normal:
					jungleLatitudeThreshold = 23;

					forestLowerBound = hm.FindSeaLevel(65);
					forestUpperBound = hm.FindSeaLevel(93);
					forestProbability = 20;

					jungleLowerBound = hm.FindSeaLevel(15);
					jungleUpperBound = hm.FindSeaLevel(28);

					marshLowerBound = hm.FindSeaLevel(55);
					marshUpperBound = hm.FindSeaLevel(58);
					break;
				case WorldCharacteristics.Climate.Arid:
					jungleLatitudeThreshold = 20;

					forestLowerBound = hm.FindSeaLevel(70);
					forestUpperBound = hm.FindSeaLevel(93);
					forestProbability = 10;

					jungleLowerBound = hm.FindSeaLevel(15);
					jungleUpperBound = hm.FindSeaLevel(20);

					marshLowerBound = hm.FindSeaLevel(55);
					marshUpperBound = hm.FindSeaLevel(56);
					break;
				default:
					throw new Exception($"Unknown climate: {wc.climate}");
			}

			Random rand = new(wc.mapSeed + 0xe5ab);
			foreach (Tile t in m.tiles) {
				// Skip water tiles and tiles that are hilly.
				if (!t.IsLand() || t.overlayTerrainType.isHilly()) {
					continue;
				}

				int height = hm.GetHeight(t.XCoordinate, t.YCoordinate);

				// Figure out the latitude of this tile.
				double normalizedY = (double)t.YCoordinate / (m.numTilesTall - 1.0);
				double latitude = Math.Abs(90.0 - (normalizedY * 180.0));

				if (height >= forestLowerBound && height < forestUpperBound && rand.Next(100) < forestProbability) {
					// Forests can go on any terrain type.
					t.overlayTerrainType = forest;
				} else if (height >= jungleLowerBound && height < jungleUpperBound
							&& latitude < jungleLatitudeThreshold && t.overlayTerrainType.Key == "grassland") {
					// We only put jungle on grassland.
					t.overlayTerrainType = jungle;
				} else if (height >= marshLowerBound && height < marshUpperBound && t.overlayTerrainType.Key == "grassland") {
					// We only put marsh on grassland.
					t.overlayTerrainType = marsh;
				}
			}
		}

		private static void AddRivers(WorldCharacteristics wc, GameMap m) {
			Random rand = new(wc.mapSeed + 0xabc12);
			HeightMap hm = new(seed: wc.mapSeed + 0x21cba, width:wc.worldSize.width, height:wc.worldSize.height, scale:.1);

			// Find possible places for rivers to start and then shuffle them.
			List<Tile> potentialSources = FindPotentialRiverSources(m);
			rand.Shuffle<Tile>(CollectionsMarshal.AsSpan(potentialSources));

			// Cap the number of rivers based on the amount of land we have.
			int totalTiles = m.tiles.Count;
			int expectedLandTiles = (int)(totalTiles * (1 - (int)wc.oceanCoverage/100.0));
			int maxRivers = (int)(expectedLandTiles / 100.0);
			int riversStarted = 0;

			foreach (Tile t in potentialSources) {
				if (riversStarted == maxRivers) {
					break;
				}

				// Some sources border a river after a previous river was added.
				// Skip those.
				if (t.BordersRiver()) {
					continue;
				}

				TileDirection[] directions = {
					TileDirection.NORTHEAST,
					TileDirection.NORTHWEST,
					TileDirection.SOUTHWEST,
					TileDirection.SOUTHEAST,
				};

				TileDirection bestDir = TileDirection.NORTHEAST;
				int bestScore = int.MinValue;

				foreach (TileDirection dir in directions) {
					int score = calculateRiverDescentScore(hm, t, dir);
					if (score > bestScore) {
						bestDir = dir;
						bestScore = score;
					}
				}

				if (bestScore > int.MinValue) {
					++riversStarted;
					flowRiver(hm, rand, t, bestDir, depth: 0);
				}
			}
		}

		private static bool flowRiver(HeightMap hm, Random rand, Tile t, TileDirection incomingDir, int depth) {
			// Don't let rivers get too long.
			if (depth > 16) {
				return false;
			}

			// We're done if we flowed into water or off the edge.
			//
			// TODO: This doesn't quite seem to avoid the problem of running
			// alongside water.
			if (t == Tile.NONE || !t.IsLand()) {
				return false;
			}

			// Mark ourself as part of the river.
			setRiverFlags(t, incomingDir, depth);

			// Figure out which way to flow next.
			// We use +6 to avoid mods of negative numbers.
			TileDirection[] options = {
				(TileDirection)(((int)incomingDir + 6) % 8), // left
				(TileDirection)(((int)incomingDir + 2) % 8), // right
				incomingDir, // center
			};
			int[] scores = {
				calculateRiverDescentScore(hm, t, options[0]),
				calculateRiverDescentScore(hm, t, options[1]),
				calculateRiverDescentScore(hm, t, options[2]),
			};

			int bestScore = int.MinValue;
			int bestIndex = 0;
			for (int i = 0; i < 3; ++i) {
				if (scores[i] > bestScore) {
					bestIndex = i;
					bestScore = scores[i];
				}
			}

			if (bestScore == int.MinValue) {
				return false; // no flow happened.
			}

			// Flow in the main direction.
			bool flowed = flowRiver(hm, rand, t.neighbors[options[bestIndex]], options[bestIndex], depth + 1);

			// See if there's a second valid flow direction.
			int secondBestScore = int.MinValue;
			int secondBestIndex = 0;

			for (int i = 0; i < 3; ++i) {
				if (scores[i] > secondBestScore && i != bestIndex) {
					secondBestIndex = i;
					secondBestScore = scores[i];
				}
			}

			if (secondBestScore == int.MinValue) {
				return flowed;
			}

			// Only branch if the second best direction is close to the first
			// best direction, with some random chance mixed in.
			if (bestScore < secondBestScore + 30 && rand.Next(100) < 33) {
				flowed |= flowRiver(hm, rand, t.neighbors[options[secondBestIndex]], options[secondBestIndex], depth + 1);
			}

			return flowed;
		}

		// This is super hacky and needs to be improved. The general idea is to
		// prefer to use the SW and SE edges as the river primary edges.
		//
		// Things that need to be improved:
		//   - when corners are turned, sometimes we get a dangly edge
		//   - the N S E W river flags aren't set
		private static void setRiverFlags(Tile t, TileDirection incomingDir, int depth) {
			//             <      >
			//         <  NW  ><  NE  >
			//     <      >< Tile ><      >
			//         <  SW  ><  SE  >
			//             <      >
			if (incomingDir == TileDirection.SOUTHWEST) {
				t.riverSoutheast = true;
				t.neighbors[TileDirection.SOUTHEAST].riverNorthwest = true;
			} else if (incomingDir == TileDirection.NORTHWEST) {
				t.riverSouthwest = true;
				t.neighbors[TileDirection.SOUTHWEST].riverNortheast = true;

				if (t.neighbors[TileDirection.SOUTHEAST].riverSoutheast) {
					t.neighbors[TileDirection.SOUTHEAST].riverSouthwest = true;
					t.neighbors[TileDirection.SOUTHEAST].neighbors[TileDirection.SOUTHWEST].riverNortheast = true;
				}
			} else if (incomingDir == TileDirection.NORTHEAST) {
				t.riverSoutheast = true;
				t.neighbors[TileDirection.SOUTHEAST].riverNorthwest = true;

				if (t.neighbors[TileDirection.SOUTHWEST].riverSouthwest) {
					t.neighbors[TileDirection.SOUTHWEST].riverSoutheast = true;
					t.neighbors[TileDirection.SOUTHWEST].neighbors[TileDirection.SOUTHEAST].riverNorthwest = true;
				}
			} else if (incomingDir == TileDirection.SOUTHEAST) {
				t.riverSouthwest = true;
				t.neighbors[TileDirection.SOUTHWEST].riverNortheast = true;
			}
		}

		private static int calculateRiverDescentScore(HeightMap hm, Tile t, TileDirection direction) {
			Tile neighbor = t.neighbors[direction];

			// Invalid tiles and tiles with rivers shouldn't get new rivers.
			if (neighbor == Tile.NONE || neighbor.BordersRiver()) {
				return int.MinValue;
			}

			TileDirection[] neighborNeighborDirs = {
				(TileDirection)(((int)direction + 6) % 8), // left
				(TileDirection)(((int)direction + 2) % 8), // right
				direction, // center
			};
			foreach (TileDirection dir in neighborNeighborDirs) {
				if (neighbor.neighbors[dir].BordersRiver()) {
					return int.MinValue;
				}
			}

			// Flowing into water is highly encouraged.
			if (!neighbor.IsLand()) {
				return 1000;
			}
			foreach (TileDirection dir in neighborNeighborDirs) {
				if (!neighbor.neighbors[dir].IsLand()) {
					return 500;
				}
			}

			// Compute a score based on the heigh difference, where the neighbor
			// being lower results in a better score.
			int currentHeight = hm.GetHeight(t.XCoordinate, t.YCoordinate);
			int neighborHeight = hm.GetHeight(neighbor.XCoordinate, neighbor.YCoordinate);
			return neighborHeight - currentHeight;
		}

		// Find hilly/mountainous tiles that don't border water.
		private static List<Tile> FindPotentialRiverSources(GameMap m) {
			List<Tile> result = new();
			foreach (Tile t in m.tiles) {
				if (!t.overlayTerrainType.isHilly()) {
					continue;
				}

				bool neighborsWater = false;
				foreach (Tile neighbor in t.neighbors.Values) {
					if (!neighbor.IsLand()) {
						neighborsWater = true;
						break;
					}
				}
				if (neighborsWater) {
					continue;
				}

				result.Add(t);
			}

			return result;
		}

		// In Civ3, any desert tile with a river running along it becomes a
		// flood plain. Only tiles whose terrain is fully desert convert - hills
		// or vegetation on the tile make it a different terrain.
		internal static void AddFloodPlains(WorldCharacteristics wc, GameMap m) {
			TerrainType floodPlain = wc.terrainTypes.Find(x => x.Key == "flood plain");
			if (floodPlain == null) {
				log.Warning("Rules have no 'flood plain' terrain; skipping flood plain generation.");
				return;
			}

			foreach (Tile t in m.tiles) {
				if (!t.IsLand() || t.baseTerrainType.Key != "desert" || t.overlayTerrainType.Key != "desert") {
					continue;
				}

				if (t.BordersRiver()) {
					t.baseTerrainType = floodPlain;
					t.overlayTerrainType = floodPlain;
				}
			}
		}

		private static void FixContinentalShelf(WorldCharacteristics wc, GameMap m) {
			TerrainType coast = wc.terrainTypes.Find(x => x.Key == "coast");
			TerrainType sea = wc.terrainTypes.Find(x => x.Key == "sea");

			// Ensure that land only borders coast.
			foreach (Tile t in m.tiles) {
				if (t.baseTerrainType.Key != "ocean" && t.baseTerrainType.Key != "sea") {
					continue;
				}

				foreach (Tile neighbor in t.neighbors.Values) {
					if (neighbor != Tile.NONE && neighbor.IsLand()) {
						t.baseTerrainType = coast;
						t.overlayTerrainType = coast;
						break;
					}
				}
			}

			// Ensure that coast only borders sea, not ocean.
			foreach (Tile t in m.tiles) {
				if (t.baseTerrainType.Key != "ocean") {
					continue;
				}

				foreach (Tile neighbor in t.neighbors.Values) {
					if (neighbor.baseTerrainType.Key == "coast") {
						t.baseTerrainType = sea;
						t.overlayTerrainType = sea;
						break;
					}
				}
			}
		}

		private static void AddResources(WorldCharacteristics wc, GameMap m) {
			Random rand = new(wc.mapSeed + 0x7171);

			// Randomize the order resources are placed and the order we go
			// through tiles.
			List<Resource> resourcesToPlace = new();
			foreach (Resource r in wc.resources) {
				log.Information(r.Key);
				resourcesToPlace.Add(r);
			}
			rand.Shuffle<Resource>(CollectionsMarshal.AsSpan(resourcesToPlace));

			List<int> tileIndicies = Enumerable.Range(0, m.tiles.Count).ToList();
			rand.Shuffle<int>(CollectionsMarshal.AsSpan(tileIndicies));

			// Luxury resources.
			//
			// Keep track of which continent a luxury resource is first placed
			// to ensure they don't get spread over multiple continents.
			Dictionary<Resource, int> resourceToContinentPlacement = new();
			foreach (Resource r in resourcesToPlace) {
				if (r.Category != ResourceCategory.LUXURY) {
					continue;
				}

				PlaceLuxuryResourceType(rand, wc, m, r, tileIndicies, resourceToContinentPlacement);
			}

			// Strategic resources.
			foreach (Resource r in resourcesToPlace) {
				if (r.Category != ResourceCategory.STRATEGIC) {
					continue;
				}

				PlaceStrategicResourceType(rand, wc, m, r, tileIndicies);
			}

			// Bonus resources.
			PlaceBonusResources(rand, wc, m, resourcesToPlace.Where(x => x.Category == ResourceCategory.BONUS).ToList(), tileIndicies);
		}

		// Determine the rate of appearance for a given resource. The rate of
		// appearance seems to have a default of 100 for civ3, but if it isn't
		// specified (like for luxuries) it is some random value less than 100
		// but always larger than 50.
		private static int GetAppearance(WorldCharacteristics wc, Random rand, Resource r, int minCount) {
			// The the appearance ratio, with a random number between 50 and
			// 100 if it isn't specified. Use multiple random calls to roughly
			// simulate a normal distribution using uniform random samples.
			int baseCount = r.AppearanceRatio;
			if (baseCount == 0) {
				baseCount = 50 + rand.Next(11) + rand.Next(11) + rand.Next(11) + rand.Next(11) + rand.Next(11);
			}

			// Scale the appearance based on the number of civs in the game.
			int targetCount = (wc.worldSize.numberOfCivs * baseCount) / 100;
			targetCount = Math.Max(minCount, targetCount);
			return targetCount;
		}

		private static void PlaceLuxuryResourceType(Random rand, WorldCharacteristics wc, GameMap m, Resource r, List<int> tileIndicies, Dictionary<Resource, int> resourceToContinentPlacement) {
			int targetCount = GetAppearance(wc, rand, r, minCount:1);
			int placed = 0;
			resourceToContinentPlacement[r] = -1;

			for (int i = 0; placed < targetCount && i < tileIndicies.Count; ++i) {
				Tile t = m.tiles[tileIndicies[i]];

				// Skip tiles where we can't place this resource.
				if (!t.overlayTerrainType.allowedResources.Contains(r.Key)) {
					continue;
				}

				// Skip tiles that are already next to a resource.
				if (IsNextToExistingResource(t)) {
					continue;
				}

				// If we have a continent for this luxury and this tile isn't
				// on that continent, skip it.
				if (resourceToContinentPlacement[r] != -1 && resourceToContinentPlacement[r] != t.continent) {
					continue;
				}

				// Skip tiles that don't need the luxury-specific criteria.
				if (!IsValidForLuxuryPlacement(wc, m, r, t)) {
					continue;
				}

				// Place the resource.
				++placed;
				t.Resource = r;
				t.ResourceKey = r.Key;
				resourceToContinentPlacement[r] = t.continent;

				// Give ourselves the chance to place additional instances of
				// this luxury in a clump.
				for (int clusterAttempt = 0; clusterAttempt < 4 && placed < targetCount && rand.Next(100) < 50; ++clusterAttempt) {
					Tile neighbor = t.neighbors.Values
						.Where(x => x != Tile.NONE
									&& x.overlayTerrainType.allowedResources.Contains(r.Key)
									&& x.continent == t.continent)
						.OrderBy(x => rand.Next()) // Shuffle the neighbors
						.FirstOrDefault(Tile.NONE);

					if (neighbor == Tile.NONE) {
						break;
					}
					++placed;
					neighbor.Resource = r;
					neighbor.ResourceKey = r.Key;
				}
			}

			if (placed < targetCount) {
				log.Information($"Only placed {placed} of {targetCount} {r.Key}");
			}
		}

		private static bool IsNextToExistingResource(Tile t) {
			foreach (Tile neighbor in t.neighbors.Values) {
				if (neighbor.Resource != null && neighbor.Resource != Resource.NONE) {
					return true;
				}
			}
			return false;
		}

		private static bool IsValidForLuxuryPlacement(WorldCharacteristics wc, GameMap m, Resource r, Tile t) {
			HashSet<Tile> continent = m.continents.First(x => x.Contains(t));

			// Don't put luxuries on islands too small for players.
			if (continent.Count < MIN_TILES_PER_PLAYER_ISLAND) {
				return false;
			}

			int minLuxurySpacing = (m.numTilesTall + m.numTilesWide) / 40;
			minLuxurySpacing = Math.Max(2, minLuxurySpacing);
			minLuxurySpacing = Math.Min(minLuxurySpacing, 10);

			foreach (Tile x in t.GetTilesWithinRankDistance(minLuxurySpacing)) {
				if (x.Resource != null
					&& x.Resource != Resource.NONE
					&& x.Resource.Category == ResourceCategory.LUXURY
					&& x.Resource.Key != r.Key) {
					return false;
				}
			}

			// If this is a water-based resource, ensure it doesn't end up in the
			// middle of the ocean.
			if (!t.IsLand() && !HasSufficientLandNeighborsForResource(wc, t)) {
				return false;
			}

			return true;
		}

		private static bool HasSufficientLandNeighborsForResource(WorldCharacteristics wc, Tile t) {
			int landTiles = 0;

			foreach (Tile x in t.GetTilesWithinRankDistance(wc.maxRankOfWorkableTiles)) {
				if (x.IsLand()) {
					++landTiles;
				}
			}

			return landTiles >= 1;
		}

		private static void PlaceStrategicResourceType(Random rand, WorldCharacteristics wc, GameMap m, Resource r, List<int> tileIndicies) {
			int targetCount = GetAppearance(wc, rand, r, minCount:2);
			int placed = 0;

			for (int i = 0; placed < targetCount && i < tileIndicies.Count; ++i) {
				Tile t = m.tiles[tileIndicies[i]];

				// Skip tiles where we can't place this resource.
				if (!t.overlayTerrainType.allowedResources.Contains(r.Key)) {
					continue;
				}

				// Skip tiles that are already next to a resource.
				if (IsNextToExistingResource(t)) {
					continue;
				}

				// Skip tiles that don't need the strategic resource-specific criteria.
				if (!IsValidForStrategicResourcePlacement(wc, m, r, t)) {
					continue;
				}

				// Place the resource.
				++placed;
				t.Resource = r;
				t.ResourceKey = r.Key;
			}

			if (placed < targetCount) {
				log.Information($"Only placed {placed} of {targetCount} {r.Key}");
			}
		}

		private static bool IsValidForStrategicResourcePlacement(WorldCharacteristics wc, GameMap m, Resource r, Tile t) {
			HashSet<Tile> continent = m.continents.First(x => x.Contains(t));

			// Don't put strategic resources on super tiny islands - though
			// putting them on small islands is ok.
			if (continent.Count < MIN_TILES_PER_PLAYER_ISLAND / 2) {
				return false;
			}

			int minSpacing = (m.numTilesTall + m.numTilesWide) / 30;
			minSpacing = Math.Max(2, minSpacing);
			minSpacing = Math.Min(minSpacing, 10);

			// Ensure strategic resources of the same kind don't clump up.
			foreach (Tile x in t.GetTilesWithinRankDistance(minSpacing)) {
				if (x.Resource != null
					&& x.Resource != Resource.NONE
					&& x.Resource.Category == ResourceCategory.STRATEGIC
					&& x.Resource.Key == r.Key) {
					return false;
				}
			}

			// If this is a water-based resource, ensure it doesn't end up in the
			// middle of the ocean.
			if (!t.IsLand() && !HasSufficientLandNeighborsForResource(wc, t)) {
				return false;
			}

			return true;
		}

		private static void PlaceBonusResources(Random rand, WorldCharacteristics wc, GameMap m,
												List<Resource> bonusResources, List<int> tileIndicies) {
			int totalPossibleBonusResources = m.tiles.Count / 32;
			int placed = 0;

			Dictionary<Resource, int> terrainScores = CalculateBonusResourceTerrainScores(wc, bonusResources);

			for (int pass = 0; pass < 32 && placed < totalPossibleBonusResources; ++pass) {
				rand.Shuffle<Resource>(CollectionsMarshal.AsSpan(bonusResources));

				foreach (Resource r in bonusResources) {
					int terrainScore = terrainScores[r];

					// Can't be placed anywhere.
					if (terrainScore == 0) {
						continue;
					}

					// Resources that can go in more places have a higher chance
					// of being placed.
					int placementProbability = 25;
					if (terrainScore < 2) {
						placementProbability = 16;
					} else if (terrainScore > 3) {
						placementProbability = 50;
					}

					if (rand.Next(100) >= placementProbability) {
						continue;
					}

					if (PlaceBonusResource(wc, m, r, tileIndicies)) {
						++placed;
					}
				}
			}
		}

		private static bool PlaceBonusResource(WorldCharacteristics wc, GameMap m, Resource r, List<int> tileIndicies) {
			// We want to place this resource. Find the first valid tile
			// we can stick it on.
			//
			// We don't want it next to other resources, and if it is a
			// water resource (fish/whale/etc) don't stick it in the
			// middle of the ocean.
			foreach (int index in tileIndicies) {
				Tile t = m.tiles[index];
				if (!t.overlayTerrainType.allowedResources.Contains(r.Key)) {
					continue;
				}

				bool hasResource = !(t.Resource == Resource.NONE || t.Resource == null);
				if (hasResource || IsNextToExistingResource(t)) {
					continue;
				}

				if (!t.IsLand() && !HasSufficientLandNeighborsForResource(wc, t)) {
					continue;
				}

				t.Resource = r;
				t.ResourceKey = r.Key;
				return true;
			}
			return false;
		}

		private static Dictionary<Resource, int> CalculateBonusResourceTerrainScores(WorldCharacteristics wc, List<Resource> bonusResources) {
			Dictionary<Resource, int> result = new();

			foreach (Resource r in bonusResources) {
				int score = 0;
				foreach (TerrainType tt in wc.terrainTypes) {
					if (tt.allowedResources.Contains(r.Key)) {
						// Make water bonus resources get a higher score, since
						// land bonus resources are generally more valuable.
						if (tt.isWater()) {
							score += 4;
						} else {
							score += 1;
						}
					}
				}
				result[r] = score;
			}

			return result;
		}

		private static void AddBarbarianCamps(WorldCharacteristics wc, GameMap m) {
			Random rand = new(wc.mapSeed + 0xba7b5);
			List<int> tileIndicies = Enumerable.Range(0, m.tiles.Count).ToList();
			rand.Shuffle<int>(CollectionsMarshal.AsSpan(tileIndicies));

			int landTiles = 0;
			foreach (Tile t in m.tiles) {
				if (t.IsLand()) {
					++landTiles;
				}
			}

			int totalPossibleBarbCamps = DeriveTotalPossibleBarbCamps(wc, landTiles);

			int numCamps = 0;
			for (int i = 0; i < tileIndicies.Count && numCamps < totalPossibleBarbCamps; ++i) {
				Tile t = m.tiles[tileIndicies[i]];
				if (IsValidForBarbarianCamp(wc, m, t)) {
					m.barbarianCamps.Add(t);
					t.hasBarbarianCamp = true;
					++numCamps;
				}
			}
		}

		/// <summary>
		/// Apply barbarian activity level to barbarian camp spawn rate. Currently NOT based on Civ3 values.
		/// TODO: Make configurable
		/// TODO: Determine what these values are in Civ3 
		/// </summary>
		private static int DeriveTotalPossibleBarbCamps(WorldCharacteristics wc, int landTiles) {
			var totalCampsBaseline = landTiles / 100;
			switch (wc.barbarianActivity) {
				case BarbarianActivity.None:
					return 0;
				case BarbarianActivity.Sedentary:
					return totalCampsBaseline;
				case BarbarianActivity.Roaming:
					return totalCampsBaseline;
				case BarbarianActivity.Restless:
					return (int)Math.Round(totalCampsBaseline * 1.25); // extra 25%
				case BarbarianActivity.Raging:
					return (int)Math.Round(totalCampsBaseline * 1.50); // extra 50%
				default:
					log.Warning("Unknown Barbarian Activity at barb camps derivation.");
					return totalCampsBaseline;
			}
		}

		private static bool IsValidForBarbarianCamp(WorldCharacteristics wc, GameMap m, Tile t) {
			// No barbarian camps on water, volcanos, or mountains.
			if (!t.IsLand() || t == Tile.NONE || t.overlayTerrainType.Key == "volcano" || t.overlayTerrainType.Key == "mountains") {
				return false;
			}

			// No barbarian camps on luxury or strategic resources (we're ok
			// with barb camps on things like cows or gold).
			if (t.Resource != null && t.Resource != Resource.NONE &&
				(t.Resource.Category == ResourceCategory.STRATEGIC || t.Resource.Category == ResourceCategory.LUXURY)) {
				return false;
			}

			// No barbarian camps on super tiny islands.
			HashSet<Tile> continent = m.continents.First(x => x.Contains(t));
			if (continent.Count < 15) {
				return false;
			}

			// No barbarian camps within the big fat cross of another camp.
			foreach (Tile n in t.GetTilesWithinRankDistance(wc.maxRankOfBarbarianCampTiles)) {
				if (n.hasBarbarianCamp) {
					return false;
				}
			}

			// No barbarian camps less than 6 tiles away from a player
			if (m.startingLocations.Any(x => t.DistanceTo(x) < 6)) return false;

			return true;
		}

		private static void DetermineStartingLocations(WorldCharacteristics wc, GameMap m) {
			Random rand = new(wc.mapSeed + 0x1337);
			List<HashSet<Tile>> landContinents = m.continents.Where(x => x.First().IsLand()).ToList();

			// Count how many luxuries each continent has.
			Dictionary<int, int> continentLuxuryCount = new();
			foreach (HashSet<Tile> continent in landContinents) {
				continentLuxuryCount[continent.First().continent] = 0;
				foreach (Tile t in continent) {
					if (t.Resource.Category == ResourceCategory.LUXURY) {
						continentLuxuryCount[t.continent]++;
					}
				}
			}

			// Find all viable city locations.
			Dictionary<Tile, int> scoredTiles = new();
			foreach (Tile t in m.tiles) {
				int score = ScorePossibleCityLocation(wc, t);
				if (score > 0) {
					scoredTiles[t] = score;
				}
			}
			List<Tile> orderedTiles = scoredTiles.OrderByDescending(t => t.Value).Select(x => x.Key).ToList();

			// Use those to find the appropriate number of starting locations.
			List<Tile> startingLocations = new();
			Dictionary<int, int> continentStartingLocationCount = new();

			for (int attempt = 0; attempt < 10 && startingLocations.Count < wc.worldSize.numberOfCivs; ++attempt) {
				foreach (Tile t in orderedTiles) {
					if (startingLocations.Count == wc.worldSize.numberOfCivs) {
						break;
					}

					if (!IsContinentLargeEnough(m, t, attempt)) {
						continue;
					}

					// TODO: We need to consider the number of seafaring civs,
					// since they need to start on a coastal tile.

					if (!ContinentHasEnoughLuxuries(t, continentLuxuryCount, continentStartingLocationCount, attempt)) {
						continue;
					}

					if (TileIsTooCloseToOtherStarts(t, startingLocations, wc.worldSize.distanceBetweenCivs, attempt)) {
						continue;
					}

					// This is a valid starting location.
					startingLocations.Add(t);
					if (continentStartingLocationCount.ContainsKey(t.continent)) {
						continentStartingLocationCount[t.continent]++;
					} else {
						continentStartingLocationCount[t.continent] = 1;
					}
					log.Information($"Placed start number {startingLocations.Count} on attempt {attempt}");
				}
			}

			if (wc.worldSize.numberOfCivs > startingLocations.Count)
				log.Error("More civs than available starting locations.");

			// Before using the starting locations, shuffle them, so that the
			// human player doesn't always get the best starting spot.
			rand.Shuffle<Tile>(CollectionsMarshal.AsSpan(startingLocations));
			m.startingLocations = startingLocations;
		}

		// Allow smaller continents the more desperate we are to find a starting
		// location. Hopefully map generation will have handled this, but we
		// do bail out of map generation eventually.
		private static bool IsContinentLargeEnough(GameMap m, Tile t, int attempt) {
			HashSet<Tile> continent = m.continents.First(x => x.Contains(t));

			if (attempt < 3) {
				return continent.Count > MIN_TILES_PER_PLAYER_ISLAND;
			} else if (attempt < 6) {
				return continent.Count > MIN_TILES_PER_PLAYER_ISLAND / 2;
			}
			return true;
		}

		// Try to match the behavior of at least one luxury per player per continent.
		private static bool ContinentHasEnoughLuxuries(Tile t, Dictionary<int, int> continentLuxuryCount, Dictionary<int, int> continentStartingLocationCount, int attempt) {
			// Only be strict on the first two passes.
			if (attempt >= 2) {
				return true;
			}

			int luxuriesOnContinent = 0;
			if (continentLuxuryCount.ContainsKey(t.continent)) {
				luxuriesOnContinent = continentLuxuryCount[t.continent];
			}

			int startsOnContinent = 0;
			if (continentStartingLocationCount.ContainsKey(t.continent)) {
				startsOnContinent = continentStartingLocationCount[t.continent];
			}

			return startsOnContinent < luxuriesOnContinent;
		}

		private static bool TileIsTooCloseToOtherStarts(Tile t, List<Tile> startingLocations, int minDistance, int attempt) {
			if (attempt > 2) {
				minDistance /= 2;
			}

			if (attempt > 3) {
				minDistance -= attempt;
				minDistance = Math.Max(minDistance, 3); // hard floor
			}

			foreach (Tile start in startingLocations) {
				if (start.continent == t.continent && start.DistanceTo(t) < minDistance) {
					return true;
				}
			}

			return false;
		}

		// TODO: merge this with the ai logic
		private static int ScorePossibleCityLocation(WorldCharacteristics wc, Tile t) {
			if (!t.IsAllowCities()) {
				return int.MinValue;
			}

			const int CommercePoints = 4;
			const int ShieldPoints = 12;
			const int FoodPoints = 20;
			const int RiverPoints = 35;
			const int CoastPoints = 30;
			const int LandTilePoints = 1;
			const int BarbarianCampPoints = -50;

			Player player = new();
			player.government = wc.defaultGovernment;

			// Calculate the score for tiles in the immediate area.
			int score = 0;
			foreach (Tile n in t.GetTilesWithinRankDistance(1)) {
				score += CommercePoints * n.CommerceYield(player).yield;
				score += ShieldPoints * n.ProductionYield(player).yield;
				score += FoodPoints * n.FoodYield(player).yield;
				if (n.hasBarbarianCamp) {
					score += BarbarianCampPoints;
				}

				if (!n.IsLand()) {
					score += CoastPoints;
				}
			}

			// Then do it again for the full big fat cross, effectively weighting
			// the immediate neighbors at a 2x rate.
			foreach (Tile n in t.GetTilesWithinRankDistance(wc.maxRankOfWorkableTiles)) {
				score += CommercePoints * n.CommerceYield(player).yield;
				score += ShieldPoints * n.ProductionYield(player).yield;
				score += FoodPoints * n.FoodYield(player).yield;
				if (n.hasBarbarianCamp) {
					score += BarbarianCampPoints;
				}
			}

			// Give an extra bonus for rivers.
			// TODO: freshwater lakes?
			if (t.BordersRiver()) {
				score += RiverPoints;
			}

			// Try to get a rough sense of the number of surrounding land tiles
			// on the same continent, to avoid players getting stuck on a
			// peninsula.
			foreach (Tile n in t.GetTilesWithinRankDistance(4)) {
				if (n.continent == t.continent) {
					score += LandTilePoints;
				}
			}

			return score;
		}

		public static void AddBonusGrasslands(WorldCharacteristics wc, GameMap m) {
			// Randomize the order we iterate through the tiles.
			Random rand = new(wc.mapSeed + 0x7361);
			List<int> tileIndicies = Enumerable.Range(0, m.tiles.Count).ToList();
			rand.Shuffle<int>(CollectionsMarshal.AsSpan(tileIndicies));

			foreach (int index in tileIndicies) {
				Tile t = m.tiles[index];

				// We only want grassland tiles without hills. A forest/jungle/marsh
				// is ok, since that can be cleared.
				if (t.baseTerrainType.Key != "grassland") {
					continue;
				}

				// Each grassland tile has a 25% chance.
				if (rand.Next(100) >= 25) {
					continue;
				}

				// We can't have bonus grassland under a resource/luxury.
				bool hasResource = !(t.Resource == Resource.NONE || t.Resource == null);
				if (hasResource) {
					continue;
				}

				// We can't have bonus grassland under a hill/mountain.
				if (t.overlayTerrainType.isHilly()) {
					continue;
				}

				t.isBonusShield = true;
			}
		}
	}
}
