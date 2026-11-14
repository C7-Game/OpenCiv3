using System;
using C7GameData;
using Xunit;

// Characterization tests for TileRandom: they freeze the current hash behaviour so that
// the same world seed keeps producing the same visuals. If TileRandom is changed on purpose,
// regenerate the pinned values and call it out in the PR (it changes every existing map's look).
//
// Expected values were computed by an independent reimplementation of the current algorithm.
public class TileRandomTests {
	private static ID MakeId(int n) => new ID($"test-{n}", n);

	private static Tile MakeMountainsTile(int n, bool hilly) => new(MakeId(n)) {
		overlayTerrainType = new TerrainType { Key = hilly ? "mountains" : "plains" },
	};

	// seed, id.GetN, GetRandomInt(max 10), GetRandomInt(max 3, offset 7), GetRandomInt(max 1000, offset -1)
	[Theory]
	[InlineData(0, 0, 0, 1, 378)]
	[InlineData(0, 1, 8, 0, 600)]
	[InlineData(1, 0, 8, 0, 600)]
	[InlineData(12345, 100, 0, 2, 167)]
	[InlineData(-1, 1, 3, 2, 42)]
	public void GetRandomInt_MatchesPinnedValues(int seed, int n, int max10, int max3Off7, int max1000OffNeg1) {
		ID id = MakeId(n);
		Assert.Equal(max10, TileRandom.GetRandomInt(id, seed, 10));
		Assert.Equal(max3Off7, TileRandom.GetRandomInt(id, seed, 3, 7));
		Assert.Equal(max1000OffNeg1, TileRandom.GetRandomInt(id, seed, 1000, -1));
	}

	[Fact]
	public void GetRandomInt_StaysInRangeAndIsStateless() {
		for (int n = 0; n < 200; n++) {
			ID id = MakeId(n);
			int v = TileRandom.GetRandomInt(id, 12345, 7, n % 5);
			Assert.InRange(v, 0, 6);
			Assert.Equal(v, TileRandom.GetRandomInt(id, 12345, 7, n % 5));
		}
	}

	// seed, t1.GetN, t2.GetN, expected at default probability (0.7), expected at 0.3
	[Theory]
	[InlineData(0, 0, 1, false, false)]
	[InlineData(0, 1, 2, true, false)]
	[InlineData(12345, 5, 100, true, true)]
	[InlineData(-1, 5, 100, true, false)]
	[InlineData(1, 0, 65535, true, true)]
	public void HasConnection_MatchesPinnedValues(int seed, int n1, int n2, bool atDefault, bool at30) {
		ID a = MakeId(n1), b = MakeId(n2);
		Assert.Equal(atDefault, TileRandom.HasConnection(a, b, seed));
		Assert.Equal(at30, TileRandom.HasConnection(a, b, seed, 0.3f));
	}

	[Fact]
	public void HasConnection_IsSymmetric() {
		foreach (int seed in new[] { 0, 1, 12345, -1 }) {
			for (int i = 0; i < 50; i++) {
				ID a = MakeId(i), b = MakeId(i * 7 + 3);
				Assert.Equal(TileRandom.HasConnection(a, b, seed), TileRandom.HasConnection(b, a, seed));
			}
		}
	}

	[Fact]
	public void HasVirtualHillConnection_RequiresNeighborAndBothHilly() {
		// Pick a seed where HasConnection is true, so only the guards can make the result false.
		Tile a = MakeMountainsTile(1, hilly: true), b = MakeMountainsTile(2, hilly: true);
		int seed = 0;
		while (!TileRandom.HasConnection(a.Id, b.Id, seed)) seed++;

		// No neighbor in that direction
		Assert.False(TileRandom.HasVirtualHillConnection(a, TileDirection.SOUTHEAST, seed));

		// Neighbor present, both hilly: delegates to HasConnection
		a.neighbors[TileDirection.SOUTHEAST] = b;
		Assert.True(TileRandom.HasVirtualHillConnection(a, TileDirection.SOUTHEAST, seed));

		// Either tile not hilly
		Tile flat = MakeMountainsTile(2, hilly: false);
		a.neighbors[TileDirection.SOUTHEAST] = flat;
		Assert.False(TileRandom.HasVirtualHillConnection(a, TileDirection.SOUTHEAST, seed));
		flat.neighbors[TileDirection.NORTHWEST] = a;
		Assert.False(TileRandom.HasVirtualHillConnection(flat, TileDirection.NORTHWEST, seed));
	}
}
