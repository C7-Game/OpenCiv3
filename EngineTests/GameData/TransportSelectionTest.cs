using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using Xunit;

namespace EngineTests.GameData;

public class TransportSelectionTest : IDisposable {

	private readonly C7GameData.GameData gameData = new();
	private readonly Player owner = new() { id = ID.FromString("player-2"), civilization = new Civilization("Rome") };
	private readonly Tile tile = new(ID.FromString("tile-1"));
	private int nextId = 1;

	public TransportSelectionTest() {
		// Engine message queue and storage are static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.messagesToUI.Clear();
		EngineStorage.InitializeGameDataForTests(gameData);
	}

	public void Dispose() {
		// Don't leave messages behind for tests that drain the queue and don't expect them
		EngineStorage.messagesToUI.Clear();
	}

	private MapUnit AddUnit(UnitPrototype type, Player unitOwner = null) {
		MapUnit unit = new(ID.FromString($"unit-{nextId++}")) {
			unitType = type,
			owner = unitOwner ?? owner,
			location = tile,
		};
		tile.unitsOnTile.Add(unit);
		gameData.mapUnits.Add(unit);
		return unit;
	}

	private static UnitPrototype Boat(string name, int capacity) => new() {
		name = name,
		capacity = capacity,
		categories = ["Sea"],
		actions = [UnitAction.Unload],
	};

	private static UnitPrototype Soldier() => new() {
		name = "Warrior",
		categories = ["Land"],
		actions = [UnitAction.Load],
	};

	[Fact]
	public void NoTransportId_LoadsIntoTheBestCandidateWithoutAsking() {
		MapUnit galley = AddUnit(Boat("Galley", 2));
		MapUnit caravel = AddUnit(Boat("Caravel", 3));
		MapUnit warrior = AddUnit(Soldier());

		new MsgLoadToTransport(warrior.id).process();

		Assert.True(warrior.IsLoadedIn(caravel));
		Assert.False(warrior.IsLoadedIn(galley));
		Assert.Empty(EngineStorage.messagesToUI.OfType<MsgSelectTransport>());
	}

	[Fact]
	public void ChoosingATransport_LoadsTheUnitIntoIt() {
		MapUnit galley = AddUnit(Boat("Galley", 2));
		MapUnit caravel = AddUnit(Boat("Caravel", 3));
		MapUnit warrior = AddUnit(Soldier());

		new MsgLoadToTransport(warrior.id, galley.id).process();

		Assert.True(warrior.IsLoadedIn(galley));
		Assert.False(warrior.IsLoadedIn(caravel));
		Assert.Equal(1, galley.FreeCapacity());
		Assert.Empty(EngineStorage.messagesToUI.OfType<MsgSelectTransport>());
	}

	[Fact]
	public void ChoosingATransportThatCannotTakeTheUnit_DoesNothing() {
		MapUnit fullGalley = AddUnit(Boat("Galley", 1));
		MapUnit caravel = AddUnit(Boat("Caravel", 3));
		Player other = new() { id = ID.FromString("player-3"), civilization = new Civilization("Greece") };
		MapUnit foreignGalley = AddUnit(Boat("Galley", 2), other);
		MapUnit first = AddUnit(Soldier());
		MapUnit second = AddUnit(Soldier());
		first.BoardTransport(fullGalley);

		new MsgLoadToTransport(second.id, fullGalley.id).process();
		new MsgLoadToTransport(second.id, foreignGalley.id).process();

		Assert.False(second.IsLoaded());
		Assert.Equal(0, fullGalley.FreeCapacity());
		Assert.Equal(3, caravel.FreeCapacity());
	}

	[Fact]
	public void OneTransport_IsUsedWithoutAsking() {
		MapUnit galley = AddUnit(Boat("Galley", 2));
		MapUnit warrior = AddUnit(Soldier());

		new MsgLoadToTransport(warrior.id).process();

		Assert.True(warrior.IsLoadedIn(galley));
		Assert.Empty(EngineStorage.messagesToUI.OfType<MsgSelectTransport>());
	}

	[Fact]
	public void FullTransportsAreNotOffered_SoOneFreeTransportIsUsedWithoutAsking() {
		MapUnit fullGalley = AddUnit(Boat("Galley", 1));
		MapUnit caravel = AddUnit(Boat("Caravel", 3));
		MapUnit first = AddUnit(Soldier());
		MapUnit second = AddUnit(Soldier());
		first.BoardTransport(fullGalley);

		new MsgLoadToTransport(second.id).process();

		Assert.True(second.IsLoadedIn(caravel));
		Assert.Empty(EngineStorage.messagesToUI.OfType<MsgSelectTransport>());
	}

	[Fact]
	public void NoTransport_DoesNothing() {
		MapUnit warrior = AddUnit(Soldier());

		new MsgLoadToTransport(warrior.id).process();

		Assert.False(warrior.IsLoaded());
		Assert.Empty(EngineStorage.messagesToUI);
	}

	[Fact]
	public void TransportsToBoard_ListsEveryTransportWithRoom() {
		MapUnit empty = AddUnit(Boat("Galleon", 4));
		MapUnit used = AddUnit(Boat("Caravel", 3));
		MapUnit full = AddUnit(Boat("Galley", 1));
		MapUnit warrior = AddUnit(Soldier());
		AddUnit(Soldier()).BoardTransport(used);
		AddUnit(Soldier()).BoardTransport(full);

		List<MapUnit> transports = warrior.GetTransportsToBoard(tile);

		Assert.Equal(2, transports.Count);
		Assert.Contains(empty, transports);
		Assert.Contains(used, transports);
	}

	[Fact]
	public void TransportsToBoard_SkipsTransportsOfOtherPlayers() {
		Player other = new() { id = ID.FromString("player-3"), civilization = new Civilization("Greece") };
		AddUnit(Boat("Galley", 2), other);
		MapUnit mine = AddUnit(Boat("Caravel", 3));
		MapUnit warrior = AddUnit(Soldier());

		Assert.Equal(new[] { mine }, warrior.GetTransportsToBoard(tile));
	}
}
