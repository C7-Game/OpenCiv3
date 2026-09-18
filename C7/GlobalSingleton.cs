using System;
using C7.Map;
using Godot;
using C7Engine;
using C7GameData.Save;
using C7Engine.Lua;
using MoonSharp.Interpreter;

/****
	Need to pass values from one scene to another, particularly when loading
	a game in main menu. This script is set to auto load in project settings.
	See https://docs.godotengine.org/en/stable/getting_started/step_by_step/singletons_autoload.html
****/
public partial class GlobalSingleton : Node {
	public GameMode GameMode;

	// Will have main menu file picker set this and Game.cs pass it to C7Engine.createGame
	// which then should blank it again to prevent reloading same if going back to main menu
	// and back to game
	public string LoadGamePath;

	// Generated game data used when starting a new game
	public SaveGame SaveGame;

	// The characteristics of the world to generate. This exists in the singleton
	// to allow the world setup screen to pass the information to the player
	// setup screen, which is what actually kicks off the world generation.
	public WorldCharacteristics WorldCharacteristics;

	public GlobalSingleton() {
		if (C7Settings.UseStandaloneMode()) {
			ActivateGameMode(GamePaths.standalone);
		} else {
			ActivateGameMode(GamePaths.basic);
		}
	}

	public void ResetLoadGameFields() {
		LoadGamePath = null;
		SaveGame = null;
	}

	public void ActivateGameMode(GameMode.Config config) {
		// Ensure we clear out our image caches, as scenarios and games will
		// use the same filenames but have different content for them.
		Util.ClearCaches();

		GameMode = GameMode.Load(GamePaths.GameModesDir, config);

		LuaTypeRegistrations();

		var (script, textureConfig) = GameMode.textures;
		TextureLoader.SetConfig(script, textureConfig);

		var (audioLua, audioConfig) = GameMode.audio;
		AudioLoader.SetConfig(audioLua, audioConfig);

		if (config.addonPaths.Contains("standalone")) {
			C7Settings.SetValue("locations", "useStandaloneMode", "true");
		} else {
			C7Settings.SetValue("locations", "useStandaloneMode", "false");
		}

		C7Settings.SaveSettings();
	}

	private void LuaTypeRegistrations() {
		// We need to register the "Type" type to be able to inspect
		// the types of C# objects in the Lua code
		UserData.RegisterType<Type>();

		// Note: classes in the C7GameData namespace are already registered as part of GameModeLoader logic
		UserData.RegisterType<CityGraphicsDetails>();
		UserData.RegisterType<PopHead.TextureKey>();
		UserData.RegisterType<BorderLayer.TextureDetails>();

		// Note, we register all of AdvisorHeader rather than just
		// AdvisorHead.AdvisorGraphicsDetails because we access the nums
		// in the class as well.
		UserData.RegisterType<AdvisorHead>();
	}

	public void ToggleStandaloneMode() {
		GameMode.Config newConfig = C7Settings.UseStandaloneMode() ? GamePaths.basic : GamePaths.standalone;

		ActivateGameMode(newConfig);
	}
}
