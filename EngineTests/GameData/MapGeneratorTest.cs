using System;
using System.Collections.Generic;
using C7Engine;
using C7GameData;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace EngineTests.GameData;

public class GameMapGeneratorTest {
	public static void SaveMapsAsWaterLandPng(List<List<GameMap>> maps, string filePath) {
		int mapWidthPx = maps[0][0].numTilesWide * 2 + 1;
		int mapHeightPx = maps[0][0].numTilesTall + 1;

		Bgra32 grass = new((byte)60,(byte)190,(byte)110);
		Bgra32 coastBlue = new((byte)62, (byte)164, (byte)240);
		Bgra32 seaBlue = new((byte)88, (byte)141, (byte)195);
		Bgra32 oceanBlue = new((byte)0, (byte)105, (byte)148);
		Bgra32 hillsGreen = new((byte)2, (byte)90, (byte)60);
		Bgra32 mountainBrown = new((byte)70, (byte)65, (byte)40);
		Bgra32 tundra = new((byte)241, (byte)245, (byte)241);
		Bgra32 desert = new((byte)231, (byte)161, (byte)112);
		Bgra32 floodPlain = new((byte)250, (byte)210, (byte)110);
		Bgra32 plains = new((byte)88, (byte)57, (byte)39);
		Bgra32 jungle = new((byte)0, (byte)60, (byte)0);
		Bgra32 forest = new((byte)107,(byte)142,(byte)35);
		Bgra32 marsh = new((byte)46,(byte)139,(byte)87);

		using (Image<Bgra32> image = new(mapWidthPx * maps.Count, mapHeightPx * maps[0].Count)) {
			for (int i = 0; i < maps.Count; ++i) {
				for (int k = 0; k < maps[i].Count; ++k) {
					int xOffset = i * mapWidthPx;
					int yOffset = k * mapHeightPx;

					GameMap m = maps[i][k];
					foreach (Tile t in m.tiles) {
						Bgra32 color;
						if (t.overlayTerrainType.Key == "mountains" || t.overlayTerrainType.Key == "volcano") {
							color = mountainBrown;
						} else if (t.overlayTerrainType.Key == "hills") {
							color = hillsGreen;
						} else if (t.overlayTerrainType.Key == "tundra") {
							color = tundra;
						} else if (t.overlayTerrainType.Key == "desert") {
							color = desert;
						} else if (t.overlayTerrainType.Key == "flood plain") {
							color = floodPlain;
						} else if (t.overlayTerrainType.Key == "plains") {
							color = plains;
						} else if (t.overlayTerrainType.Key == "forest") {
							color = forest;
						} else if (t.overlayTerrainType.Key == "jungle") {
							color = jungle;
						} else if (t.overlayTerrainType.Key == "marsh") {
							color = marsh;
						} else if (t.overlayTerrainType.Key == "ocean") {
							color = oceanBlue;
						} else if (t.overlayTerrainType.Key == "sea") {
							color = seaBlue;
						} else if (t.overlayTerrainType.Key == "coast") {
							color = coastBlue;
						} else {
							color = grass;
						}

						image[t.XCoordinate * 2 + xOffset, t.YCoordinate + yOffset] = color;
						image[t.XCoordinate * 2 + 1 + xOffset, t.YCoordinate + yOffset] = color;

						if (t.XCoordinate > 0) {
							image[t.XCoordinate * 2 - 1 + xOffset, t.YCoordinate + yOffset] = color;
						}
						if (t.XCoordinate + 1 < m.numTilesWide) {
							image[t.XCoordinate * 2 + 2 + xOffset, t.YCoordinate + yOffset] = color;
						}

						if (t.YCoordinate > 0) {
							Bgra32 old = image[t.XCoordinate * 2 + xOffset, t.YCoordinate + yOffset - 1];

							image[t.XCoordinate * 2 + xOffset, t.YCoordinate + yOffset - 1] = Average(old, color);
							image[t.XCoordinate * 2 + 1 + xOffset, t.YCoordinate + yOffset - 1] = Average(old, color);
						}
					}
				}
			}

			image.SaveAsPng(filePath);
		}

		Console.WriteLine($"Noise map saved to: {filePath}");
	}

	private static Bgra32 Average(Bgra32 a, Bgra32 c) {
		int r = (int)Math.Sqrt((Math.Pow(a.R, 2) + Math.Pow(c.R, 2)) / 2);
		int g = (int)Math.Sqrt((Math.Pow(a.G, 2) + Math.Pow(c.G, 2)) / 2);
		int b = (int)Math.Sqrt((Math.Pow(a.B, 2) + Math.Pow(c.B, 2)) / 2);

		return new Bgra32((byte)r, (byte)g, (byte)b);
	}

	[Fact]
	public void GameMapGeneration() {
		int numMapsPerCategory = 1;

		List<TerrainType> terrainTypes = new();
		terrainTypes.Add(new TerrainType() { Key = "grassland" });
		terrainTypes.Add(new TerrainType() { Key = "plains" });
		terrainTypes.Add(new TerrainType() { Key = "desert" });
		terrainTypes.Add(new TerrainType() { Key = "flood plain" });
		terrainTypes.Add(new TerrainType() { Key = "tundra" });
		terrainTypes.Add(new TerrainType() { Key = "coast" });
		terrainTypes.Add(new TerrainType() { Key = "sea" });
		terrainTypes.Add(new TerrainType() { Key = "ocean" });
		terrainTypes.Add(new TerrainType() { Key = "forest" });
		terrainTypes.Add(new TerrainType() { Key = "jungle" });
		terrainTypes.Add(new TerrainType() { Key = "marsh" });
		terrainTypes.Add(new TerrainType() { Key = "hills" });
		terrainTypes.Add(new TerrainType() { Key = "volcano" });
		terrainTypes.Add(new TerrainType() { Key = "mountains" });

		List<List<GameMap>> archipelagoMaps = new();
		archipelagoMaps.Add(new List<GameMap>());
		archipelagoMaps.Add(new List<GameMap>());
		archipelagoMaps.Add(new List<GameMap>());

		WorldCharacteristics.OceanCoverage[] oceans = {
			WorldCharacteristics.OceanCoverage.Percent_60,
			WorldCharacteristics.OceanCoverage.Percent_70,
			WorldCharacteristics.OceanCoverage.Percent_80,
		};

		// Uncommenting the options will display them, but make the test take
		// longer.
		WorldCharacteristics.Age[] ages = {
			// WorldCharacteristics.Age.Billion_3,
			WorldCharacteristics.Age.Billion_4,
			// WorldCharacteristics.Age.Billion_5,
		};
		WorldCharacteristics.Temperature[] temps = {
			// WorldCharacteristics.Temperature.Cool,
			WorldCharacteristics.Temperature.Temperate,
			// WorldCharacteristics.Temperature.Warm,
		};
		WorldCharacteristics.Climate[] climates = {
			// WorldCharacteristics.Climate.Wet,
			WorldCharacteristics.Climate.Normal,
			// WorldCharacteristics.Climate.Arid,
		};

		for (int i = 0; i < oceans.Length; ++i) {
			foreach (var age in ages) {
				foreach (var temp in temps) {
					foreach (var climate in climates) {
						for (int k = 0; k < numMapsPerCategory; ++k) {
							archipelagoMaps[i].Add(MapGenerator.GenerateMap(new WorldCharacteristics() {
								landform = WorldCharacteristics.Landform.Archipelago,
								oceanCoverage = oceans[i],
								age = age,
								climate = climate,
								temperature = temp,
								worldSize = new WorldSize() { width = 100, height = 100 },
								terrainTypes = terrainTypes,
							}));
						}
					}
				}
			}
		}

		SaveMapsAsWaterLandPng(archipelagoMaps, "debug_archipelago_maps.png");

		List<List<GameMap>> continentMaps = new();
		continentMaps.Add(new List<GameMap>());
		continentMaps.Add(new List<GameMap>());
		continentMaps.Add(new List<GameMap>());

		for (int i = 0; i < oceans.Length; ++i) {
			foreach (var age in ages) {
				foreach (var temp in temps) {
					foreach (var climate in climates) {
						for (int k = 0; k < numMapsPerCategory; ++k) {
							continentMaps[i].Add(MapGenerator.GenerateMap(new WorldCharacteristics() {
								landform = WorldCharacteristics.Landform.Continents,
								oceanCoverage = oceans[i],
								age = age,
								climate = climate,
								temperature = temp,
								worldSize = new WorldSize() { width = 100, height = 100 },
								terrainTypes = terrainTypes,
							}));
						}
					}
				}
			}
		}

		SaveMapsAsWaterLandPng(continentMaps, "debug_continent_maps.png");

		List<List<GameMap>> pangaeaMaps = new();
		pangaeaMaps.Add(new List<GameMap>());
		pangaeaMaps.Add(new List<GameMap>());
		pangaeaMaps.Add(new List<GameMap>());

		for (int i = 0; i < oceans.Length; ++i) {
			foreach (var age in ages) {
				foreach (var temp in temps) {
					foreach (var climate in climates) {
						for (int k = 0; k < numMapsPerCategory; ++k) {
							pangaeaMaps[i].Add(MapGenerator.GenerateMap(new WorldCharacteristics() {
								landform = WorldCharacteristics.Landform.Pangaea,
								oceanCoverage = oceans[i],
								age = age,
								climate = climate,
								temperature = temp,
								worldSize = new WorldSize() { width = 100, height = 100 },
								terrainTypes = terrainTypes,
							}));
						}
					}
				}
			}
		}

		SaveMapsAsWaterLandPng(pangaeaMaps, "debug_pangaea_maps.png");
	}
}
