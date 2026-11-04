using System;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class AgeBonusTest : CityTest {
	private static BehaviorEngine LoadBehaviors() {
		return GameMode.Load(PathUtils.GameModesDir, new GameMode.Config("civ3")).behaviors;
	}

	// The game starts in the given year and the tests advance it by changing
	// the turn, so the build years come from the same place the engine reads.
	private (City city, Player player, C7GameData.GameData gameData) SetupCultureCity(int startYear = -4000) {
		C7GameData.GameData gameData = new() {
			rules = new Rules(),
			timeOptions = new TimeOptions { startYear = startYear },
		};
		EngineStorage.InitializeGameDataForTests(gameData);

		BehaviorEngine behaviors = LoadBehaviors();
		gameData.luaBehaviorEngine = behaviors;
		gameData.LoadAgeBonusFunction(behaviors);

		Player player = MakePlayerForProduction();
		player.rules = gameData.rules;
		City city = MakeCity(player);
		return (city, player, gameData);
	}

	// Years are taken from GetRawNumber so that they match the calendar the
	// engine would use; not every integer year is reachable.
	private static void SetYear(C7GameData.GameData gameData, int year) {
		gameData.turn = gameData.timeOptions.GetTurnFromRaw(year);
	}

	private static Building MakeCultureBuilding(int culturePerTurn = 2) {
		return MakeBuilding(new SaveBuilding {
			name = "Temple",
			culturePerTurn = culturePerTurn,
		});
	}

	[Fact]
	public void CultureBuilding_BelowThreshold_GetsNoBonus() {
		var setup = SetupCultureCity();
		setup.city.AddBuilding(MakeCultureBuilding(2), setup.gameData);

		// 950 years old, just short of the millennium.
		SetYear(setup.gameData, -3050);
		Assert.Equal(2, setup.city.GetCulturePerTurnRaw());
	}

	[Fact]
	public void CultureBuilding_AtThreshold_GetsDoubleCulture() {
		var setup = SetupCultureCity();
		setup.city.AddBuilding(MakeCultureBuilding(2), setup.gameData);

		SetYear(setup.gameData, -3000);
		setup.city.UpdateAgeBonuses(setup.gameData);

		Assert.Equal(4, setup.city.GetCulturePerTurnRaw());
	}

	[Fact]
	public void CultureBuilding_WellPastThreshold_DoublesOnlyOnce() {
		var setup = SetupCultureCity();
		setup.city.AddBuilding(MakeCultureBuilding(2), setup.gameData);

		// Over 3000 years old, but the default bonus does not compound.
		SetYear(setup.gameData, -750);
		setup.city.UpdateAgeBonuses(setup.gameData);

		Assert.Equal(4, setup.city.GetCulturePerTurnRaw());
	}

	[Fact]
	public void CultureBuilding_NoAgeBonusFlag_GetsNoBonus() {
		var setup = SetupCultureCity();
		Building building = MakeBuilding(new SaveBuilding {
			name = "Unique",
			culturePerTurn = 2,
			flags = [SaveBuilding.Flag.NoAgeBonus],
		});
		setup.city.AddBuilding(building, setup.gameData);

		SetYear(setup.gameData, -1750);
		Assert.Equal(2, setup.city.GetCulturePerTurnRaw());
	}

	[Fact]
	public void CultureBuilding_BuildingPropertyNoAgeBonus_GetsNoBonus() {
		var setup = SetupCultureCity();
		Building building = MakeCultureBuilding(2);
		building.noAgeBonus = true;
		setup.city.AddBuilding(building, setup.gameData);

		SetYear(setup.gameData, -1750);
		Assert.Equal(2, setup.city.GetCulturePerTurnRaw());
	}

	[Fact]
	public void CustomAgeBonusHook_OverridesTheDefaultFunction() {
		var setup = SetupCultureCity();
		setup.gameData.ageBonusFunc = (cb, gd, c, p) => 7;

		setup.city.AddBuilding(MakeCultureBuilding(2), setup.gameData);
		SetYear(setup.gameData, -3000);

		Assert.Equal(7, setup.city.GetCulturePerTurnRaw());
	}

	[Fact]
	public void CustomAgeBonusHook_ZeroCultureMultiplier_IsRespected() {
		var setup = SetupCultureCity();
		setup.gameData.ageBonusFunc = (cb, gd, c, p) => 0;

		setup.city.AddBuilding(MakeCultureBuilding(2), setup.gameData);
		SetYear(setup.gameData, -3000);

		Assert.Equal(0, setup.city.GetCulturePerTurnRaw());
	}

	[Fact]
	public void LoadAgeBonusFunction_UsesTheDefaultWhenTheSaveHasNoPath() {
		BehaviorEngine behaviors = LoadBehaviors();
		C7GameData.GameData gameData = new() {
			rules = new Rules { ageBonus = new AgeBonusRules { luaFunctionPath = null } },
		};

		gameData.LoadAgeBonusFunction(behaviors);

		Assert.NotNull(gameData.ageBonusFunc);
	}

	[Fact]
	public void LoadAgeBonusFunction_FallsBackWhenTheConfiguredPathIsMissing() {
		BehaviorEngine behaviors = LoadBehaviors();
		C7GameData.GameData gameData = new() {
			rules = new Rules { ageBonus = new AgeBonusRules { luaFunctionPath = "gameplay.culture.no_such_function" } },
		};

		gameData.LoadAgeBonusFunction(behaviors);

		Assert.NotNull(gameData.ageBonusFunc);
	}

	[Fact]
	public void TryImportFunc_MissingPath_ReturnsFalse() {
		BehaviorEngine behaviors = LoadBehaviors();
		Assert.False(behaviors.TryImportFunc<Func<CityBuilding, C7GameData.GameData, City, Player, int>>("gameplay.culture.no_such_function", out Func<CityBuilding, C7GameData.GameData, City, Player, int> func));
		Assert.Null(func);
	}

	[Fact]
	public void TryImportFunc_ExistingPath_ReturnsTheFunction() {
		BehaviorEngine behaviors = LoadBehaviors();
		Assert.True(behaviors.TryImportFunc<Func<CityBuilding, C7GameData.GameData, City, Player, int>>(AgeBonusRules.DefaultLuaFunctionPath, out Func<CityBuilding, C7GameData.GameData, City, Player, int> func));
		Assert.NotNull(func);
	}

	[Fact]
	public void GetAbsoluteYear_YearsBaseUnit_ReturnsRawYear() {
		TimeOptions timeOptions = new() { startYear = -4000 };
		Assert.Equal(-4000, timeOptions.GetAbsoluteYear(0));
		Assert.Equal(-3000, timeOptions.GetAbsoluteYear(timeOptions.GetTurnFromRaw(-3000)));
	}

	[Fact]
	public void AddBuilding_RecordsTheCurrentYear() {
		var setup = SetupCultureCity();

		setup.city.AddBuilding(MakeCultureBuilding(2), setup.gameData);
		Assert.Equal(-4000, setup.city.constructed_buildings[0].year);

		SetYear(setup.gameData, -3000);
		setup.city.AddBuilding(MakeCultureBuilding(2), setup.gameData);
		Assert.Equal(-3000, setup.city.constructed_buildings[1].year);
	}

	[Fact]
	public void LoadedBuildingWithoutCultureData_UsesTheBuildingCulture() {
		Building building = MakeCultureBuilding(2);
		Player player = MakePlayerForProduction();

		CityBuilding cb = new SaveCityBuilding {
			building = building.name,
			year = -4000,
		}.ToCityBuilding([building], [player]);

		Assert.Equal(2, cb.baseCulturePerTurn);
		Assert.Equal(2, cb.currentCulturePerTurn);
	}

	[Fact]
	public void LoadedBuildingKeepsSavedCulture() {
		Building building = MakeCultureBuilding(2);
		Player player = MakePlayerForProduction();

		CityBuilding cb = new SaveCityBuilding {
			building = building.name,
			year = -4000,
			baseCulturePerTurn = 1,
			currentCulturePerTurn = 2,
		}.ToCityBuilding([building], [player]);

		Assert.Equal(1, cb.baseCulturePerTurn);
		Assert.Equal(2, cb.currentCulturePerTurn);
	}
}
