using System;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;
using static C7GameData.PlayerRelationship;

namespace EngineTests.GameData;

public class PlayerRelationshipTest : IClassFixture<SaveGameFixture> {
	C7GameData.GameData gameData;

	public PlayerRelationshipTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);

		EngineStorage.InitializeGameDataForTests(gameData);
	}

	[Fact]
	public void TestHumanToBarbarianRelationship() {
		C7GameData.GameData gd = gameData;

		Player playerA = gd.players[0];
		Player playerB = gd.players[2];

		Assert.False(TryGetRelationship(playerA, playerB, out _));

		Assert.True(AtWar(playerA, playerB));
		// because playerA is barbarians
		Assert.False(IsInAnyWar(playerB, gd.players));

		// because a player can't sign peace with barbarians
		Assert.Throws<Exception>(() => SignPeaceAfterWar(playerA, playerB, gd));

		Assert.False(TryGetRelationship(playerA, playerB, out _));
	}

	[Fact]
	public void TestRelationshipAtVariousPoints() {
		C7GameData.GameData gd = gameData;

		Player playerA = gd.players[1];
		Player playerB = gd.players[2];

		Assert.False(TryGetRelationship(playerA, playerB, out _));

		playerA.EnsureRelationshipExists(playerB);
		Assert.True(TryGetRelationship(playerA, playerB, out var relationshipA));
		PlayerRelationship relationshipB = null;
		if (TryGetRelationship(playerB, playerA, out var relB)) {
			relationshipB = relB;
		}
		Assert.True(AtPeace(playerA, playerB));
		Assert.False(IsInAnyWar(playerA, gd.players));
		Assert.False(IsInAnyWar(playerB, gd.players));
		Assert.False(HaveActiveRightOfPassage(playerA, playerB));

		// because players not at war can't sign a peace treaty
		Assert.Throws<Exception>(() => SignPeaceAfterWar(playerA, playerB, gd));

		MultiTurnDeal rop = new MultiTurnDeal(DealType.DiplomaticAgreement, DealSubType.RightOfPassage, DealDetails.Exchange,
			0, null, 20, 0, null);

		RegisterMultiTurnDeal(playerA, playerB, rop);
		Assert.True(HaveActiveRightOfPassage(playerA, playerB));

		int refusal = 10;
		DeclareWar(playerA, playerB, false, 0, refusal);
		Assert.True(AtWar(playerA, playerB));
		Assert.True(IsInAnyWar(playerA, gd.players));
		Assert.True(IsInAnyWar(playerB, gd.players));
		Assert.True(relationshipA.multiTurnDeals.Count == 0);
		Assert.True(relationshipB.multiTurnDeals.Count == 0);
		Assert.False(HaveActiveRightOfPassage(playerA, playerB));
		Assert.False(relationshipA.wasSneakAttacked);
		Assert.False(relationshipB.wasSneakAttacked);
		Assert.False(relationshipA.warDeclarationWithRoPActiveCount == 1);
		Assert.True(relationshipB.warDeclarationWithRoPActiveCount == 1);
		Assert.True(relationshipA.refuseContactUntilTurn == refusal / 2);
		Assert.True(relationshipB.refuseContactUntilTurn == refusal);

		SignPeaceAfterWar(playerA, playerB, gd);
		Assert.True(AtPeace(playerA, playerB));
		Assert.False(IsInAnyWar(playerA, gd.players));
		Assert.False(IsInAnyWar(playerB, gd.players));
		Assert.False(HaveActiveRightOfPassage(playerA, playerB));
	}


	[Fact]
	public void TestMultiTurnDealRegistration() {
		C7GameData.GameData gd = gameData;

		Player playerA = gd.players[2];
		Player playerB = gd.players[3];
		playerA.EnsureRelationshipExists(playerB);

		// A's relationship to B (to be)
		PlayerRelationship relationshipA = null;
		// B's relationship to A (to be)
		PlayerRelationship relationshipB = null;

		if (TryGetRelationship(playerA, playerB, out var relA))
			relationshipA = relA;
		if (TryGetRelationship(playerB, playerA, out var relB))
			relationshipB = relB;

		Assert.True(relationshipA.multiTurnDeals.Count == 1);
		Assert.True(relationshipB.multiTurnDeals.Count == 1);

		MultiTurnDeal horseDeal = new MultiTurnDeal(DealType.Resource, DealSubType.ResourcePerTurn, DealDetails.Inbound,
			0, "Horses", 20, 0, null);

		RegisterMultiTurnDeal(playerA, playerB, horseDeal);

		Assert.Contains(horseDeal, relationshipA.multiTurnDeals);
		Assert.True(relationshipA.multiTurnDeals.Count == 2);
		Assert.DoesNotContain(horseDeal, relationshipB.multiTurnDeals);
		Assert.True(relationshipB.multiTurnDeals.Count == 2);

		Assert.Contains(horseDeal, relationshipA.multiTurnDeals);
		Assert.Contains(MultiTurnDeal.GetCounterpartDeal(playerA, playerB, horseDeal), relationshipB.multiTurnDeals);


		MultiTurnDeal winesDeal = new MultiTurnDeal(DealType.Resource, DealSubType.ResourcePerTurn, DealDetails.Outbound,
			0, "Wines", 20, 0, null);

		RegisterMultiTurnDeal(playerB, playerA, winesDeal);

		Assert.DoesNotContain(winesDeal, relationshipA.multiTurnDeals);
		Assert.True(relationshipA.multiTurnDeals.Count == 3);
		Assert.Contains(winesDeal, relationshipB.multiTurnDeals);
		Assert.True(relationshipB.multiTurnDeals.Count == 3);

		Assert.Contains(winesDeal, relationshipB.multiTurnDeals);
		Assert.Contains(MultiTurnDeal.GetCounterpartDeal(playerB, playerA, winesDeal), relationshipA.multiTurnDeals);
	}
}
