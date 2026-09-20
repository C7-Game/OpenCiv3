using C7Engine;
using C7Engine.Pathing;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI.Pathing;

public sealed class AStarPathFindingLandUnitTest : MapBase {

	[Fact]
	private void TestHumanLandUnitCanEnterUnknownCoastTiles() {
		InitilizeStartTile(MakeHillTile(), new TileLocation(50, 50));
		var north1 = AddNeighborsAndUpdateMap(startTile, MakeCoastTile(), TileDirection.NORTH);
		var north2 = AddNeighborsAndUpdateMap(north1, MakeCoastTile(), TileDirection.NORTH);

		MapUnit unit = MakeLandUnit();

		// make player human, so that they can't see unknown tiles
		unit.owner.isHuman = true;

		AStarAlgorithm aStarAlgorithm = PathingAlgorithmChooser.GetAlgorithm(unit) as AStarAlgorithm;
		TilePath tilePath = aStarAlgorithm.PathFrom(startTile, north2, unit);

		// Human land unit can calculate a path that contains coast tiles because it doesn't know yet
		Assert.NotEmpty(tilePath.path);
		Assert.True(tilePath.path.Count == 2);
		Assert.True(tilePath.path.Contains(north1));
		Assert.True(tilePath.path.Contains(north2));
	}

	[Fact]
	private void TestAILandUnitCannotEnterUnknownCoastTiles() {
		InitilizeStartTile(MakeHillTile(), new TileLocation(50, 50));
		var north1 = AddNeighborsAndUpdateMap(startTile, MakeCoastTile(), TileDirection.NORTH);
		var north2 = AddNeighborsAndUpdateMap(north1, MakeCoastTile(), TileDirection.NORTH);

		MapUnit unit = MakeLandUnit();

		unit.owner.isHuman = false;

		AStarAlgorithm aStarAlgorithm = PathingAlgorithmChooser.GetAlgorithm(unit) as AStarAlgorithm;
		TilePath tilePath = aStarAlgorithm.PathFrom(startTile, north2, unit);

		// AI land unit will not calculate a path that contains coast tiles because it can see them
		// even if they are unexplored yet; i.e. the AI is cheating
		Assert.Empty(tilePath.path);
	}

	[Fact]
	private void TestHumanLandUnitCannotEnterKnownCoastTile() {
		InitilizeStartTile(MakeHillTile(), new TileLocation(50, 50));
		var north1 = AddNeighborsAndUpdateMap(startTile, MakeCoastTile(), TileDirection.NORTH);
		var north2 = AddNeighborsAndUpdateMap(north1, MakeCoastTile(), TileDirection.NORTH);

		MapUnit unit = MakeLandUnit();

		// make player human, so that they can't see unknown tiles
		unit.owner.isHuman = true;
		unit.owner.tileKnowledge.knownTiles.Add(north1);
		unit.owner.tileKnowledge.knownTiles.Add(north2);

		AStarAlgorithm aStarAlgorithm = PathingAlgorithmChooser.GetAlgorithm(unit) as AStarAlgorithm;
		TilePath tilePath = aStarAlgorithm.PathFrom(startTile, north2, unit);

		// Human land unit will ignore known coast tiles
		Assert.Empty(tilePath.path);
	}

	[Fact]
	private void TestLandUnitCanEnterDifferentContinentUnknownTile() {
		InitilizeStartTile(MakeHillTile(), new TileLocation(50, 50));
		startTile.continent = 1;
		var coast = AddNeighborsAndUpdateMap(startTile, MakeCoastTile(), TileDirection.NORTH);
		coast.continent = 0;
		var otherContinentTile = AddNeighborsAndUpdateMap(coast, MakePlainsTile(), TileDirection.NORTH);
		otherContinentTile.continent = 2;

		MapUnit unit = MakeLandUnit();

		AStarAlgorithm aStarAlgorithm = PathingAlgorithmChooser.GetAlgorithm(unit) as AStarAlgorithm;
		TilePath tilePath = aStarAlgorithm.PathFrom(startTile, otherContinentTile, unit);

		// Test 1
		// The AI won't attempt to find a path to another continent's tile
		Assert.Empty(tilePath.path);

		// Test 2
		unit.owner.isHuman = true;

		tilePath = aStarAlgorithm.PathFrom(startTile, otherContinentTile, unit);

		// The Human doesn't know what the tile is, even more so that's in a different continent and it can't ever reach it
		Assert.NotEmpty(tilePath.path);
		Assert.True(tilePath.path.Count == 2);
		Assert.True(tilePath.path.Contains(otherContinentTile));
		Assert.True(tilePath.path.Contains(coast));
	}

	[Fact]
	private void TestLandUnitCannotEnterImpassableTile() {
		InitilizeStartTile(MakeHillTile(), new TileLocation(50, 50));
		var impassableDesert = AddNeighborsAndUpdateMap(startTile, MakeImpassableDesertTile(), TileDirection.NORTH);

		MapUnit unit = MakeLandUnit();

		// The unit must not consider the impassable tile enterable,
		// even forcefully.
		Assert.False(unit.CanEnter(impassableDesert));
		Assert.False(unit.CanEnterForcefully(impassableDesert));

		AStarAlgorithm aStarAlgorithm = PathingAlgorithmChooser.GetAlgorithm(unit) as AStarAlgorithm;
		TilePath tilePath = aStarAlgorithm.PathFrom(startTile, impassableDesert, unit);

		// No path can be found onto an impassable tile.
		Assert.Empty(tilePath.path);
	}

	[Fact]
	private void TestLandUnitCannotPathThroughImpassableTile() {
		InitilizeStartTile(MakeHillTile(), new TileLocation(50, 50));
		var impassableDesert = AddNeighborsAndUpdateMap(startTile, MakeImpassableDesertTile(), TileDirection.NORTH);
		var beyondDesert = AddNeighborsAndUpdateMap(impassableDesert, MakeDesertTile(), TileDirection.NORTH);

		MapUnit unit = MakeLandUnit();

		AStarAlgorithm aStarAlgorithm = PathingAlgorithmChooser.GetAlgorithm(unit) as AStarAlgorithm;
		TilePath tilePath = aStarAlgorithm.PathFrom(startTile, beyondDesert, unit);

		// The path cannot cross the impassable tile to reach the destination.
		Assert.Empty(tilePath.path);
	}

	[Fact]
	private void TestBarbarianCampTileIsEnterableEvenWhenImpassable() {
		InitilizeStartTile(MakeHillTile(), new TileLocation(50, 50));
		var impassableDesert = AddNeighborsAndUpdateMap(startTile, MakeImpassableDesertTile(), TileDirection.NORTH);
		impassableDesert.hasBarbarianCamp = true;

		MapUnit unit = MakeLandUnit();
		unit.unitType.attack = 1;
		unit.location = startTile;
		startTile.unitsOnTile.Add(unit);

		// A camp tile stays enterable even on impassable terrain, so a
		// garrison can leave and return, both peacefully and forcefully.
		Assert.True(unit.CanEnter(impassableDesert));
		Assert.True(unit.CanEnterForcefully(impassableDesert));

		AStarAlgorithm aStarAlgorithm = PathingAlgorithmChooser.GetAlgorithm(unit) as AStarAlgorithm;
		TilePath tilePath = aStarAlgorithm.PathFrom(startTile, impassableDesert, unit);

		Assert.NotEmpty(tilePath.path);
		Assert.True(tilePath.path.Contains(impassableDesert));
	}
}

public sealed class AStarPathFindingWaterUnitTest : MapBase {

	[Fact]
	private void TestWaterUnitCanEnterWaterTile() {
		InitilizeStartTile(MakeCoastTile(), new TileLocation(50, 50));
		var north1 = AddNeighborsAndUpdateMap(startTile, MakeCoastTile(), TileDirection.NORTH);
		var north2 = AddNeighborsAndUpdateMap(north1, MakeCoastTile(), TileDirection.NORTH);

		MapUnit unit = MakeWaterUnit();
		unit.location = startTile;
		startTile.unitsOnTile.Add(unit);

		AStarAlgorithm aStarAlgorithm = PathingAlgorithmChooser.GetAlgorithm(unit) as AStarAlgorithm;
		TilePath tilePath = aStarAlgorithm.PathFrom(startTile, north2, unit);

		// Test 1
		// AI can find the path
		Assert.True(tilePath.path.Count == 2);
		Assert.True(tilePath.path.Contains(north1));
		Assert.True(tilePath.path.Contains(north2));

		// Test 2
		// Human can find the path, while tiles are unknown
		unit.owner.isHuman = true;

		tilePath = aStarAlgorithm.PathFrom(startTile, north2, unit);

		Assert.True(tilePath.path.Count == 2);
		Assert.True(tilePath.path.Contains(north1));
		Assert.True(tilePath.path.Contains(north2));

		// Test 3
		// Human can find the path, while having knowledge of the tiles
		unit.owner.tileKnowledge.knownTiles.Add(north1);
		unit.owner.tileKnowledge.knownTiles.Add(north2);

		ComputeAllNeighbors(unit.owner.tileKnowledge.knownTiles);

		tilePath = aStarAlgorithm.PathFrom(startTile, north2, unit);

		Assert.True(tilePath.path.Count == 2);
		Assert.True(tilePath.path.Contains(north1));
		Assert.True(tilePath.path.Contains(north2));
	}

	[Fact]
	private void TestWaterUnitCanEnterWaterTileFromCity() {
		InitilizeStartTile(MakeHillTile(), new TileLocation(50, 50));
		var north1 = AddNeighborsAndUpdateMap(startTile, MakeCoastTile(), TileDirection.NORTH);
		var north2 = AddNeighborsAndUpdateMap(north1, MakeCoastTile(), TileDirection.NORTH);


		MapUnit unit = MakeWaterUnit();
		startTile.cityAtTile = new City(north1, unit.owner, "Canal City", ID.None(""));
		unit.location = startTile;
		startTile.unitsOnTile.Add(unit);

		AStarAlgorithm aStarAlgorithm = PathingAlgorithmChooser.GetAlgorithm(unit) as AStarAlgorithm;
		TilePath tilePath = aStarAlgorithm.PathFrom(startTile, north2, unit);

		// Test 1
		// AI can find the path
		Assert.True(tilePath.path.Count == 2);
		Assert.True(tilePath.path.Contains(north1));
		Assert.True(tilePath.path.Contains(north2));

		// Test 2
		// Human can find the path, while tiles are unknown
		unit.owner.isHuman = true;

		tilePath = aStarAlgorithm.PathFrom(startTile, north2, unit);

		Assert.True(tilePath.path.Count == 2);
		Assert.True(tilePath.path.Contains(north1));
		Assert.True(tilePath.path.Contains(north2));

		// Test 3
		// Human can find the path, while having knowledge of the tiles
		unit.owner.tileKnowledge.knownTiles.Add(north1);
		unit.owner.tileKnowledge.knownTiles.Add(north2);

		ComputeAllNeighbors(unit.owner.tileKnowledge.knownTiles);

		tilePath = aStarAlgorithm.PathFrom(startTile, north2, unit);

		Assert.True(tilePath.path.Count == 2);
		Assert.True(tilePath.path.Contains(north1));
		Assert.True(tilePath.path.Contains(north2));
	}

	[Fact]
	private void TestHumanWaterUnitCanEnterUnknownLandTilesFromWater() {
		InitilizeStartTile(MakeCoastTile(), new TileLocation(50, 50));
		var north1 = AddNeighborsAndUpdateMap(startTile, MakePlainsTile(), TileDirection.NORTH);
		var north2 = AddNeighborsAndUpdateMap(north1, MakePlainsTile(), TileDirection.NORTH);

		MapUnit unit = MakeWaterUnit();

		// make player human, so that they can't see unknown tiles
		unit.owner.isHuman = true;

		AStarAlgorithm aStarAlgorithm = PathingAlgorithmChooser.GetAlgorithm(unit) as AStarAlgorithm;
		TilePath tilePath = aStarAlgorithm.PathFrom(startTile, north2, unit);

		// Test 1
		// Human water unit can calculate a path that contains land tiles because it doesn't know yet
		Assert.NotEmpty(tilePath.path);
		Assert.True(tilePath.path.Count == 2);
		Assert.True(tilePath.path.Contains(north1));
		Assert.True(tilePath.path.Contains(north2));

		// Test 2
		// AI land unit will not calculate a path from a water tile to a land tile
		// because it can see them even if they are unexplored yet; i.e. the AI is cheating
		unit.owner.isHuman = false;

		tilePath = aStarAlgorithm.PathFrom(startTile, north2, unit);

		Assert.Empty(tilePath.path);
	}

	[Fact]
	private void TestHumanWaterUnitCanEnterUnknownLandTilesFromCity() {
		InitilizeStartTile(MakePlainsTile(), new TileLocation(50, 50));
		var north1 = AddNeighborsAndUpdateMap(startTile, MakePlainsTile(), TileDirection.NORTH);
		var north2 = AddNeighborsAndUpdateMap(north1, MakePlainsTile(), TileDirection.NORTH);

		MapUnit unit = MakeWaterUnit();

		// make player human, so that they can't see unknown tiles
		unit.owner.isHuman = true;
		startTile.cityAtTile = new City(startTile, unit.owner, "Canal City", ID.None(""));

		AStarAlgorithm aStarAlgorithm = PathingAlgorithmChooser.GetAlgorithm(unit) as AStarAlgorithm;
		TilePath tilePath = aStarAlgorithm.PathFrom(startTile, north2, unit);

		// Test 1
		// Human water unit can calculate a path that contains land tiles because it doesn't know yet
		Assert.NotEmpty(tilePath.path);
		Assert.True(tilePath.path.Count == 2);
		Assert.True(tilePath.path.Contains(north1));
		Assert.True(tilePath.path.Contains(north2));

		// Test 2
		// AI land unit will not calculate a path from a water tile to a land tile
		// because it can see them even if they are unexplored yet; i.e. the AI is cheating
		unit.owner.isHuman = false;

		tilePath = aStarAlgorithm.PathFrom(startTile, north2, unit);

		Assert.Empty(tilePath.path);
	}

	[Fact]
	private void TestWaterUnitCanEnterCityTile() {
		InitilizeStartTile(MakeCoastTile(), new TileLocation(50, 50));
		var north1 = AddNeighborsAndUpdateMap(startTile, MakePlainsTile(), TileDirection.NORTH);
		var north2 = AddNeighborsAndUpdateMap(north1, MakeCoastTile(), TileDirection.NORTH);

		MapUnit unit = MakeWaterUnit();
		unit.location = startTile;
		startTile.unitsOnTile.Add(unit);

		north1.cityAtTile = new City(north1, unit.owner, "Canal City", ID.None(""));

		AStarAlgorithm aStarAlgorithm = PathingAlgorithmChooser.GetAlgorithm(unit) as AStarAlgorithm;
		TilePath tilePath = aStarAlgorithm.PathFrom(startTile, north2, unit);

		// Test 1
		// Water unit can enter a land tile that has a city, if they have the same owner
		Assert.NotEmpty(tilePath.path);
		Assert.True(tilePath.path.Count == 2);
		Assert.True(tilePath.path.Contains(north1));
		Assert.True(tilePath.path.Contains(north2));

		// Test 2
		// But cannot enter city with different owner
		Player otherPlayer = new Player();

		north1.cityAtTile = new City(north1, otherPlayer, "Canal City", ID.None(""));

		tilePath = aStarAlgorithm.PathFrom(startTile, north2, unit);

		// Any water unit cannot enter a land tile that has a city if they have different owners
		Assert.Empty(tilePath.path);
	}

	[Fact]
	private void TestWaterUnitCanReachLakeNextToOcean() {
		// "ocean"
		InitilizeStartTile(MakeSeaTile(), new TileLocation(50, 50));
		var coast1 = AddNeighborsAndUpdateMap(startTile, MakeSeaTile(), TileDirection.NORTH);
		// land borders
		var land1 =  AddNeighborsAndUpdateMap(coast1, MakePlainsTile(), TileDirection.NORTHWEST);
		var land2 =  AddNeighborsAndUpdateMap(coast1, MakePlainsTile(), TileDirection.NORTHEAST);
		// "Lake"
		var lake1 = AddNeighborsAndUpdateMap(coast1, MakeLakeTile(), TileDirection.NORTH);
		lake1.isFreshWater = true;
		var lake2 = AddNeighborsAndUpdateMap(lake1, MakeLakeTile(), TileDirection.NORTHWEST);
		var lake3 = AddNeighborsAndUpdateMap(lake1, MakeLakeTile(), TileDirection.NORTHEAST);
		// destination
		var lake4 = AddNeighborsAndUpdateMap(lake1, MakeLakeTile(), TileDirection.NORTH);

		MapUnit unit = MakeWaterUnit();
		unit.location = startTile;
		startTile.unitsOnTile.Add(unit);

		AStarAlgorithm aStarAlgorithm = PathingAlgorithmChooser.GetAlgorithm(unit) as AStarAlgorithm;
		TilePath tilePath = aStarAlgorithm.PathFrom(startTile, lake4, unit);

		// Test 1
		// Fist test the AI
		Assert.Empty(tilePath.path);

		// Test 2
		// Then the human
		unit.owner.isHuman = true;
		unit.owner.tileKnowledge.knownTiles.Add(startTile);
		unit.owner.tileKnowledge.knownTiles.Add(coast1);
		unit.owner.tileKnowledge.knownTiles.Add(land1);
		unit.owner.tileKnowledge.knownTiles.Add(land2);
		unit.owner.tileKnowledge.knownTiles.Add(lake1);
		unit.owner.tileKnowledge.knownTiles.Add(lake2);
		unit.owner.tileKnowledge.knownTiles.Add(lake3);
		unit.owner.tileKnowledge.knownTiles.Add(lake4);

		ComputeAllNeighbors(unit.owner.tileKnowledge.knownTiles);

		tilePath = aStarAlgorithm.PathFrom(startTile, lake4, unit);

		Assert.Empty(tilePath.path);

		//Test 3
		// Go through city
		land1.cityAtTile = new City(land1, unit.owner, "Canal City", ID.None(""));
		tilePath = aStarAlgorithm.PathFrom(startTile, lake4, unit);

		Assert.True(tilePath.path.Count == 4);
		Assert.True(tilePath.path.Contains(coast1));
		Assert.True(tilePath.path.Contains(land1));
		Assert.True(tilePath.path.Contains(lake2));
		Assert.True(tilePath.path.Contains(lake4));
	}

	[Fact]
	private void TestWaterUnitCannotGoThroughLandStrip() {
		InitilizeStartTile(MakeCoastTile(), new TileLocation(50, 50));
		var destination = AddNeighborsAndUpdateMap(startTile, MakeCoastTile(), TileDirection.NORTH);
		var west = AddNeighborsAndUpdateMap(startTile, MakePlainsTile(), TileDirection.NORTHWEST);
		var east = AddNeighborsAndUpdateMap(startTile, MakePlainsTile(), TileDirection.NORTHEAST);

		MapUnit unit = MakeWaterUnit();

		AStarAlgorithm aStarAlgorithm = PathingAlgorithmChooser.GetAlgorithm(unit) as AStarAlgorithm;
		TilePath tilePath = aStarAlgorithm.PathFrom(startTile, destination, unit);

		// Test 1
		// Fist test the AI
		Assert.Empty(tilePath.path);

		// Test 2
		// Then the human
		unit.owner.isHuman = true;

		tilePath = aStarAlgorithm.PathFrom(startTile, destination, unit);

		Assert.Single(tilePath.path);
		Assert.True(tilePath.path.Contains(destination));
	}

	[Fact]
	private void TestWaterUnitHasToGoAroundLandStrip() {
		EngineStorage.InitializeGameDataForTests(new C7GameData.GameData(1324));
		// Tile location here doesn't have a special meaning, I just had a map example to work off of
		InitilizeStartTile(MakeCoastTile(), new TileLocation(88, 12));
		var east = AddNeighborsAndUpdateMap(startTile, MakeCoastTile(), TileDirection.EAST);
		var destination = AddNeighborsAndUpdateMap(east, MakeCoastTile(), TileDirection.EAST);

		var land1 = AddNeighborsAndUpdateMap(east, MakePlainsTile(), TileDirection.SOUTHEAST);
		var land2 = AddNeighborsAndUpdateMap(east, MakePlainsTile(), TileDirection.NORTHEAST);

		var north = AddNeighborsAndUpdateMap(east, MakeCoastTile(), TileDirection.NORTH);
		var northEast = AddNeighborsAndUpdateMap(north, MakeCoastTile(), TileDirection.NORTHEAST);
		var southEast = AddNeighborsAndUpdateMap(northEast, MakeCoastTile(), TileDirection.SOUTHEAST);


		MapUnit unit = MakeWaterUnit();
		unit.owner.isHuman = true;
		unit.location = startTile;
		startTile.unitsOnTile.Add(unit);

		// Test 1
		// First we will try to path to the destination blind as a human
		unit.owner.tileKnowledge.knownTiles.Add(startTile);

		ComputeAllNeighbors(unit.owner.tileKnowledge.knownTiles);

		AStarAlgorithm aStarAlgorithm = PathingAlgorithmChooser.GetAlgorithm(unit) as AStarAlgorithm;
		TilePath tilePath = aStarAlgorithm.PathFrom(startTile, destination, unit);

		Assert.True(tilePath.path.Count == 2);
		Assert.True(tilePath.path.Contains(east));
		Assert.True(tilePath.path.Contains(destination));

		// Test 2
		// Update human player knowledge and recompute for the actual path now that we have "seen" the tiles
		unit.owner.tileKnowledge.knownTiles.Add(east);
		unit.owner.tileKnowledge.knownTiles.Add(destination);
		unit.owner.tileKnowledge.knownTiles.Add(land1);
		unit.owner.tileKnowledge.knownTiles.Add(land2);
		unit.owner.tileKnowledge.knownTiles.Add(north);
		unit.owner.tileKnowledge.knownTiles.Add(northEast);
		unit.owner.tileKnowledge.knownTiles.Add(southEast);

		ComputeAllNeighbors(unit.owner.tileKnowledge.knownTiles);

		tilePath = aStarAlgorithm.PathFrom(startTile, destination, unit);

		// path should be
		Assert.True(tilePath.path.Count == 4);
		Assert.True(tilePath.path.Contains(east));
		Assert.True(tilePath.path.Contains(north));
		Assert.True(tilePath.path.Contains(southEast));
		Assert.True(tilePath.path.Contains(destination));

		// Test 3
		// Now add a city tile in one of the land tiles and unit should go through that
		land1.cityAtTile = new City(land1, unit.owner, "Canal City", ID.None(""));
		tilePath = aStarAlgorithm.PathFrom(startTile, destination, unit);

		Assert.True(tilePath.path.Count == 3);
		Assert.True(tilePath.path.Contains(east));
		Assert.True(tilePath.path.Contains(land1));
		Assert.True(tilePath.path.Contains(destination));

		// Test 4
		// Make the city another player's city, should ignore it
		land1.cityAtTile = new City(land1, new Player(), "Canal City", ID.None(""));
		tilePath = aStarAlgorithm.PathFrom(startTile, destination, unit);

		Assert.True(tilePath.path.Count == 4);
		Assert.False(tilePath.path.Contains(land1));
		Assert.True(tilePath.path.Contains(east));
		Assert.True(tilePath.path.Contains(north));
		Assert.True(tilePath.path.Contains(southEast));
		Assert.True(tilePath.path.Contains(destination));

		// Test 5
		// Then remove the tile knowledge & city and try as AI and should get the same results
		unit.owner.isHuman = false;
		land1.cityAtTile = null;
		foreach (Tile tile in unit.owner.tileKnowledge.knownTiles)
			unit.owner.tileKnowledge.knownTiles.Remove(tile);

		ComputeAllNeighbors(unit.owner.tileKnowledge.knownTiles);

		tilePath = aStarAlgorithm.PathFrom(startTile, destination, unit);

		Assert.True(tilePath.path.Count == 4);
		Assert.True(tilePath.path.Contains(east));
		Assert.True(tilePath.path.Contains(north));
		Assert.True(tilePath.path.Contains(southEast));
		Assert.True(tilePath.path.Contains(destination));
	}
}
