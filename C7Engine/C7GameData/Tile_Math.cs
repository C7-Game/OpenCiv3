using System;
using System.Collections.Generic;
using C7GameData.Save;

namespace C7GameData;

public partial class Tile {
	public TileDirection DirectionTo(Tile other) {
		if ((this == NONE) || (other == NONE))
			throw new Exception("Can't get direction toward NONE Tile since it doesn't have a meaningful location");

		// We have to use the map helper functions to handle edge wrapping
		// correctly.
		//
		// y calculation is reversed so dy is in typical Cartesian coords
		// instead of tile coords, where y is inverted
		int dx = map.CalculateXDelta(other.XCoordinate, this.XCoordinate);
		int dy = map.CalculateYDelta(this.YCoordinate, other.YCoordinate);
		double angle = Math.Atan2(dy, dx); // angle is in interval [-pi, pi]

		return angle switch {
			> 7.0 / 8.0 * Math.PI => TileDirection.WEST,
			> 5.0 / 8.0 * Math.PI => TileDirection.NORTHWEST,
			> 3.0 / 8.0 * Math.PI => TileDirection.NORTH,
			> 1.0 / 8.0 * Math.PI => TileDirection.NORTHEAST,
			> -1.0 / 8.0 * Math.PI => TileDirection.EAST,
			> -3.0 / 8.0 * Math.PI => TileDirection.SOUTHEAST,
			> -5.0 / 8.0 * Math.PI => TileDirection.SOUTH,
			> -7.0 / 8.0 * Math.PI => TileDirection.SOUTHWEST,
			_ => TileDirection.WEST
		};
	}

	/**
     * Distance as the raven flies to another tile.
     * This is a rough metric only.
    */
	public int DistanceTo(Tile other) {
		if (this == NONE || other == NONE) {
			// We can't path to tiles that don't exist.
			return int.MaxValue;
		}
		return (Math.Abs(map.CalculateXDelta(other.XCoordinate, this.XCoordinate)) + Math.Abs(map.CalculateYDelta(other.YCoordinate, this.YCoordinate))) / 2;
	}

	// Returns the number of "ranks" to another tile, where each rank is a
	// border expansion due to culture. So rank 1 is immediate neighbors,
	// rank 2 is the "big fat cross", etc.
	public int RankDistanceTo(Tile other) {
		// Get the x and y deltas in the standard grid coordinates.
		int dx = Math.Abs(map.CalculateXDelta(other.XCoordinate, this.XCoordinate));
		int dy = Math.Abs(map.CalculateYDelta(other.YCoordinate, this.YCoordinate));

		// Transform that to the rank distance using the formula from
		// https://forums.civfanatics.com/threads/everything-about-corruption-c3c-edition.76619/post-1551201
		return (dx + dy) / 2 + Math.Abs(dx - dy) / 4;
	}

	// Returns the tile at a "neighbor index", where 0 is this tile, 1 is
	// due north, 2 is NE, 3 is E, and so on in a clockwise spiral.
	// Index 9 is N+N, 10 is N+NE, etc.
	//
	// This is slightly different than the civ3 spiral, which starts with
	// the NE and goes clockwise. Ring 2 of the civ3 spiral is the BFC tiles,
	// but rings beyond that get stranger. We don't need to match the civ3
	// spiral exactly, and this is much simpler to understand.
	public Tile GetTileAtNeighborIndex(int neighborIndex) {
		// Special case: Index 0 is this tile.
		if (neighborIndex <= 0) {
			return this;
		}

		int xDelta = 0;
		int yDelta = 0;

		// Figure out which ring we're in.
		int ringNumber = 0;
		do {
			ringNumber++;
		} while (Math.Pow(2 * ringNumber + 1, 2) <= neighborIndex);

		// Figure out how many tiles are in the previous ring.
		// For ring 2, we get (2*2 - 1)^2, which is 9.
		int cellsInInnerRings = (ringNumber * 2 - 1) * (ringNumber * 2 - 1);

		// Figure out the index of this neighbor within our ring.
		int indexInRing1Based = neighborIndex - cellsInInnerRings;

		// Our ring is a square with 4 sides, and each side has
		// (ringNumber*2 + 1) tiles in it. But then we have the overlap of
		// each corner, so excluding the overlap we have ringNumber*2 tiles
		// per side.
		//
		// For ring 1, the 4 sections of size 2 are
		//    (N, NE), (E, SE), (S, SW), (W, NW)
		int cellsPerSquareEdge = ringNumber * 2;

		// Define segment boundaries based on 1-based index within the ring
		int segment1End = cellsPerSquareEdge;
		int segment2End = 2 * cellsPerSquareEdge;
		int segment3End = 3 * cellsPerSquareEdge;
		int segment4End = 4 * cellsPerSquareEdge;

		if (indexInRing1Based <= segment1End) {
			// This is the side that goes from N to 1 short of E.
			// N and NE for ring 1.
			xDelta = indexInRing1Based;
			yDelta = indexInRing1Based - cellsPerSquareEdge;
		} else if (indexInRing1Based <= segment2End) {
			// This is the side that goes from E to 1 short of S.
			// E and SE for ring 1.
			xDelta = segment2End - indexInRing1Based;
			yDelta = indexInRing1Based - cellsPerSquareEdge;
		} else if (indexInRing1Based <= segment3End) {
			// This is the side that goes from S to 1 short of W.
			// S and SW for ring 1.
			xDelta = segment2End - indexInRing1Based;
			yDelta = segment3End - indexInRing1Based;
		} else {
			// This is the side that goes from W to 1 short of N.
			// W and NW for ring 1.
			xDelta = indexInRing1Based - segment4End;
			yDelta = segment3End - indexInRing1Based;
		}

		return map.tileAt(XCoordinate + xDelta, YCoordinate + yDelta);
	}

	/// <summary>
	/// <para>
	/// Walks clockwise/counter-clockwise the nth ring around
	/// the specified tile starting on the northmost tile
	/// and tries to find the first tile that matches our boolean criterion.
	/// </para>
	/// <para>
	/// This differs from <see cref="GetTilesWithinRankDistance"/>,
	/// because it includes all the tiles regardless of the distance.
	/// An example would be that GetTilesWithinRankDistance() with a rank of 2
	/// will not return a NN, SS, WW, or EE tile, whereas this method will.
	/// </para>
	/// <para>
	/// It is mostly used to calculate to whom we should assign tiles
	/// that are being claimed by more than 1 city or civilization.
	/// </para>
	/// </summary>
	/// <param name="rank"></param>
	/// <param name="predicate"></param>
	/// <param name="clockwise"></param>
	/// <returns></returns>
	public Tile FindInRing(int rank, Func<Tile, bool> predicate, bool clockwise = true) {
		int x = this.XCoordinate;
		int y = this.YCoordinate - (2 * rank);

		Tile currentTile = map.tileAt(x, y);
		if (currentTile != NONE && predicate(currentTile)) return currentTile;

		// Going SW(counter-clockwise) or SE(clockwise)
		for (int _ = 1; _ < (2 * rank) + 1; _++) {
			if (clockwise) { x++; y++; } else { x--; y++; }
			currentTile = map.tileAt(x, y);
			if (currentTile == NONE || !predicate(currentTile)) continue;
			return currentTile;
		}
		// Going SE(counter-clockwise) or SW(clockwise)
		for (int _ = 1; _ < (2 * rank) + 1; _++) {
			if (clockwise) { x--; y++; } else { x++; y++; }
			currentTile = map.tileAt(x, y);
			if (currentTile == NONE || !predicate(currentTile)) continue;
			return currentTile;
		}
		// Going NE(counter-clockwise) or NW(clockwise)
		for (int _ = 1; _ < (2 * rank) + 1; _++) {
			if (clockwise) { x--; y--; } else { x++; y--; }
			currentTile = map.tileAt(x, y);
			if (currentTile == NONE || !predicate(currentTile)) continue;
			return currentTile;
		}
		// Going NW(counter-clockwise) or NE(clockwise)
		for (int _ = 1; _ < (2 * rank); _++) {
			if (clockwise) { x++; y--; } else { x--; y--; }
			currentTile = map.tileAt(x, y);
			if (currentTile == NONE || !predicate(currentTile)) continue;
			return currentTile;
		}
		return null;
	}

	// Returns the tiles in the spiral ordering defined by
	// GetTileAtNeighborIndex(i).
	public List<Tile> GetTilesWithinRankDistance(int rank) {
		List<Tile> result = new();
		for (int i = 0; i < (rank * 2 + 1) * (rank * 2 + 1); ++i) {
			Tile t = GetTileAtNeighborIndex(i);
			if (RankDistanceTo(t) <= rank) {
				result.Add(t);
			}
		}

		return result;
	}

	// Same as GetTilesWithinRankDistance, but includes "corner tiles",
	// i.e., returns perfect tile squares.
	public List<Tile> GetTilesWithinTileSquare(int rank) {
		List<Tile> result = new();
		for (int i = 0; i < (rank * 2 + 1) * (rank * 2 + 1); ++i) {
			Tile t = GetTileAtNeighborIndex(i);
			result.Add(t);
		}
		return result;
	}

	// Returns the X and Y coordinates of the neighbor in the specified direction.
	public static TileLocation NeighborCoordinate(TileLocation location, TileDirection direction) {
		switch (direction) {
			case TileDirection.NORTH:
				location.Y -= 2;
				break;
			case TileDirection.NORTHEAST:
				location.Y--;
				location.X++;
				break;
			case TileDirection.EAST:
				location.X += 2;
				break;
			case TileDirection.SOUTHEAST:
				location.Y++;
				location.X++;
				break;
			case TileDirection.SOUTH:
				location.Y += 2;
				break;
			case TileDirection.SOUTHWEST:
				location.Y++;
				location.X--;
				break;
			case TileDirection.WEST:
				location.X -= 2;
				break;
			case TileDirection.NORTHWEST:
				location.X--;
				location.Y--;
				break;
		}
		return location;
	}
}

public enum TileDirection {
	NORTH,
	NORTHEAST,
	EAST,
	SOUTHEAST,
	SOUTH,
	SOUTHWEST,
	WEST,
	NORTHWEST,
	INVALID,
}

public static class TileDirectionExtensions {
	// The 8 real directions, e.g. for iterating neighbors. Do NOT iterate
	// Enum.GetValues(typeof(TileDirection)) here: it also returns INVALID.
	public static readonly TileDirection[] All = new TileDirection[] {
		TileDirection.NORTH,
		TileDirection.NORTHEAST,
		TileDirection.EAST,
		TileDirection.SOUTHEAST,
		TileDirection.SOUTH,
		TileDirection.SOUTHWEST,
		TileDirection.WEST,
		TileDirection.NORTHWEST,
	};

	public static TileDirection Reversed(this TileDirection dir) {
		switch (dir) {
			case TileDirection.NORTH: return TileDirection.SOUTH;
			case TileDirection.NORTHEAST: return TileDirection.SOUTHWEST;
			case TileDirection.EAST: return TileDirection.WEST;
			case TileDirection.SOUTHEAST: return TileDirection.NORTHWEST;
			case TileDirection.SOUTH: return TileDirection.NORTH;
			case TileDirection.SOUTHWEST: return TileDirection.NORTHEAST;
			case TileDirection.WEST: return TileDirection.EAST;
			case TileDirection.NORTHWEST: return TileDirection.SOUTHEAST;
			default: return TileDirection.INVALID;
		}
	}

	public static TileDirection RotatedCounterClockwise90Degrees(this TileDirection dir) {
		switch (dir) {
			case TileDirection.NORTH: return TileDirection.WEST;
			case TileDirection.NORTHEAST: return TileDirection.NORTHWEST;
			case TileDirection.EAST: return TileDirection.NORTH;
			case TileDirection.SOUTHEAST: return TileDirection.NORTHEAST;
			case TileDirection.SOUTH: return TileDirection.EAST;
			case TileDirection.SOUTHWEST: return TileDirection.SOUTHEAST;
			case TileDirection.WEST: return TileDirection.SOUTH;
			case TileDirection.NORTHWEST: return TileDirection.SOUTHWEST;
			default: return TileDirection.INVALID;
		}
	}

	public static (int, int) ToCoordDiff(this TileDirection dir) {
		switch (dir) {
			case TileDirection.NORTH: return (0, -2);
			case TileDirection.NORTHEAST: return (1, -1);
			case TileDirection.EAST: return (2, 0);
			case TileDirection.SOUTHEAST: return (1, 1);
			case TileDirection.SOUTH: return (0, 2);
			case TileDirection.SOUTHWEST: return (-1, 1);
			case TileDirection.WEST: return (-2, 0);
			case TileDirection.NORTHWEST: return (-1, -1);
			default: return (0, 0);
		}
	}
}

public static class TileRandom {
	// These methods basically use the MurmurHash as a way of producing a random value
	// that also persists across game seeds and items' IDs
	// https://en.wikipedia.org/wiki/MurmurHash
	// https://gist.github.com/zeux/25b490b07b4873efc08bd37c843777a4

	// The max is exclusive
	public static int GetRandomInt(ID id, int worldSeed, int max, int offset = 0) {
		unchecked {
			uint hash = (uint)worldSeed;

			hash = (hash ^ (uint)id.GetN) * 0x85ebca6b;
			hash = (hash ^ (uint)offset) * 0x9e3779b9;

			hash ^= hash >> 16;
			hash *= 0x85ebca6b;
			hash ^= hash >> 13;
			hash *= 0xc2b2ae35;
			hash ^= hash >> 16;

			return (int)(hash % (uint)max);
		}
	}

	// These takes two edge neighboring tiles (NW->SE) that both could be hilly,
	// and if you either the NW of tile A or the SE of tile B, 
	// you will get the same result every time.
	// This way we don't have to allocate memory to save and update every tile's neighbor connections.
	// Obviously this could be used for anything else
	// that wants to check the relationship (true/false)
	// between A and B with a custom probability threshold,
	// without one having to update the other.
	public static bool HasConnection(ID t1, ID t2, int worldSeed, float probability = 0.7f) {
		int min = Math.Min(t1.GetN, t2.GetN);
		int max = Math.Max(t1.GetN, t2.GetN);

		unchecked {
			uint hash = (uint)worldSeed;

			hash = (hash ^ (uint)min) * 0x85ebca6b;
			hash = (hash ^ (uint)max) * 0xc2b2ae35;
			hash = (hash ^ 0x879) * 0x9e3779b9;

			hash ^= hash >> 16;
			hash *= 0x85ebca6b;
			hash ^= hash >> 13;
			hash *= 0xc2b2ae35;
			hash ^= hash >> 16;

			return hash / (double)uint.MaxValue < probability;
		}
	}

	// Affects only the graphics, used to randomize if 2 tiles are connected as hills/mountain/volcanoes
	public static bool HasVirtualHillConnection(Tile tile, TileDirection direction, int worldSeed) {
		if (!tile.neighbors.TryGetValue(direction, out Tile neighbor))
			return false;

		if (!tile.overlayTerrainType.isHilly() || !neighbor.overlayTerrainType.isHilly())
			return false;

		return HasConnection(tile.Id, neighbor.Id, worldSeed);
	}
}
