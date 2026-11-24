using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using QueryCiv3;
using Xunit;

namespace EngineTests.GameData;

public class UnitPrototypeConquestsTest : RemoteSaveLoader {
	private const string SAVES_FOLDER = "saves/unit-availability";

	[SkippableFact]
	public async void UnitAvailability_SAV() {
		// This tests a Conquests game with Conquests rules from a .SAV file

		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		string saveName = "Conquests 16 Players.SAV";
		string uri = "https://www.dropbox.com/scl/fi/gmxbx1mtrammzfc6vly1g/Conquests-16-Players.SAV?rlkey=2z1es5aetqva4ymv59qduq1at&st=d0udmb3w&dl=1";

		(SaveGame game, Exception ex, string savePath) = await LoadGameAndData(saveName, SAVES_FOLDER, uri);

		Assert.Null(ex);
		Assert.NotNull(game);
		Assert.True(File.Exists(savePath));

		TestUnitAvailabilityAndUpgrades(game);
	}

	[SkippableFact]
	public async void UnitAvailability_JSON() {
		// This tests a Conquests game with Conquests rules from a .json file

		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		string saveName = "Conquests 16 Players.json";
		string uri = "https://www.dropbox.com/scl/fi/g1qxuvc6xptg1l6hx9s21/Conquests-16-Players.json?rlkey=bkq158od7469pibhtw44g04if&st=tqax1064&dl=1";

		(SaveGame game, Exception ex, string savePath) = await LoadGameAndData(saveName, SAVES_FOLDER, uri);

		Assert.Null(ex);
		Assert.NotNull(game);
		Assert.True(File.Exists(savePath));

		TestUnitAvailabilityAndUpgrades(game);
	}

	private void TestUnitAvailabilityAndUpgrades(SaveGame game) {
		#region Setup
		var gd = SaveGameFixture.HydrateSaveGame(game);
		EngineStorage.InitializeGameDataForTests(gd);

		List<UnitPrototype> protos = gd.unitPrototypes;

		// setup civilizations and players
		var netherlands = gd.civilizations.FirstOrDefault(c => c.name == "Netherlands");
		var nether = new Player() { civilization = netherlands };
		var americans = gd.civilizations.FirstOrDefault(c => c.name == "America");
		var amer = new Player() { civilization = americans };
		var ottomans = gd.civilizations.FirstOrDefault(c => c.name == "Ottomans");
		var otto = new Player() { civilization = ottomans };
		var russians = gd.civilizations.FirstOrDefault(c => c.name == "Russia");
		var rus = new Player() { civilization = russians };
		var koreans = gd.civilizations.FirstOrDefault(c => c.name == "Korea");
		var kor = new Player() { civilization = koreans };
		var hittites = gd.civilizations.FirstOrDefault(c => c.name == "Hittites");
		var hit = new Player() { civilization = hittites };
		var persians = gd.civilizations.FirstOrDefault(c => c.name == "Persia");
		var per = new Player() { civilization = persians };
		var sumerians = gd.civilizations.FirstOrDefault(c => c.name == "Sumeria");
		var sum = new Player() { civilization = sumerians };
		var spanish = gd.civilizations.FirstOrDefault(c => c.name == "Spain");
		var span = new Player() { civilization = spanish };

		// Units that start golden ages are not obsolete until their owner has had a golden age (see GoldenAgeTriggerTest),
		// so give every player one in order to test general unit obsolescence.
		foreach (Player p in new[] { nether, amer, otto, rus, kor, hit, per, sum, span }) {
			p.goldenAgeEnds = 1;
		}

		Assert.NotNull(netherlands);
		Assert.NotNull(americans);
		Assert.NotNull(ottomans);
		Assert.NotNull(russians);
		Assert.NotNull(koreans);
		Assert.NotNull(hittites);
		Assert.NotNull(persians);
		Assert.NotNull(sumerians);

		// setup resources
		var noResources = new HashSet<Resource>(){};
		var horses = gd.Resources.FirstOrDefault(r => r.Key == "Horses");
		var iron = gd.Resources.FirstOrDefault(r => r.Key == "Iron");
		var saltpeter = gd.Resources.FirstOrDefault(r => r.Key == "Saltpeter");
		var rubber = gd.Resources.FirstOrDefault(r => r.Key == "Rubber");
		var oil = gd.Resources.FirstOrDefault(r => r.Key == "Oil");
		var aluminum = gd.Resources.FirstOrDefault(r => r.Key == "Aluminum");
		var gunpowder = gd.techs.FirstOrDefault(t => t.Name == "Gunpowder");

		// setup techs
		var wheel = gd.techs.FirstOrDefault(t => t.Name == "The Wheel");
		var bronze = gd.techs.FirstOrDefault(t => t.Name == "Bronze Working");
		var ironWorking = gd.techs.FirstOrDefault(t => t.Name == "Iron Working");
		var horsebackRiding = gd.techs.FirstOrDefault(t => t.Name == "Horseback Riding");
		var mathematics = gd.techs.FirstOrDefault(t => t.Name == "Mathematics");
		var feudalism = gd.techs.FirstOrDefault(t => t.Name == "Feudalism");
		var engineering = gd.techs.FirstOrDefault(t => t.Name == "Engineering");
		var chivalry = gd.techs.FirstOrDefault(t => t.Name == "Chivalry");
		var metallurgy = gd.techs.FirstOrDefault(t => t.Name == "Metallurgy");
		var astronomy = gd.techs.FirstOrDefault(t => t.Name == "Astronomy");
		var milTradition = gd.techs.FirstOrDefault(t => t.Name == "Military Tradition");
		var nationalism = gd.techs.FirstOrDefault(t => t.Name == "Nationalism");
		var flight = gd.techs.FirstOrDefault(t => t.Name == "Flight");
		var replaceableParts = gd.techs.FirstOrDefault(t => t.Name == "Replaceable Parts");
		var rocketry = gd.techs.FirstOrDefault(t => t.Name == "Rocketry");

		// setup some unit prototypes
		var warrior = protos.FirstOrDefault(p => p.name == "Warrior");
		var spearman = protos.FirstOrDefault(p => p.name == "Spearman");
		var enkidu = protos.FirstOrDefault(p => p.name == "Enkidu Warrior");
		var swordsman = protos.FirstOrDefault(p => p.name == "Swordsman");
		var immortals = protos.FirstOrDefault(p => p.name == "Immortals");
		var pikeman = protos.FirstOrDefault(p => p.name == "Pikeman");
		var medInfantry = protos.FirstOrDefault(p => p.name == "Medieval Infantry");
		var swiss = protos.FirstOrDefault(p => p.name == "Swiss Mercenary");
		var horseman = protos.FirstOrDefault(p => p.name == "Horseman");
		var chariot = protos.FirstOrDefault(p => p.name == "Chariot");
		var threeManChariot = protos.FirstOrDefault(p => p.name == "Three-Man Chariot");
		var knight = protos.FirstOrDefault(p => p.name == "Knight");
		var cavalry = protos.FirstOrDefault(p => p.name == "Cavalry");
		var sipahi = protos.FirstOrDefault(p => p.name == "Sipahi");
		var cossack = protos.FirstOrDefault(p => p.name == "Cossack");
		var catapult = protos.FirstOrDefault(p => p.name == "Catapult");
		var trebuchet = protos.FirstOrDefault(p => p.name == "Trebuchet");
		var cannon = protos.FirstOrDefault(p => p.name == "Cannon");
		var rifleman = protos.FirstOrDefault(p => p.name == "Rifleman");
		var hwacha = protos.FirstOrDefault(p => p.name == "Hwach'a");
		var scout = protos.FirstOrDefault(p => p.name == "Scout");
		var conquistador = protos.FirstOrDefault(p => p.name == "Conquistador");
		var explorer = protos.FirstOrDefault(p => p.name == "Explorer");
		var artillery = protos.FirstOrDefault(p => p.name == "Artillery");
		var fighter = protos.FirstOrDefault(p => p.name == "Fighter");
		var jet = protos.FirstOrDefault(p => p.name == "Jet Fighter");
		var f15 = protos.FirstOrDefault(p => p.name == "F-15");
		var musketman = protos.FirstOrDefault(p => p.name == "Musketman");
		var guerilla = protos.FirstOrDefault(p => p.name == "Guerilla");
		#endregion

		#region Ottomans
		Tile ankaraTile = new Tile(ID.None("tile"));
		City ankara = new City(ankaraTile, otto, "Ankara", gd.ids.CreateID("Ankara"));

		Assert.True(horseman.producibleBy.Contains(ottomans));
		Assert.True(knight.producibleBy.Contains(ottomans));
		Assert.True(sipahi.producibleBy.Contains(ottomans));
		Assert.False(cavalry.producibleBy.Contains(ottomans));

		otto.knownTechs.Add(horsebackRiding.id);

		Assert.True(horseman.CanProduce(ankara, new HashSet<Resource>() { horses, iron }));
		Assert.False(knight.CanProduce(ankara, new HashSet<Resource>() { horses, iron, saltpeter }));
		Assert.False(sipahi.CanProduce(ankara, new HashSet<Resource>() { horses, iron, saltpeter }));
		Assert.False(cavalry.CanProduce(ankara, new HashSet<Resource>() { horses, iron, saltpeter }));

		otto.knownTechs.Add(chivalry.id);
		// The Horseman is now obsolete
		Assert.False(horseman.CanProduce(ankara, new HashSet<Resource>() { horses, iron }));
		Assert.True(horseman.GetProducibleUpgrade(ankara, new HashSet<Resource>() { horses, iron }) == knight);

		// even though we have saltpeter, we don't know Military Tradition, so the Knight is not yet obsolete
		Assert.True(knight.CanProduce(ankara, new HashSet<Resource>() { horses, iron, saltpeter }));
		Assert.False(cavalry.CanProduce(ankara, new HashSet<Resource>() { horses, iron, saltpeter }));

		otto.knownTechs.Add(milTradition.id);
		// the Knight is now obsolete
		Assert.False(knight.CanProduce(ankara, new HashSet<Resource>() { horses, iron, saltpeter }));
		Assert.True(knight.GetProducibleUpgrade(ankara, new HashSet<Resource>() { horses, iron, saltpeter }) == sipahi);
		Assert.False(cavalry.CanProduce(ankara, new HashSet<Resource>() { horses, iron, saltpeter }));

		Assert.True(sipahi.GetProducibleUpgrade(ankara, new HashSet<Resource>() { horses, iron, saltpeter }) == null);
		#endregion

		#region Netherlands
		Tile amsterdamTile = new Tile(ID.None("tile"));
		City amsterdam = new City(amsterdamTile, nether, "Amsterdam", gd.ids.CreateID("Amsterdam"));

		Assert.True(spearman.producibleBy.Contains(netherlands));

		nether.knownTechs.Add(bronze.id);

		Assert.True(spearman.CanProduce(amsterdam, noResources));

		nether.knownTechs.Add(feudalism.id);
		Assert.True(spearman.CanProduce(amsterdam, noResources));
		Assert.True(swiss.CanProduce(amsterdam, new HashSet<Resource>() { iron }));
		// if the city has iron then we can't produce Spearman anymore, as we can produce their upgrade, the Swiss Mercenary
		Assert.False(spearman.CanProduce(amsterdam, new HashSet<Resource>() { iron }));

		nether.knownTechs.Add(gunpowder.id);
		Assert.True(swiss.CanProduce(amsterdam, new HashSet<Resource>() { iron }));
		Assert.False(swiss.CanProduce(amsterdam, new HashSet<Resource>() { saltpeter }));
		Assert.True(swiss.CanProduce(amsterdam, new HashSet<Resource>() { iron, saltpeter }));
		Assert.True(musketman.CanProduce(amsterdam, new HashSet<Resource>() { saltpeter }));
		Assert.False(musketman.CanProduce(amsterdam, new HashSet<Resource>() { iron, saltpeter }));
		Assert.False(rifleman.CanProduce(amsterdam, new HashSet<Resource>() { iron, saltpeter }));

		Assert.Null(swiss.GetProducibleUpgrade(amsterdam, new HashSet<Resource>() { iron, saltpeter }));
		Assert.False(swiss.GetProducibleUpgrade(amsterdam, new HashSet<Resource>() { iron, saltpeter }) == musketman);

		nether.knownTechs.Add(nationalism.id);
		Assert.True(rifleman.CanProduce(amsterdam, noResources));

		Assert.True(spearman.GetProducibleUpgrade(amsterdam, new HashSet<Resource>() { iron, saltpeter }) == rifleman);
		Assert.False(spearman.GetProducibleUpgrade(amsterdam, new HashSet<Resource>() { iron, saltpeter }) == swiss);

		Assert.False(spearman.CanProduce(amsterdam, new HashSet<Resource>() { iron, saltpeter }));
		Assert.False(swiss.CanProduce(amsterdam, new HashSet<Resource>() { iron, saltpeter }));
		Assert.True(swiss.GetProducibleUpgrade(amsterdam, new HashSet<Resource>() { iron, saltpeter }) == rifleman);
		Assert.True(rifleman.CanProduce(amsterdam, new HashSet<Resource>() { iron, saltpeter }));
		#endregion

		#region Hittites
		Tile hattusasTile = new Tile(ID.None("tile"));
		City hattusas = new City(hattusasTile, hit, "Hattusas", gd.ids.CreateID("Hattusas"));

		Assert.True(horseman.producibleBy.Contains(hittites));
		Assert.False(chariot.producibleBy.Contains(hittites));
		Assert.True(threeManChariot.producibleBy.Contains(hittites));


		hit.knownTechs.Add(wheel.id);

		Assert.False(horseman.CanProduce(hattusas, new HashSet<Resource>() { horses }));
		Assert.False(chariot.CanProduce(hattusas, new HashSet<Resource>() { horses }));
		Assert.True(threeManChariot.CanProduce(hattusas, new HashSet<Resource>() { horses }));

		#endregion

		#region Russians
		Tile moscowTile = new Tile(ID.None("tile"));
		City moscow = new City(moscowTile, rus, "Moscow", gd.ids.CreateID("Moscow"));

		Assert.True(horseman.producibleBy.Contains(russians));
		Assert.True(scout.producibleBy.Contains(russians));
		Assert.True(explorer.producibleBy.Contains(russians));
		Assert.True(knight.producibleBy.Contains(russians));
		Assert.True(cossack.producibleBy.Contains(russians));
		Assert.False(cavalry.producibleBy.Contains(russians));

		Assert.True(scout.CanProduce(moscow, noResources));
		Assert.Null(scout.GetProducibleUpgrade(moscow, noResources));

		rus.knownTechs.Add(horsebackRiding.id);

		Assert.True(horseman.CanProduce(moscow, new HashSet<Resource>() { horses, iron }));
		Assert.False(knight.CanProduce(moscow, new HashSet<Resource>() { horses, iron, saltpeter }));
		Assert.False(sipahi.CanProduce(moscow, new HashSet<Resource>() { horses, iron, saltpeter }));
		Assert.False(cavalry.CanProduce(moscow, new HashSet<Resource>() { horses, iron, saltpeter }));

		rus.knownTechs.Add(chivalry.id);
		// The Horseman is now obsolete
		Assert.False(horseman.CanProduce(moscow, new HashSet<Resource>() { horses, iron }));
		Assert.True(horseman.GetProducibleUpgrade(moscow, new HashSet<Resource>() { horses, iron }) == knight);

		// even though we have saltpeter, we don't know Military Tradition, so the Knight is not yet obsolete
		Assert.True(knight.CanProduce(moscow, new HashSet<Resource>() { horses, iron, saltpeter }));
		Assert.False(cavalry.CanProduce(moscow, new HashSet<Resource>() { horses, iron, saltpeter }));
		Assert.False(cossack.CanProduce(moscow, new HashSet<Resource>() { horses, iron, saltpeter }));

		rus.knownTechs.Add(milTradition.id);
		// the Knight is now obsolete
		Assert.False(knight.CanProduce(moscow, new HashSet<Resource>() { horses, iron, saltpeter }));
		Assert.True(knight.GetProducibleUpgrade(moscow, new HashSet<Resource>() { horses, iron, saltpeter }) == cossack);
		Assert.False(cavalry.CanProduce(moscow, new HashSet<Resource>() { horses, iron, saltpeter }));

		Assert.True(cossack.GetProducibleUpgrade(moscow, new HashSet<Resource>() { horses, iron, saltpeter }) == null);

		rus.knownTechs.Add(astronomy.id);
		Assert.False(scout.CanProduce(moscow, noResources));
		Assert.True(explorer.CanProduce(moscow, noResources));
		Assert.Equal(explorer, scout.GetProducibleUpgrade(moscow, noResources));
		#endregion

		#region Korean
		Tile seoulTile = new Tile(ID.None("tile"));
		City seoul = new City(seoulTile, kor, "Seoul", gd.ids.CreateID("Seoul"));

		// In conquests they both the Cannon and the Hwacha are permitted in the rules for the Koreans,
		// maybe an oversight in the original rules, but it's there.
		// We only care that the Trebuchet can upgrade to a Hwacha and not to a Cannon
		Assert.True(catapult.producibleBy.Contains(koreans));
		Assert.True(trebuchet.producibleBy.Contains(koreans));
		Assert.True(cannon.producibleBy.Contains(koreans));
		Assert.True(hwacha.producibleBy.Contains(koreans));

		Assert.False(catapult.CanProduce(seoul, noResources));
		Assert.False(trebuchet.CanProduce(seoul, noResources));
		Assert.False(cannon.CanProduce(seoul, noResources));
		Assert.False(hwacha.CanProduce(seoul, noResources));

		kor.knownTechs.Add(mathematics.id);

		Assert.True(catapult.CanProduce(seoul, noResources));
		Assert.False(trebuchet.CanProduce(seoul, noResources));
		Assert.False(cannon.CanProduce(seoul, noResources));
		Assert.False(hwacha.CanProduce(seoul, noResources));

		kor.knownTechs.Add(engineering.id);

		Assert.False(catapult.CanProduce(seoul, noResources));
		Assert.True(trebuchet.CanProduce(seoul, noResources));
		Assert.False(cannon.CanProduce(seoul, noResources));
		Assert.False(hwacha.CanProduce(seoul, noResources));

		Assert.True(catapult.GetProducibleUpgrade(seoul, noResources) == trebuchet);

		kor.knownTechs.Add(metallurgy.id);

		Assert.False(catapult.CanProduce(seoul, new HashSet<Resource>() { saltpeter }));
		Assert.False(trebuchet.CanProduce(seoul, new HashSet<Resource>() { saltpeter }));
		Assert.False(cannon.CanProduce(seoul, new HashSet<Resource>() { saltpeter }));
		Assert.True(hwacha.CanProduce(seoul, new HashSet<Resource>() { saltpeter }));

		Assert.True(trebuchet.GetProducibleUpgrade(seoul, new HashSet<Resource>() { saltpeter }) == hwacha);
		Assert.False(trebuchet.GetProducibleUpgrade(seoul, new HashSet<Resource>() { saltpeter }) == cannon);

		kor.knownTechs.Add(replaceableParts.id);
		Assert.False(hwacha.CanProduce(seoul, new HashSet<Resource>() { saltpeter }));
		Assert.True(artillery.CanProduce(seoul, new HashSet<Resource>() { }));
		Assert.True(hwacha.GetProducibleUpgrade(seoul, new HashSet<Resource>() { }) == artillery);

		#endregion

		#region America
		Tile seattleTile = new Tile(ID.None("tile"));
		City seattle = new City(seattleTile, amer, "Seattle", gd.ids.CreateID("Seattle"));

		Assert.True(fighter.producibleBy.Contains(americans));
		Assert.False(jet.producibleBy.Contains(americans));
		Assert.True(f15.producibleBy.Contains(americans));

		Assert.False(fighter.CanProduce(seattle, new HashSet<Resource>() { oil, aluminum }));
		Assert.False(jet.CanProduce(seattle, new HashSet<Resource>() { oil, aluminum }));
		Assert.False(f15.CanProduce(seattle, new HashSet<Resource>() { oil, aluminum }));

		amer.knownTechs.Add(flight.id);
		Assert.True(fighter.CanProduce(seattle, new HashSet<Resource>() { oil, aluminum }));
		Assert.False(jet.CanProduce(seattle, new HashSet<Resource>() { oil, aluminum }));
		Assert.False(f15.CanProduce(seattle, new HashSet<Resource>() { oil, aluminum }));

		Assert.True(fighter.GetProducibleUpgrade(seattle, new HashSet<Resource>() { oil, aluminum }) == null);

		amer.knownTechs.Add(rocketry.id);
		Assert.True(fighter.CanProduce(seattle, new HashSet<Resource>() { oil }));
		Assert.False(fighter.CanProduce(seattle, new HashSet<Resource>() { oil, aluminum }));
		Assert.False(jet.CanProduce(seattle, new HashSet<Resource>() { oil, aluminum }));
		Assert.True(f15.CanProduce(seattle, new HashSet<Resource>() { oil, aluminum }));

		Assert.True(fighter.GetProducibleUpgrade(seattle, new HashSet<Resource>() { oil, aluminum }) == f15);
		#endregion

		#region Persians
		Tile persepolisTile = new Tile(ID.None("tile"));
		City persepolis = new City(persepolisTile, per, "Persepolis", gd.ids.CreateID("Persepolis"));

		Assert.False(swordsman.producibleBy.Contains(persians));
		Assert.True(immortals.producibleBy.Contains(persians));

		per.knownTechs.Add(ironWorking.id);

		// Can't build swordsman even without iron (duh)
		Assert.False(swordsman.CanProduce(persepolis, noResources));
		Assert.False(swordsman.CanProduce(persepolis, new HashSet<Resource>() { iron }));
		Assert.True(immortals.CanProduce(persepolis, new HashSet<Resource>() { iron }));

		Assert.Null(immortals.GetProducibleUpgrade(persepolis, noResources));

		per.knownTechs.Add(feudalism.id);
		Assert.False(swordsman.CanProduce(persepolis, noResources));
		Assert.False(swordsman.CanProduce(persepolis, new HashSet<Resource>() { iron }));
		Assert.False(medInfantry.CanProduce(persepolis, new HashSet<Resource>() { iron }));
		Assert.True(immortals.CanProduce(persepolis, new HashSet<Resource>() { iron }));

		Assert.Null(immortals.GetProducibleUpgrade(persepolis, noResources));

		per.knownTechs.Add(replaceableParts.id);
		Assert.False(immortals.CanProduce(persepolis, new HashSet<Resource>() { iron }));
		Assert.True(guerilla.CanProduce(persepolis, noResources));
		Assert.True(guerilla.CanProduce(persepolis, new HashSet<Resource>() { iron }));
		Assert.Equal(guerilla, immortals.GetProducibleUpgrade(persepolis, noResources));
		#endregion

		#region Sumerians
		Tile sumerTile = new Tile(ID.None("tile"));
		City sumer = new City(sumerTile, sum, "Sumer", gd.ids.CreateID("Sumer"));

		Assert.DoesNotContain(sumerians, warrior.producibleBy);
		Assert.DoesNotContain(sumerians, spearman.producibleBy);
		Assert.Contains(sumerians, enkidu.producibleBy);

		Assert.False(warrior.CanProduce(sumer, noResources));
		Assert.False(spearman.CanProduce(sumer, noResources));
		Assert.True(enkidu.CanProduce(sumer, noResources));

		Assert.Null(enkidu.GetProducibleUpgrade(sumer, noResources));

		sum.knownTechs.Add(bronze.id);
		Assert.False(warrior.CanProduce(sumer, noResources));
		Assert.False(spearman.CanProduce(sumer, noResources));
		Assert.True(enkidu.CanProduce(sumer, noResources));

		Assert.Null(enkidu.GetProducibleUpgrade(sumer, noResources));

		sum.knownTechs.Add(feudalism.id);
		Assert.False(warrior.CanProduce(sumer, noResources));
		Assert.False(spearman.CanProduce(sumer, noResources));
		Assert.True(enkidu.CanProduce(sumer, noResources));
		Assert.False(enkidu.CanProduce(sumer, new HashSet<Resource>() { iron }));
		Assert.True(pikeman.CanProduce(sumer, new HashSet<Resource>() { iron }));
		Assert.False(pikeman.CanProduce(sumer, noResources));

		Assert.Null(enkidu.GetProducibleUpgrade(sumer, noResources));
		Assert.Equal(pikeman, enkidu.GetProducibleUpgrade(sumer, new HashSet<Resource>() { iron }));

		#endregion

		#region Spanish
		Tile madridTile = new Tile(ID.None("tile"));
		City madrid = new City(madridTile, span, "Madrid", gd.ids.CreateID("Madrid"));

		Assert.DoesNotContain(spanish, scout.producibleBy);
		Assert.DoesNotContain(spanish, explorer.producibleBy);
		Assert.Contains(spanish, conquistador.producibleBy);

		Assert.False(conquistador.CanProduce(madrid, noResources));
		Assert.False(conquistador.CanProduce(madrid, new HashSet<Resource>() { horses }));

		span.knownTechs.Add(astronomy.id);
		Assert.False(conquistador.CanProduce(madrid, noResources));
		Assert.True(conquistador.CanProduce(madrid, new HashSet<Resource>() { horses }));
		Assert.Null(conquistador.GetProducibleUpgrade(madrid, noResources));

		#endregion
	}

}

public class UnitPrototypeScenarioTest : RemoteSaveLoader {
	private const string SAVES_FOLDER = "saves/unit-availability";

	[SkippableFact]
	public async void UnitAvailability_SAV() {
		// This tests a Conquests scenario with custom rules

		// Civ3 isn't installed in CI, so we can't load the default BIC. Local
		// contributors without Civ3 assets configured should skip this too.
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		#region setup

		string scenarioBiqPath = Path.Combine(Civ3Location.GetCiv3Path(), "Conquests", "Conquests", "4 Middle Ages.biq");
		string scenarioPediaPath = Path.Combine(Civ3Location.GetCiv3Path(), "Conquests", "Conquests", "Middle Ages", "Text", "PediaIcons.txt");

		string saveName = "Middle Ages Scenario Abbasids, 843 AD.SAV";
		string uri = "https://www.dropbox.com/scl/fi/nz7wp7whu326i7em8jle9/Middle-Ages-Scenario-Abbasids-843-AD.SAV?rlkey=oz65m286jbchytd3yuu5ksr6f&st=b3dsheus&dl=1";

		(SaveGame game, Exception ex, string savePath) = await LoadGameAndData(saveName, SAVES_FOLDER, uri, scenarioBiqPath, scenarioPediaPath);

		Assert.Null(ex);
		Assert.NotNull(game);
		Assert.True(File.Exists(savePath));

		var gd = SaveGameFixture.HydrateSaveGame(game);
		EngineStorage.InitializeGameDataForTests(gd);

		List<UnitPrototype> protos = gd.unitPrototypes;

		// setup civilizations and players
		var byzantines = gd.civilizations.FirstOrDefault(c => c.name == "Byzantines");
		var byz = new Player() { civilization = byzantines };
		var kievanRus = gd.civilizations.FirstOrDefault(c => c.name == "Kievan Rus");
		var rus = new Player() { civilization = kievanRus };
		var bulgars = gd.civilizations.FirstOrDefault(c => c.name == "Bulgars");
		var bul = new Player() { civilization = bulgars };

		// setup resources
		var noResources = new HashSet<Resource>(){};
		var horses = gd.Resources.FirstOrDefault(r => r.Key == "Horses");
		var iron = gd.Resources.FirstOrDefault(r => r.Key == "Iron");

		// setup techs
		var horsebackRiding = gd.techs.FirstOrDefault(t => t.Name == "Horseback Riding");
		var heavyCavalry = gd.techs.FirstOrDefault(t => t.Name == "Heavy Cavalry");
		var feudalism = gd.techs.FirstOrDefault(t => t.Name == "Feudalism");

		// setup some unit prototypes
		var horseman = protos.FirstOrDefault(p => p.name == "Horseman");
		var cataphract = protos.FirstOrDefault(p => p.name == "Cataphract");
		var knight = protos.FirstOrDefault(p => p.name == "Knight");

		#endregion

		#region Byzantines
		Tile constantinopleTile = new Tile(ID.None("tile"));
		City constantinople = new City(constantinopleTile, byz, "Constantinople", gd.ids.CreateID("Constantinople"));

		Assert.True(horseman.producibleBy.Contains(byzantines));
		Assert.True(cataphract.producibleBy.Contains(byzantines));
		Assert.False(knight.producibleBy.Contains(byzantines));

		byz.knownTechs.Add(horsebackRiding.id);

		Assert.True(horseman.CanProduce(constantinople, new HashSet<Resource>() { horses, iron }));
		Assert.False(knight.CanProduce(constantinople, new HashSet<Resource>() { horses, iron }));
		Assert.False(cataphract.CanProduce(constantinople, new HashSet<Resource>() { horses, iron }));

		byz.knownTechs.Add(heavyCavalry.id);
		Assert.True(horseman.CanProduce(constantinople, new HashSet<Resource>() { horses }));
		Assert.False(horseman.CanProduce(constantinople, new HashSet<Resource>() { horses, iron }));
		Assert.True(cataphract.CanProduce(constantinople, new HashSet<Resource>() { horses, iron }));
		Assert.True(horseman.GetProducibleUpgrade(constantinople, new HashSet<Resource>() { horses }) == null);
		Assert.True(horseman.GetProducibleUpgrade(constantinople, new HashSet<Resource>() { horses, iron }) == cataphract);

		byz.knownTechs.Add(feudalism.id);
		// Cataphract upgrades to Knight, not the other way around
		// even if the Knight becomes available later on in the game
		Assert.False(knight.CanProduce(constantinople, new HashSet<Resource>() { horses, iron }));
		Assert.True(cataphract.CanProduce(constantinople, new HashSet<Resource>() { horses, iron }));
		#endregion

		#region Bulgars
		Tile sofiaTile = new Tile(ID.None("tile"));
		City sofia = new City(sofiaTile, bul, "Sofia", gd.ids.CreateID("Sofia"));

		Assert.True(horseman.producibleBy.Contains(bulgars));
		Assert.False(cataphract.producibleBy.Contains(bulgars));
		Assert.False(knight.producibleBy.Contains(bulgars));

		bul.knownTechs.Add(horsebackRiding.id);

		Assert.True(horseman.CanProduce(sofia, new HashSet<Resource>() { horses, iron }));
		Assert.False(knight.CanProduce(sofia, new HashSet<Resource>() { horses, iron }));
		Assert.False(cataphract.CanProduce(sofia, new HashSet<Resource>() { horses, iron }));

		bul.knownTechs.Add(heavyCavalry.id);
		Assert.True(horseman.GetProducibleUpgrade(sofia, new HashSet<Resource>() { horses, iron }) == null);

		bul.knownTechs.Add(feudalism.id);
		Assert.True(horseman.GetProducibleUpgrade(sofia, new HashSet<Resource>() { horses, iron }) == null);
		#endregion
	}
}
