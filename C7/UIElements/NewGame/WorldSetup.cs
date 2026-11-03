using Godot;
using System;
using System.Linq;
using C7Engine;
using C7Engine.Lua;
using Serilog;
using C7GameData;
using C7GameData.Save;

[Tool]
public partial class WorldSetup : Control {
	private ILogger log = LogManager.ForContext<WorldSetup>();

	[Export] TextureRect background;

	[Export] Label pangaeaLabel;
	[Export] Label continentsLabel;
	[Export] Label archipelagoLabel;

	[Export] TextureButton pangaea60;
	[Export] TextureButton pangaea70;
	[Export] TextureButton pangaea80;

	[Export] TextureButton continents60;
	[Export] TextureButton continents70;
	[Export] TextureButton continents80;

	[Export] TextureButton archipelago60;
	[Export] TextureButton archipelago70;
	[Export] TextureButton archipelago80;

	[Export] TextureRect pangaea60Large;
	[Export] TextureRect pangaea70Large;
	[Export] TextureRect pangaea80Large;
	[Export] TextureRect continents60Large;
	[Export] TextureRect continents70Large;
	[Export] TextureRect continents80Large;
	[Export] TextureRect archipelago60Large;
	[Export] TextureRect archipelago70Large;
	[Export] TextureRect archipelago80Large;

	[Export] TextureButton arid;
	[Export] TextureButton normal;
	[Export] TextureButton wet;
	[Export] TextureButton cool;
	[Export] TextureButton temperate;
	[Export] TextureButton warm;
	[Export] TextureButton billion3;
	[Export] TextureButton billion4;
	[Export] TextureButton billion5;

	[Export] TextureRect aridLarge;
	[Export] TextureRect normalLarge;
	[Export] TextureRect wetLarge;
	[Export] TextureRect coolLarge;
	[Export] TextureRect temperateLarge;
	[Export] TextureRect warmLarge;
	[Export] TextureRect billion3Large;
	[Export] TextureRect billion4Large;
	[Export] TextureRect billion5Large;

	[Export] TextureButton confirm;
	[Export] TextureButton cancel;

	[Export] LineEdit seedInput;

	[Export] VBoxContainer worldSizeButtonsContainer;
	[Export] VBoxContainer barbActivityButtonsContainer;

	WorldCharacteristics.Landform landform = WorldCharacteristics.Landform.Pangaea;
	WorldCharacteristics.OceanCoverage ocean = WorldCharacteristics.OceanCoverage.Percent_70;
	WorldCharacteristics.Age age = WorldCharacteristics.Age.Billion_4;
	WorldCharacteristics.Temperature temp = WorldCharacteristics.Temperature.Temperate;
	WorldCharacteristics.Climate clim = WorldCharacteristics.Climate.Normal;

	private BarbarianActivity _barbarianActivity = BarbarianActivity.Roaming;

	private WorldSize _worldSize = WorldSize.Generic();
	private SaveGame _saveGame;

	private int GameSeed => int.Parse(seedInput.Text);

	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		background.Texture = TextureLoader.Load("world_setup.background");

		TextureLoader.SetButtonTextures(pangaea80, "world_setup.pangaea80");
		pangaea80.Pressed += () => {
			landform = WorldCharacteristics.Landform.Pangaea;
			ocean = WorldCharacteristics.OceanCoverage.Percent_80;
			SelectLandform();
		};

		TextureLoader.SetButtonTextures(pangaea70, "world_setup.pangaea70");
		pangaea70.Pressed += () => {
			landform = WorldCharacteristics.Landform.Pangaea;
			ocean = WorldCharacteristics.OceanCoverage.Percent_70;
			SelectLandform();
		};

		TextureLoader.SetButtonTextures(pangaea60, "world_setup.pangaea60");
		pangaea60.Pressed += () => {
			landform = WorldCharacteristics.Landform.Pangaea;
			ocean = WorldCharacteristics.OceanCoverage.Percent_60;
			SelectLandform();
		};

		TextureLoader.SetButtonTextures(continents80, "world_setup.continents80");
		continents80.Pressed += () => {
			landform = WorldCharacteristics.Landform.Continents;
			ocean = WorldCharacteristics.OceanCoverage.Percent_80;
			SelectLandform();
		};

		TextureLoader.SetButtonTextures(continents70, "world_setup.continents70");
		continents70.Pressed += () => {
			landform = WorldCharacteristics.Landform.Continents;
			ocean = WorldCharacteristics.OceanCoverage.Percent_70;
			SelectLandform();
		};

		TextureLoader.SetButtonTextures(continents60, "world_setup.continents60");
		continents60.Pressed += () => {
			landform = WorldCharacteristics.Landform.Continents;
			ocean = WorldCharacteristics.OceanCoverage.Percent_60;
			SelectLandform();
		};

		TextureLoader.SetButtonTextures(archipelago80, "world_setup.archipelago80");
		archipelago80.Pressed += () => {
			landform = WorldCharacteristics.Landform.Archipelago;
			ocean = WorldCharacteristics.OceanCoverage.Percent_80;
			SelectLandform();
		};

		TextureLoader.SetButtonTextures(archipelago70, "world_setup.archipelago70");
		archipelago70.Pressed += () => {
			landform = WorldCharacteristics.Landform.Archipelago;
			ocean = WorldCharacteristics.OceanCoverage.Percent_70;
			SelectLandform();
		};

		TextureLoader.SetButtonTextures(archipelago60, "world_setup.archipelago60");
		archipelago60.Pressed += () => {
			landform = WorldCharacteristics.Landform.Archipelago;
			ocean = WorldCharacteristics.OceanCoverage.Percent_60;
			SelectLandform();
		};

		TextureLoader.SetButtonTextures(arid, "world_setup.arid");
		arid.Pressed += () => {
			clim = WorldCharacteristics.Climate.Arid;
			SelectClimate();
		};

		TextureLoader.SetButtonTextures(normal, "world_setup.normal");
		normal.Pressed += () => {
			clim = WorldCharacteristics.Climate.Normal;
			SelectClimate();
		};

		TextureLoader.SetButtonTextures(wet, "world_setup.wet");
		wet.Pressed += () => {
			clim = WorldCharacteristics.Climate.Wet;
			SelectClimate();
		};

		TextureLoader.SetButtonTextures(warm, "world_setup.warm");
		warm.Pressed += () => {
			temp = WorldCharacteristics.Temperature.Warm;
			SelectTemperature();
		};

		TextureLoader.SetButtonTextures(temperate, "world_setup.temperate");
		temperate.Pressed += () => {
			temp = WorldCharacteristics.Temperature.Temperate;
			SelectTemperature();
		};

		TextureLoader.SetButtonTextures(cool, "world_setup.cool");
		cool.Pressed += () => {
			temp = WorldCharacteristics.Temperature.Cool;
			SelectTemperature();
		};

		TextureLoader.SetButtonTextures(billion3, "world_setup.billion3");
		billion3.Pressed += () => {
			age = WorldCharacteristics.Age.Billion_3;
			SelectAge();
		};

		TextureLoader.SetButtonTextures(billion4, "world_setup.billion4");
		billion4.Pressed += () => {
			age = WorldCharacteristics.Age.Billion_4;
			SelectAge();
		};

		TextureLoader.SetButtonTextures(billion5, "world_setup.billion5");
		billion5.Pressed += () => {
			age = WorldCharacteristics.Age.Billion_5;
			SelectAge();
		};

		TextureLoader.SetButtonTextures(confirm, "ui.confirm");
		confirm.Pressed += CreateGame;

		TextureLoader.SetButtonTextures(cancel, "ui.cancel");
		cancel.Pressed += BackToMainMenu;

		pangaea60Large.Texture = TextureLoader.Load("world_setup.large.pangaea60");
		pangaea70Large.Texture = TextureLoader.Load("world_setup.large.pangaea70");
		pangaea80Large.Texture = TextureLoader.Load("world_setup.large.pangaea80");
		continents60Large.Texture = TextureLoader.Load("world_setup.large.continents60");
		continents70Large.Texture = TextureLoader.Load("world_setup.large.continents70");
		continents80Large.Texture = TextureLoader.Load("world_setup.large.continents80");
		archipelago60Large.Texture = TextureLoader.Load("world_setup.large.archipelago60");
		archipelago70Large.Texture = TextureLoader.Load("world_setup.large.archipelago70");
		archipelago80Large.Texture = TextureLoader.Load("world_setup.large.archipelago80");

		aridLarge.Texture = TextureLoader.Load("world_setup.large.arid");
		normalLarge.Texture = TextureLoader.Load("world_setup.large.normal");
		wetLarge.Texture = TextureLoader.Load("world_setup.large.wet");
		coolLarge.Texture = TextureLoader.Load("world_setup.large.cool");
		temperateLarge.Texture = TextureLoader.Load("world_setup.large.temperate");
		warmLarge.Texture = TextureLoader.Load("world_setup.large.warm");
		billion3Large.Texture = TextureLoader.Load("world_setup.large.billion3");
		billion4Large.Texture = TextureLoader.Load("world_setup.large.billion4");
		billion5Large.Texture = TextureLoader.Load("world_setup.large.billion5");

		// Restore the last game's world settings.
		landform = GameSetup.LandformFromSettings();
		ocean = GameSetup.OceanCoverageFromSettings();
		SelectLandform();

		clim = GameSetup.ClimateFromSettings();
		SelectClimate();

		temp = GameSetup.TemperatureFromSettings();
		SelectTemperature();

		age = GameSetup.AgeFromSettings();
		SelectAge();

		var game = GameMode.Load(GamePaths.GameModesDir, GamePaths.GameMode);
		_saveGame = game.GetSave();

		InitBarbarianActivityOptions();
		InitMapSizes();
	}

	private void InitBarbarianActivityOptions() {
		var barbRandom = new Random();
		BarbarianActivity barbDefault = GameSetup.BarbarianActivityFromSettings();
		var barbOptions = Enum.GetValues<BarbarianActivity>().OrderBy(x => x).ToList();
		_barbarianActivity = barbDefault;

		var barbActivityButtonGroup = new ButtonGroup() { ResourceName = "BarbActivityButtonGroup" };
		var randomSizeButton = new Civ3MenuButton
		{
			Text = "Random",
			textPosition = Civ3MenuButton.TextPosition.TextRightOfIcon,
			FontSize = 0,
			ButtonGroup = barbActivityButtonGroup,
			ToggleMode = true
		};
		randomSizeButton.Pressed += () => {
			_barbarianActivity = barbOptions[barbRandom.Next(barbOptions.Count)];
		};

		// Dynamically create a new button for each barbarian activity option 
		foreach (var ba in barbOptions) {
			var barbActivityButton = new Civ3MenuButton
			{
				Text = ba.ToString("G"),
				textPosition = Civ3MenuButton.TextPosition.TextRightOfIcon,
				FontSize = 0,
				ButtonGroup = barbActivityButtonGroup,
				ToggleMode = true,
				ButtonPressed = ba == barbDefault
			};
			barbActivityButton.Pressed += () => _barbarianActivity = ba;
			barbActivityButtonsContainer.AddChild(barbActivityButton);
		}

		// Append random as last in the list 
		barbActivityButtonsContainer.AddChild(randomSizeButton);
	}

	private void InitMapSizes() {
		var sizeRandom = new Random();
		WorldSize selectedSize = GameSetup.WorldSizeFromSettings(_saveGame.WorldSizes);
		_worldSize = selectedSize;

		var worldSizeButtonGroup = new ButtonGroup() { ResourceName = "WorldSizeButtonGroup" };
		var randomSizeButton = new Civ3MenuButton
		{
			Text = "Random",
			textPosition = Civ3MenuButton.TextPosition.TextRightOfIcon,
			FontSize = 0,
			ButtonGroup = worldSizeButtonGroup,
			ToggleMode = true
		};

		randomSizeButton.Pressed += () => {
			var wss = _saveGame?.WorldSizes ?? [];
			_worldSize = wss.Count > 0 ? wss[sizeRandom.Next(wss.Count)] : WorldSize.Generic();
		};

		try {
			// Dynamically create a new button for each world size in the game
			foreach (var ws in _saveGame.WorldSizes) {
				var worldSizeButton = new Civ3MenuButton
				{
					Text = ws.name,
					textPosition = Civ3MenuButton.TextPosition.TextRightOfIcon,
					FontSize = 0,
					ButtonGroup = worldSizeButtonGroup,
					ToggleMode = true,
					ButtonPressed = ws == selectedSize
				};
				worldSizeButton.Pressed += () => _worldSize = ws;
				worldSizeButtonsContainer.AddChild(worldSizeButton);
			}

			// Append random as last in the list 
			worldSizeButtonsContainer.AddChild(randomSizeButton);
		} catch (Exception ex) {
			log.Warning(ex, "Failed to load map sizes from game mode.");
		}
	}

	private void SelectLandform() {
		(TextureButton button, TextureRect large) = (landform, ocean) switch {
			(WorldCharacteristics.Landform.Pangaea, WorldCharacteristics.OceanCoverage.Percent_60) => (pangaea60, pangaea60Large),
			(WorldCharacteristics.Landform.Pangaea, WorldCharacteristics.OceanCoverage.Percent_70) => (pangaea70, pangaea70Large),
			(WorldCharacteristics.Landform.Pangaea, _) => (pangaea80, pangaea80Large),
			(WorldCharacteristics.Landform.Continents, WorldCharacteristics.OceanCoverage.Percent_60) => (continents60, continents60Large),
			(WorldCharacteristics.Landform.Continents, WorldCharacteristics.OceanCoverage.Percent_70) => (continents70, continents70Large),
			(WorldCharacteristics.Landform.Continents, _) => (continents80, continents80Large),
			(_, WorldCharacteristics.OceanCoverage.Percent_60) => (archipelago60, archipelago60Large),
			(_, WorldCharacteristics.OceanCoverage.Percent_70) => (archipelago70, archipelago70Large),
			_ => (archipelago80, archipelago80Large),
		};

		Label label = landform switch {
			WorldCharacteristics.Landform.Pangaea => pangaeaLabel,
			WorldCharacteristics.Landform.Continents => continentsLabel,
			_ => archipelagoLabel
		};

		ResetLandformGraphics();
		large.Visible = true;
		button.ButtonPressed = true;
		label.Text = $"{landform} ({(int)ocean}% water)";
	}

	private void SelectClimate() {
		(TextureButton button, TextureRect large) = clim switch {
			WorldCharacteristics.Climate.Arid => (arid, aridLarge),
			WorldCharacteristics.Climate.Wet => (wet, wetLarge),
			_ => (normal, normalLarge),
		};

		ResetClimateGraphics();
		large.Visible = true;
		button.ButtonPressed = true;
	}

	private void SelectTemperature() {
		(TextureButton button, TextureRect large) = temp switch {
			WorldCharacteristics.Temperature.Cool => (cool, coolLarge),
			WorldCharacteristics.Temperature.Warm => (warm, warmLarge),
			_ => (temperate, temperateLarge),
		};

		ResetTemperatureGraphics();
		large.Visible = true;
		button.ButtonPressed = true;
	}

	private void SelectAge() {
		(TextureButton button, TextureRect large) = age switch {
			WorldCharacteristics.Age.Billion_3 => (billion3, billion3Large),
			WorldCharacteristics.Age.Billion_5 => (billion5, billion5Large),
			_ => (billion4, billion4Large),
		};

		ResetAgeGraphics();
		large.Visible = true;
		button.ButtonPressed = true;
	}

	private void ResetLandformGraphics() {
		pangaea60Large.Visible = false;
		pangaea70Large.Visible = false;
		pangaea80Large.Visible = false;
		continents60Large.Visible = false;
		continents70Large.Visible = false;
		continents80Large.Visible = false;
		archipelago60Large.Visible = false;
		archipelago70Large.Visible = false;
		archipelago80Large.Visible = false;

		pangaeaLabel.Text = "Pangaea";
		continentsLabel.Text = "Continents";
		archipelagoLabel.Text = "Archipelago";
	}

	private void ResetClimateGraphics() {
		aridLarge.Visible = false;
		normalLarge.Visible = false;
		wetLarge.Visible = false;
	}

	private void ResetTemperatureGraphics() {
		coolLarge.Visible = false;
		temperateLarge.Visible = false;
		warmLarge.Visible = false;
	}

	private void ResetAgeGraphics() {
		billion3Large.Visible = false;
		billion4Large.Visible = false;
		billion5Large.Visible = false;
	}

	private void CreateGame() {
		GlobalSingleton Global = GetNode<GlobalSingleton>("/root/GlobalSingleton");
		Global.ResetLoadGameFields();

		Global.WorldCharacteristics = new WorldCharacteristics(_saveGame) {
			landform = landform,
			oceanCoverage = ocean,
			age = age,
			climate = clim,
			temperature = temp,
			worldSize = _worldSize,
			barbarianActivity = _barbarianActivity,
			mapSeed = GameSeed,
		};

		Global.SaveGame = _saveGame;

		GetTree().ChangeSceneToFile("res://UIElements/NewGame/player_setup.tscn");
	}

	private void BackToMainMenu() {
		GetTree().ChangeSceneToFile("res://UIElements/MainMenu/main_menu.tscn");
	}
}
