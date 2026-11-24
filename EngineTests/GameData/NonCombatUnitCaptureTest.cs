using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Attacking a tile that only holds non-combat units does not start a battle. The
// units surrender instead: settlers become workers for the attacker, units the
// attacker has the technology for are captured, everything else (and everything,
// for barbarians) is destroyed. In every case the attacker takes the tile.
public class NonCombatUnitCaptureTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;
	private C7GameData.GameData gameData;

	private Player human;
	private Player victim;
	private Player barbarians;
	private Tile attackFrom;
	private Tile target;
	private TileDirection direction;

	public NonCombatUnitCaptureTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		EngineStorage.animationsEnabled = false;
		// Engine message queue is static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.messagesToUI.Clear();

		gameData = fixture.saveGame.Clone().ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);

		human = gameData.players.First(p => p.isHuman);
		victim = gameData.players.First(p => !p.isHuman && !p.isBarbarians);
		barbarians = gameData.players.First(p => p.isBarbarians);

		// Human and victim are at war with each other. Barbarians are always at war with everyone.
		human.DeclareWarOn(victim, 0);
		Assert.False(human.IsAtPeaceWith(victim));

		FindAttackAndTargetTiles();
	}

	// Finds two adjacent, empty land tiles near the human start.
	private void FindAttackAndTargetTiles() {
		Tile start = human.units[0].location;
		static bool IsFree(Tile t) => t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity();

		foreach (Tile candidate in start.GetTilesWithinRankDistance(3).Where(t => t.DistanceTo(start) >= 2 && IsFree(t))) {
			foreach (KeyValuePair<TileDirection, Tile> neighbor in candidate.neighbors) {
				if (!IsFree(neighbor.Value) || neighbor.Value.DistanceTo(start) < 2)
					continue;
				attackFrom = candidate;
				target = neighbor.Value;
				direction = neighbor.Key;
				return;
			}
		}
		throw new Exception("no two free adjacent land tiles near the human start");
	}

	private UnitPrototype Proto(string name) => gameData.unitPrototypes.Find(p => p.name == name);

	private MapUnit SpawnAttacker(Player owner) =>
		gameData.SpawnUnit(owner, Proto("Warrior"), attackFrom);

	private MapUnit SpawnDefender(string protoName, Player owner = null) =>
		gameData.SpawnUnit(owner ?? victim, Proto(protoName), target);

	private static bool IsAlive(MapUnit unit) => unit.hitPointsRemaining > 0;

	private void AssertAttackerTookTile(MapUnit attacker) {
		Assert.Equal(target, attacker.location);
		Assert.Contains(attacker, target.unitsOnTile);
		Assert.DoesNotContain(attacker, attackFrom.unitsOnTile);
	}

	private void AssertFullyCaptured(MapUnit unit, Player expectedOwner, Civilization expectedNationality) {
		Assert.True(IsAlive(unit));
		Assert.Equal(expectedOwner, unit.owner);
		Assert.Equal(expectedNationality, unit.nationality);
		Assert.Equal(target, unit.location);
		Assert.Contains(unit, target.unitsOnTile);
		Assert.Contains(unit, gameData.mapUnits);
		Assert.Contains(unit, expectedOwner.units);
		foreach (Player p in gameData.players.Where(p => p != expectedOwner))
			Assert.DoesNotContain(unit, p.units);
	}

	private void AssertDestroyed(MapUnit unit, Player formerOwner) {
		Assert.False(IsAlive(unit));
		Assert.DoesNotContain(unit, target.unitsOnTile);
		Assert.DoesNotContain(unit, gameData.mapUnits);
		Assert.DoesNotContain(unit, formerOwner.units);
	}

	private List<MapUnit> OwnedBy(Player owner) => target.unitsOnTile.Where(u => u.owner == owner).ToList();

	// ---- Captured workers ----

	[Fact]
	public async Task WorkerIsTransferredToTheAttackerAndKeepsItsNationality() {
		MapUnit attacker = SpawnAttacker(human);
		MapUnit worker = SpawnDefender("Worker");
		Civilization victimCiv = victim.civilization;
		int humanUnitsBefore = human.units.Count;
		int victimUnitsBefore = victim.units.Count;

		await attacker.Move(direction);

		AssertFullyCaptured(worker, human, victimCiv);
		Assert.True(worker.IsCaptive());
		Assert.Equal(humanUnitsBefore + 1, human.units.Count);
		Assert.Equal(victimUnitsBefore - 1, victim.units.Count);
		AssertAttackerTookTile(attacker);
	}

	[Fact]
	public async Task CapturedWorkerNoLongerBelongsToThePreviousOwnersAI() {
		MapUnit attacker = SpawnAttacker(human);
		MapUnit worker = SpawnDefender("Worker");
		// Automate() finds no work in this test world, so attach an AI by hand.
		worker.currentAI = new WorkerAI(new C7GameData.AIData.WorkerAIData());

		await attacker.Move(direction);

		Assert.Null(worker.currentAI);
		Assert.Equal(human, worker.owner);
	}

	[Fact]
	public async Task CapturedWorkerWorksAtSlaveSpeedUnlessItsNativeToTheNewOwner() {
		MapUnit attacker = SpawnAttacker(human);
		MapUnit worker = SpawnDefender("Worker");

		await attacker.Move(direction);

		// An own-nationality worker of the same owner, for comparison. This keeps the test
		// independent of whether the human's civ happens to be industrious.
		MapUnit nativeWorker = gameData.SpawnUnit(human, Proto("Worker"), target);
		Assert.False(nativeWorker.IsCaptive());
		Assert.True(worker.IsCaptive());

		// Civ3: slaves work at half the speed of regular workers.
		Assert.Equal(nativeWorker.workerSpeed(), worker.workerSpeed() * 2, precision: 3);
		Assert.True(worker.workerSpeed() < nativeWorker.workerSpeed());
	}

	[Fact]
	public async Task WorkerThatIsCapturedBackIsNoLongerCaptive() {
		// A worker captured by the human, then recaptured by its original civ, is native again.
		MapUnit attacker = SpawnAttacker(human);
		MapUnit worker = SpawnDefender("Worker");
		await attacker.Move(direction);
		Assert.True(worker.IsCaptive());

		// Move the human's units off the tile, so the victim can attack it again.
		Tile victimFrom = attackFrom;
		MapUnit counterAttacker = gameData.SpawnUnit(victim, Proto("Warrior"), victimFrom);
		// The human's attacker is a combat unit and defends the tile, so this is a real battle.
		// Remove it first so that only the captured worker is left.
		attacker.RemoveFromPlay();
		TileDirection back = victimFrom.neighbors.First(n => n.Value == target).Key;

		await counterAttacker.Move(back);

		AssertFullyCaptured(worker, victim, victim.civilization);
		Assert.False(worker.IsCaptive());
	}

	// ---- Captured settlers ----

	[Theory]
	[InlineData(0, 1)] // always at least one worker
	[InlineData(1, 1)]
	[InlineData(2, 2)]
	[InlineData(3, 3)]
	public async Task SettlerBecomesOneWorkerPerPopulationCost(int populationCost, int expectedWorkers) {
		Proto("Settler").populationCost = populationCost;
		MapUnit attacker = SpawnAttacker(human);
		MapUnit settler = SpawnDefender("Settler");
		Civilization victimCiv = victim.civilization;
		int humanUnitsBefore = human.units.Count;

		await attacker.Move(direction);

		AssertDestroyed(settler, victim);
		List<MapUnit> workers = OwnedBy(human).Where(u => u != attacker).ToList();
		Assert.Equal(expectedWorkers, workers.Count);
		Assert.Equal(humanUnitsBefore + expectedWorkers, human.units.Count);
		foreach (MapUnit worker in workers) {
			Assert.True(worker.unitType.isWorker);
			AssertFullyCaptured(worker, human, victimCiv);
			Assert.True(worker.IsCaptive());
		}
		AssertAttackerTookTile(attacker);
	}

	[Fact]
	public async Task SettlerWorkersAreTheCheapestWorkerTheAttackerCanBuild() {
		UnitPrototype worker = Proto("Worker");
		Tech unknownTech = gameData.techs.First(t => !human.knownTechs.Contains(t.id));

		// Cheaper, but the attacker lacks the tech.
		UnitPrototype needsTech = CloneWorker("Needs Tech Worker", worker, worker.shieldCost - 1);
		needsTech.requiredTech = unknownTech;
		// Cheaper, but can't be built at all.
		UnitPrototype unproducible = CloneWorker("Unproducible Worker", worker, worker.shieldCost - 1);
		unproducible.unproducible = true;
		// More expensive, which must lose against the plain worker.
		CloneWorker("Expensive Worker", worker, worker.shieldCost + 10);

		MapUnit attacker = SpawnAttacker(human);
		SpawnDefender("Settler");

		await attacker.Move(direction);

		List<MapUnit> workers = OwnedBy(human).Where(u => u != attacker).ToList();
		Assert.NotEmpty(workers);
		Assert.All(workers, w => Assert.Equal(worker, w.unitType));
	}

	[Fact]
	public async Task SettlerIsJustRemovedWhenThereIsNoWorkerToCreate() {
		gameData.unitPrototypes.RemoveAll(p => p.isWorker);
		MapUnit attacker = SpawnAttacker(human);
		MapUnit settler = SpawnDefender("Settler");
		int humanUnitsBefore = human.units.Count;

		await attacker.Move(direction);

		AssertDestroyed(settler, victim);
		Assert.Equal(humanUnitsBefore, human.units.Count);
		AssertAttackerTookTile(attacker);
	}

	// ---- Other non-combat units ----

	[Fact]
	public async Task OtherNonCombatUnitIsCapturedIfTheAttackerKnowsItsTechnologyAndCanProduceIt() {
		UnitPrototype scout = Proto("Scout"); // needs no tech
		Assert.Null(scout.requiredTech);
		scout.producibleBy.Add(human.civilization);
		MapUnit attacker = SpawnAttacker(human);
		MapUnit unit = SpawnDefender("Scout");
		Civilization victimCiv = victim.civilization;

		await attacker.Move(direction);

		AssertFullyCaptured(unit, human, victimCiv);
		Assert.True(unit.IsCaptive());
		AssertAttackerTookTile(attacker);
	}

	[Fact]
	public async Task NonCombatUnitIsDestroyedWhenTheAttackersCivCannotProduceIt() {
		// E.g. Egypt capturing a Portuguese scout: scouts are not producible by Egypt, so the scout dies,
		// even though no technology is missing.
		UnitPrototype scout = Proto("Scout");
		Assert.Null(scout.requiredTech);
		scout.producibleBy.Remove(human.civilization);
		scout.producibleBy.Add(victim.civilization);

		MapUnit attacker = SpawnAttacker(human);
		MapUnit unit = SpawnDefender("Scout");
		int humanUnitsBefore = human.units.Count;

		await attacker.Move(direction);

		AssertDestroyed(unit, victim);
		Assert.Equal(humanUnitsBefore, human.units.Count);
		Assert.Empty(OwnedBy(victim));
		AssertAttackerTookTile(attacker);
	}

	[Fact]
	public async Task ProducibilityAndTechnologyAreBothRequiredToCapture() {
		UnitPrototype explorer = Proto("Explorer");
		Assert.NotNull(explorer.requiredTech);
		explorer.producibleBy.Add(human.civilization);

		// Can produce, lacks the tech: destroyed
		human.knownTechs.Remove(explorer.requiredTech.id);
		MapUnit attacker = SpawnAttacker(human);
		MapUnit lacksTech = SpawnDefender("Explorer");
		await attacker.Move(direction);
		AssertDestroyed(lacksTech, victim);

		// Has the tech, can't produce: destroyed
		human.knownTechs.Add(explorer.requiredTech.id);
		explorer.producibleBy.Remove(human.civilization);
		attacker.location.unitsOnTile.Remove(attacker);
		attacker.location = attackFrom;
		attackFrom.unitsOnTile.Add(attacker);
		attacker.movementPoints.reset(attacker.unitType.movement);
		MapUnit notProducible = SpawnDefender("Explorer");
		await attacker.Move(direction);
		AssertDestroyed(notProducible, victim);
		AssertAttackerTookTile(attacker);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task FastNonCombatUnitsNeverRetreatTheyAreCapturedOrKilled(bool canCapture) {
		// Scouts have more than 1 movement point, which would let them retreat in a regular battle.
		UnitPrototype scout = Proto("Scout");
		Assert.True(scout.movement > 1);
		if (canCapture)
			scout.producibleBy.Add(human.civilization);
		else
			scout.producibleBy.Remove(human.civilization);

		MapUnit attacker = SpawnAttacker(human);
		MapUnit unit = SpawnDefender("Scout");

		CombatResult result = await attacker.Fight(unit);

		Assert.Equal(CombatResult.DefenderKilled, result);
		Assert.Empty(OwnedBy(victim));
		Assert.Equal(canCapture, IsAlive(unit));
		Assert.Equal(canCapture ? human : victim, unit.owner);
	}

	[Fact]
	public async Task NonCombatUnitIsCapturedWhenTheAttackerKnowsTheTechEvenIfItLacksTheResources() {
		UnitPrototype explorer = Proto("Explorer");
		Assert.NotNull(explorer.requiredTech);
		human.knownTechs.Add(explorer.requiredTech.id);
		explorer.producibleBy.Add(human.civilization);
		// A resource the human can't possibly have.
		explorer.requiredResources = [new Resource { Key = "unobtainium", Name = "Unobtainium", Category = ResourceCategory.STRATEGIC }];
		Assert.False(explorer.requiredResources.All(r => human.resourcesInBorders.ContainsKey(r)));

		MapUnit attacker = SpawnAttacker(human);
		MapUnit unit = SpawnDefender("Explorer");
		Civilization victimCiv = victim.civilization;

		await attacker.Move(direction);

		AssertFullyCaptured(unit, human, victimCiv);
		AssertAttackerTookTile(attacker);
	}

	[Fact]
	public async Task NonCombatUnitIsDestroyedWhenTheAttackerLacksTheTechnology() {
		UnitPrototype explorer = Proto("Explorer");
		Assert.NotNull(explorer.requiredTech);
		human.knownTechs.Remove(explorer.requiredTech.id);

		MapUnit attacker = SpawnAttacker(human);
		MapUnit unit = SpawnDefender("Explorer");
		int humanUnitsBefore = human.units.Count;

		await attacker.Move(direction);

		AssertDestroyed(unit, victim);
		Assert.Equal(humanUnitsBefore, human.units.Count);
		Assert.Empty(OwnedBy(human).Where(u => u != attacker));
		AssertAttackerTookTile(attacker);
	}

	[Fact]
	public async Task TechnologyOfTheVictimIsIrrelevantForCapture() {
		// The victim knows the tech, the attacker doesn't. The attacker's tech decides.
		UnitPrototype explorer = Proto("Explorer");
		human.knownTechs.Remove(explorer.requiredTech.id);
		victim.knownTechs.Add(explorer.requiredTech.id);

		MapUnit attacker = SpawnAttacker(human);
		MapUnit unit = SpawnDefender("Explorer");

		await attacker.Move(direction);

		AssertDestroyed(unit, victim);
	}

	// ---- Barbarians ----

	[Theory]
	[InlineData("Worker")]
	[InlineData("Settler")]
	public async Task BarbariansDestroyNonCombatUnitsInsteadOfCapturingThem(string protoName) {
		MapUnit attacker = SpawnAttacker(barbarians);
		MapUnit unit = SpawnDefender(protoName, human);
		int barbarianUnitsBefore = barbarians.units.Count;

		await attacker.Move(direction);

		AssertDestroyed(unit, human);
		// No captured unit and no workers made from a settler
		Assert.Equal(barbarianUnitsBefore, barbarians.units.Count);
		Assert.Empty(OwnedBy(barbarians).Where(u => u != attacker));
		AssertAttackerTookTile(attacker);
	}

	[Fact]
	public async Task BarbariansKillAFastNonCombatUnitWithoutItRetreating() {
		// Barbarians fight non-combat units in a regular battle. Scouts have more than one
		// movement point, which would normally let them retreat from a slow attacker, but
		// non-combat units never retreat: they die.
		MapUnit attacker = SpawnAttacker(barbarians);
		MapUnit scout = SpawnDefender("Scout", human);
		Assert.True(scout.unitType.movement > attacker.unitType.movement);

		await attacker.Move(direction);

		AssertDestroyed(scout, human);
		Assert.Empty(OwnedBy(barbarians).Where(u => u != attacker));
		AssertAttackerTookTile(attacker);
	}

	// ---- Stacks and combat ----

	[Fact]
	public async Task EveryNonCombatUnitOnTheTileIsHandledAndTheAttackerStillMoves() {
		UnitPrototype explorer = Proto("Explorer");
		human.knownTechs.Remove(explorer.requiredTech.id);

		MapUnit attacker = SpawnAttacker(human);
		MapUnit worker = SpawnDefender("Worker");      // captured
		MapUnit settler = SpawnDefender("Settler");    // becomes workers
		MapUnit unknownTech = SpawnDefender("Explorer"); // destroyed
		Civilization victimCiv = victim.civilization;

		await attacker.Move(direction);

		AssertFullyCaptured(worker, human, victimCiv);
		AssertDestroyed(settler, victim);
		AssertDestroyed(unknownTech, victim);
		Assert.Empty(OwnedBy(victim));
		AssertAttackerTookTile(attacker);
	}

	[Fact]
	public async Task NonCombatUnitsAreNotCapturedWhileAnotherCombatUnitStillDefendsTheTile() {
		MapUnit attacker = SpawnAttacker(human);
		MapUnit guard1 = SpawnDefender("Warrior");
		MapUnit guard2 = SpawnDefender("Warrior");
		MapUnit worker = SpawnDefender("Worker");
		// Always win the first battle, which only kills one of the guards
		attacker.hitPointsRemaining = 1000;
		guard1.hitPointsRemaining = 1;
		guard2.hitPointsRemaining = 1;

		await attacker.Move(direction);

		Assert.Single(OwnedBy(victim).Where(u => u.unitType.name == "Warrior"));
		Assert.Equal(victim, worker.owner);
		Assert.Equal(victim.civilization, worker.nationality);
		Assert.Contains(worker, target.unitsOnTile);
		Assert.Equal(attackFrom, attacker.location);
	}

	// A fast unit (more than 1 movement point) which retreats from slow attackers with certainty, if it may.
	private MapUnit SpawnCertainRetreater(Player owner) {
		MapUnit horseman = gameData.SpawnUnit(owner, Proto("Horseman"), target);
		horseman.experienceLevel = new ExperienceLevel("test", "Test", 3, 1.0, 0.0);
		horseman.hitPointsRemaining = 3;
		return horseman;
	}

	[Fact]
	public async Task LastCombatDefenderProtectingNonCombatUnitsNeverRetreats() {
		MapUnit attacker = SpawnAttacker(human);
		MapUnit horseman = SpawnCertainRetreater(victim);
		MapUnit worker = SpawnDefender("Worker");
		Civilization victimCiv = victim.civilization;
		attacker.hitPointsRemaining = 1000;

		CombatResult result = await attacker.Fight(horseman);

		// Retreating would leave the worker undefended, so the horseman fights to the death instead.
		Assert.Equal(CombatResult.DefenderKilled, result);
		AssertDestroyed(horseman, victim);
		AssertFullyCaptured(worker, human, victimCiv);
	}

	[Fact]
	public async Task LoneCombatDefenderStillRetreatsWhenThereIsNothingToProtect() {
		MapUnit attacker = SpawnAttacker(human);
		MapUnit horseman = SpawnCertainRetreater(victim);
		attacker.hitPointsRemaining = 1000;
		Tile retreatDestination = target.neighbors[direction];
		Assert.True(retreatDestination != Tile.NONE && horseman.CanEnter(retreatDestination), "test world has nowhere to retreat to");

		CombatResult result = await attacker.Fight(horseman);

		Assert.Equal(CombatResult.DefenderRetreated, result);
		Assert.True(IsAlive(horseman));
		Assert.Equal(retreatDestination, horseman.location);
	}

	[Fact]
	public async Task CombatDefenderMayStillRetreatWhenAnotherCombatUnitCoversTheTile() {
		MapUnit attacker = SpawnAttacker(human);
		MapUnit horseman = SpawnCertainRetreater(victim);
		MapUnit otherGuard = SpawnDefender("Warrior");
		MapUnit worker = SpawnDefender("Worker");
		attacker.hitPointsRemaining = 1000;
		Tile retreatDestination = target.neighbors[direction];
		Assert.True(retreatDestination != Tile.NONE && horseman.CanEnter(retreatDestination), "test world has nowhere to retreat to");

		CombatResult result = await attacker.Fight(horseman);

		Assert.Equal(CombatResult.DefenderRetreated, result);
		Assert.True(IsAlive(horseman));
		Assert.Equal(retreatDestination, horseman.location);
		// The other guard still defends, so the worker is untouched
		Assert.Contains(otherGuard, target.unitsOnTile);
		Assert.Equal(victim, worker.owner);
	}

	[Fact]
	public async Task NonCombatUnitsAreCapturedAfterTheLastCombatUnitIsKilled() {
		MapUnit attacker = SpawnAttacker(human);
		MapUnit guard = SpawnDefender("Warrior");
		MapUnit worker = SpawnDefender("Worker");
		Civilization victimCiv = victim.civilization;
		attacker.hitPointsRemaining = 1000;
		guard.hitPointsRemaining = 1;

		await attacker.Move(direction);

		AssertDestroyed(guard, victim);
		AssertFullyCaptured(worker, human, victimCiv);
		Assert.True(worker.IsCaptive());
		Assert.Empty(OwnedBy(victim));
		AssertAttackerTookTile(attacker);
	}

	[Fact]
	public async Task SettlersAreConvertedAfterTheLastCombatUnitIsKilled() {
		Proto("Settler").populationCost = 2;
		MapUnit attacker = SpawnAttacker(human);
		MapUnit guard = SpawnDefender("Warrior");
		MapUnit settler = SpawnDefender("Settler");
		attacker.hitPointsRemaining = 1000;
		guard.hitPointsRemaining = 1;

		await attacker.Move(direction);

		AssertDestroyed(guard, victim);
		AssertDestroyed(settler, victim);
		List<MapUnit> workers = OwnedBy(human).Where(u => u != attacker).ToList();
		Assert.Equal(2, workers.Count);
		Assert.All(workers, w => {
			Assert.True(w.unitType.isWorker);
			Assert.Equal(victim.civilization, w.nationality);
		});
		AssertAttackerTookTile(attacker);
	}

	[Fact]
	public async Task NonCombatUnitsAreDestroyedAfterTheLastCombatUnitIsKilledIfTheAttackerCannotCaptureThem() {
		UnitPrototype explorer = Proto("Explorer");
		human.knownTechs.Remove(explorer.requiredTech.id);
		MapUnit attacker = SpawnAttacker(human);
		MapUnit guard = SpawnDefender("Warrior");
		MapUnit unit = SpawnDefender("Explorer");
		attacker.hitPointsRemaining = 1000;
		guard.hitPointsRemaining = 1;

		await attacker.Move(direction);

		AssertDestroyed(guard, victim);
		AssertDestroyed(unit, victim);
		Assert.Empty(OwnedBy(human).Where(u => u != attacker));
		AssertAttackerTookTile(attacker);
	}

	[Fact]
	public async Task BarbariansDestroyNonCombatUnitsAfterTheLastCombatUnitIsKilled() {
		MapUnit attacker = SpawnAttacker(barbarians);
		MapUnit guard = SpawnDefender("Warrior", human);
		MapUnit worker = SpawnDefender("Worker", human);
		MapUnit settler = SpawnDefender("Settler", human);
		int barbarianUnitsBefore = barbarians.units.Count;
		attacker.hitPointsRemaining = 1000;
		guard.hitPointsRemaining = 1;

		await attacker.Move(direction);

		AssertDestroyed(guard, human);
		AssertDestroyed(worker, human);
		AssertDestroyed(settler, human);
		Assert.Equal(barbarianUnitsBefore, barbarians.units.Count);
		AssertAttackerTookTile(attacker);
	}

	[Fact]
	public async Task FriendlyNonCombatUnitsOnTheTileAreLeftAloneWhenTheLastCombatUnitIsKilled() {
		MapUnit attacker = SpawnAttacker(human);
		MapUnit guard = SpawnDefender("Warrior");
		MapUnit ownWorker = SpawnDefender("Worker", human);
		Civilization humanCiv = human.civilization;
		attacker.hitPointsRemaining = 1000;
		guard.hitPointsRemaining = 1;

		await attacker.Move(direction);

		AssertFullyCaptured(ownWorker, human, humanCiv);
		Assert.False(ownWorker.IsCaptive());
		AssertAttackerTookTile(attacker);
	}

	[Fact]
	public async Task NonCombatUnitsAreNotTouchedWhenTheAttackerLosesTheBattle() {
		MapUnit attacker = SpawnAttacker(human);
		MapUnit guard = SpawnDefender("Warrior");
		MapUnit worker = SpawnDefender("Worker");
		// Always lose
		attacker.hitPointsRemaining = 1;
		guard.hitPointsRemaining = 1000;
		attacker.unitType.attack = 1;
		guard.unitType.defense = 1000;

		await attacker.Move(direction);

		Assert.False(IsAlive(attacker));
		Assert.True(IsAlive(guard));
		Assert.Equal(victim, worker.owner);
		Assert.Contains(worker, target.unitsOnTile);
	}

	[Fact]
	public async Task AttackerDoesNotMoveIntoTileWhileAHostileDefenderRemains() {
		// After winning a fight against the top defender the attacker only advances once
		// no hostile defender is left on the tile.
		MapUnit attacker = SpawnAttacker(human);
		MapUnit guard1 = SpawnDefender("Warrior");
		MapUnit guard2 = SpawnDefender("Warrior");
		// Always win
		attacker.hitPointsRemaining = 1000;
		guard1.hitPointsRemaining = 1;
		guard2.hitPointsRemaining = 1;

		await attacker.Move(direction);

		// The first fight kills one guard, the other still holds the tile
		Assert.Single(OwnedBy(victim));
		Assert.Equal(attackFrom, attacker.location);
	}

	[Fact]
	public async Task AttackerMovesIntoTileOnceTheLastDefenderIsKilledInBattle() {
		MapUnit attacker = SpawnAttacker(human);
		MapUnit guard = SpawnDefender("Warrior");
		attacker.hitPointsRemaining = 1000;
		guard.hitPointsRemaining = 1;

		await attacker.Move(direction);

		AssertDestroyed(guard, victim);
		AssertAttackerTookTile(attacker);
	}

	[Fact]
	public async Task OwnUnitsOnTheTileDoNotBlockTheAttackerAfterAVictory() {
		// Friendly units that end up on the target tile, whether captured or already there,
		// must not be mistaken for hostile defenders.
		MapUnit attacker = SpawnAttacker(human);
		MapUnit guard = SpawnDefender("Warrior");
		MapUnit friendlyWorker = SpawnDefender("Worker", human);
		attacker.hitPointsRemaining = 1000;
		guard.hitPointsRemaining = 1;

		await attacker.Move(direction);

		AssertDestroyed(guard, victim);
		Assert.Contains(friendlyWorker, target.unitsOnTile);
		AssertAttackerTookTile(attacker);
	}

	[Fact]
	public async Task CapturingCostsTheAttackerOneMovementPoint() {
		MapUnit attacker = SpawnAttacker(human);
		SpawnDefender("Worker");
		int movementBefore = (int)attacker.movementPoints.remaining;
		Assert.True(movementBefore >= 1);

		await attacker.Move(direction);

		AssertAttackerTookTile(attacker);
		Assert.True(attacker.movementPoints.remaining < movementBefore || attacker.movementPoints.remaining == 0);
	}

	// A copy of the prototype, so it also is a worker. Registered in the game data's prototypes.
	private UnitPrototype CloneWorker(string name, UnitPrototype template, int shieldCost) {
		UnitPrototype clone = new() {
			name = name,
			art = template.art,
			shieldCost = shieldCost,
			movement = template.movement,
			attack = 0,
			defense = 0,
			actions = template.actions,
			terraformActions = template.terraformActions,
		};
		foreach (string category in template.categories)
			clone.categories.Add(category);
		gameData.unitPrototypes.Add(clone);
		return clone;
	}
}
