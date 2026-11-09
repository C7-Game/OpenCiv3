using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class RiverCrossingTest : MapBase {
	private static readonly TileDirection[] Cardinals = {
		TileDirection.NORTH, TileDirection.EAST, TileDirection.SOUTH, TileDirection.WEST,
	};
	private static readonly TileDirection[] Diagonals = {
		TileDirection.NORTHEAST, TileDirection.SOUTHEAST, TileDirection.SOUTHWEST, TileDirection.NORTHWEST,
	};

	public static IEnumerable<object[]> CardinalData() => Cardinals.Select(d => new object[] { d });
	public static IEnumerable<object[]> DiagonalData() => Diagonals.Select(d => new object[] { d });
	public static IEnumerable<object[]> AllDirectionData() => Cardinals.Concat(Diagonals).Select(d => new object[] { d });

	// The four river edges meeting at the corner passed through by a cardinal
	// move, split by which side of the path they lie on. `fromTile` is true when
	// the edge flag is stored on the origin tile, false for the destination tile.
	private record Edge(bool fromTile, TileDirection flag);

	private static (Edge[] sideA, Edge[] sideB) CornerEdges(TileDirection dir) {
		return dir switch {
			TileDirection.NORTH => (
				new[] { new Edge(true, TileDirection.NORTHWEST), new Edge(false, TileDirection.SOUTHWEST) },
				new[] { new Edge(true, TileDirection.NORTHEAST), new Edge(false, TileDirection.SOUTHEAST) }),
			TileDirection.SOUTH => (
				new[] { new Edge(true, TileDirection.SOUTHWEST), new Edge(false, TileDirection.NORTHWEST) },
				new[] { new Edge(true, TileDirection.SOUTHEAST), new Edge(false, TileDirection.NORTHEAST) }),
			TileDirection.EAST => (
				new[] { new Edge(true, TileDirection.NORTHEAST), new Edge(false, TileDirection.NORTHWEST) },
				new[] { new Edge(true, TileDirection.SOUTHEAST), new Edge(false, TileDirection.SOUTHWEST) }),
			TileDirection.WEST => (
				new[] { new Edge(true, TileDirection.NORTHWEST), new Edge(false, TileDirection.NORTHEAST) },
				new[] { new Edge(true, TileDirection.SOUTHWEST), new Edge(false, TileDirection.SOUTHEAST) }),
			_ => throw new System.ArgumentException("Not a cardinal direction"),
		};
	}

	private static void SetRiver(Tile tile, TileDirection edge, bool value) {
		switch (edge) {
			case TileDirection.NORTHEAST: tile.riverNortheast = value; break;
			case TileDirection.SOUTHEAST: tile.riverSoutheast = value; break;
			case TileDirection.SOUTHWEST: tile.riverSouthwest = value; break;
			case TileDirection.NORTHWEST: tile.riverNorthwest = value; break;
			default: throw new System.ArgumentException("Rivers are only stored on diagonal edges");
		}
	}

	private static void SetEdge(Tile from, Tile to, Edge edge, bool value) {
		SetRiver(edge.fromTile ? from : to, edge.flag, value);
	}

	// Makes two tiles that are neighbors in both directions.
	private (Tile from, Tile to) MakeNeighbors(TileDirection dir, System.Func<Tile> makeTo = null) {
		Tile from = MakePlainsTile();
		Tile to = (makeTo ?? MakePlainsTile)();
		InitilizeStartTile(from, new TileLocation(50, 50));
		AddNeighborsAndUpdateMap(from, to, dir);
		AddNeighborsAndUpdateMap(to, from, dir.Reversed());
		return (from, to);
	}

	// Adds river edges so that moving `dir` from `from` to `to` crosses a river.
	private static void AddCrossingRiver(Tile from, Tile to, TileDirection dir) {
		if (Diagonals.Contains(dir)) {
			SetRiver(from, dir, true);
			SetRiver(to, dir.Reversed(), true);
		} else {
			(Edge[] sideA, Edge[] sideB) = CornerEdges(dir);
			SetEdge(from, to, sideA[0], true);
			SetEdge(from, to, sideB[0], true);
		}
	}

	[Theory]
	[MemberData(nameof(DiagonalData))]
	public void DiagonalMoveCrossesRiverOnlyWhenThatEdgeHasOne(TileDirection dir) {
		(Tile from, Tile to) = MakeNeighbors(dir);
		Assert.False(from.HasRiverCrossing(dir));

		// A river on one of the other edges of the tile doesn't matter.
		foreach (TileDirection other in Diagonals.Where(d => d != dir)) {
			SetRiver(from, other, true);
		}
		Assert.False(from.HasRiverCrossing(dir));

		SetRiver(from, dir, true);
		Assert.True(from.HasRiverCrossing(dir));
	}

	[Theory]
	[MemberData(nameof(CardinalData))]
	public void CardinalMoveCrossesRiverWithEdgesOnBothSides(TileDirection dir) {
		(Edge[] sideA, Edge[] sideB) = CornerEdges(dir);

		// Every pairing of one edge on each side crosses, including the case
		// where both edges belong to the destination tile.
		foreach (Edge a in sideA) {
			foreach (Edge b in sideB) {
				(Tile from, Tile to) = MakeNeighbors(dir);
				SetEdge(from, to, a, true);
				SetEdge(from, to, b, true);
				Assert.True(from.HasRiverCrossing(dir), $"{dir}: {a} + {b}");
			}
		}
	}

	[Theory]
	[MemberData(nameof(CardinalData))]
	public void CardinalMoveDoesNotCrossRiverWithEdgesOnOneSideOnly(TileDirection dir) {
		(Edge[] sideA, Edge[] sideB) = CornerEdges(dir);

		foreach (Edge[] side in new[] { sideA, sideB }) {
			// A single edge, such as a river ending at the corner.
			foreach (Edge edge in side) {
				(Tile from, Tile to) = MakeNeighbors(dir);
				SetEdge(from, to, edge, true);
				Assert.False(from.HasRiverCrossing(dir), $"{dir}: only {edge}");
			}

			// Both edges on the same side: a river running alongside the path.
			(Tile from2, Tile to2) = MakeNeighbors(dir);
			foreach (Edge edge in side) {
				SetEdge(from2, to2, edge, true);
			}
			Assert.False(from2.HasRiverCrossing(dir), $"{dir}: both edges of one side");
		}

		(Tile fromNone, _) = MakeNeighbors(dir);
		Assert.False(fromNone.HasRiverCrossing(dir));
	}

	[Theory]
	[MemberData(nameof(CardinalData))]
	public void CardinalMoveIgnoresStoredCardinalRiverFlags(TileDirection dir) {
		// The cardinal fields are imported river-point data, not crossings.
		(Tile from, Tile to) = MakeNeighbors(dir);
		from.riverNorth = from.riverEast = from.riverSouth = from.riverWest = true;
		to.riverNorth = to.riverEast = to.riverSouth = to.riverWest = true;
		Assert.False(from.HasRiverCrossing(dir));
	}

	[Theory]
	[MemberData(nameof(CardinalData))]
	public void CardinalCrossingIsTheSameInBothDirections(TileDirection dir) {
		(Edge[] sideA, Edge[] sideB) = CornerEdges(dir);
		Edge[] edges = sideA.Concat(sideB).ToArray();

		// Try every combination of the four edges at the corner.
		for (int mask = 0; mask < (1 << edges.Length); mask++) {
			(Tile from, Tile to) = MakeNeighbors(dir);
			for (int i = 0; i < edges.Length; i++) {
				SetEdge(from, to, edges[i], (mask & (1 << i)) != 0);
			}
			Assert.Equal(from.HasRiverCrossing(dir), to.HasRiverCrossing(dir.Reversed()));
		}
	}

	[Theory]
	[MemberData(nameof(AllDirectionData))]
	public void RiverCrossingRemovesRoadBenefitWithoutBridgeBuilding(TileDirection dir) {
		C7GameData.GameData gameData = new(1234);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player player = MakePlayer();

		// The terrain costs 2 (hills) and the road 1/3, so we can tell which applies.
		(Tile from, Tile to) = MakeNeighbors(dir, MakeHillTile);
		from.overlays.Add(road);
		to.overlays.Add(road);
		Assert.Equal(1f / 3, TilePath.GetMovementCost(player, from, dir, to), 3);

		AddCrossingRiver(from, to, dir);
		Assert.Equal(2f, TilePath.GetMovementCost(player, from, dir, to));
	}

	[Theory]
	[MemberData(nameof(AllDirectionData))]
	public void RoadsBridgeRiversOnceBridgeBuildingIsKnown(TileDirection dir) {
		C7GameData.GameData gameData = new(1234);
		Tech bridgeTech = new() { id = ID.None("bridge-building"), EnablesBridges = true };
		gameData.techs.Add(bridgeTech);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player player = MakePlayer();
		player.knownTechs.Add(bridgeTech.id);
		Assert.True(player.CanBridgeRoads());

		(Tile from, Tile to) = MakeNeighbors(dir, MakeHillTile);
		from.overlays.Add(road);
		to.overlays.Add(road);
		AddCrossingRiver(from, to, dir);

		Assert.Equal(1f / 3, TilePath.GetMovementCost(player, from, dir, to), 3);
	}

	private static readonly StrengthBonus RiverBonus = new("River crossing", 0.25);

	private static List<StrengthBonus> DefenseBonuses(Tile defenderTile, TileDirection attackDirection) {
		MapUnit defender = MakeLandUnit();
		defender.location = defenderTile;
		return defender.ListStrengthBonusesVersus(MakeLandUnit(), CombatRole.Defense, attackDirection).ToList();
	}

	private void InitializeRiverBonus() {
		C7GameData.GameData gameData = new(1234) { riverCrossingBonus = RiverBonus };
		EngineStorage.InitializeGameDataForTests(gameData);
	}

	[Theory]
	[MemberData(nameof(AllDirectionData))]
	public void DefenderGetsRiverBonusWhenAttackedAcrossRiver(TileDirection attackDirection) {
		InitializeRiverBonus();

		// The attacker comes from the opposite side of the defender.
		(Tile defender, Tile attacker) = MakeNeighbors(attackDirection.Reversed());
		Assert.DoesNotContain(RiverBonus, DefenseBonuses(defender, attackDirection));

		AddCrossingRiver(defender, attacker, attackDirection.Reversed());
		Assert.Contains(RiverBonus, DefenseBonuses(defender, attackDirection));
	}
}
