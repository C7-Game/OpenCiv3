using System.Collections.Generic;
using Godot;
using C7GameData;
using C7Engine;
using System;
using System.Linq;

public partial class RightClickMenu : VBoxContainer {
	protected Game game;
	protected Vector2 position;

	protected RightClickMenu(Game game) : base() {
		this.game = game;

		// Set theme for menu node. TODO: This should be made moddable. I noticed in the Godot docs something about loading themes from files
		// but didn't look into how it works, but that's probably what we'll want to do.
		Color black = Color.Color8(0, 0, 0, 255);
		var theme = new Theme();
		theme.SetConstant("separation", "VBoxContainer", 0);
		theme.SetColor("font_color", "Button", black);
		theme.SetColor("font_hover_color", "Button", black);
		theme.SetColor("font_pressed_color", "Button", black);
		theme.SetColor("font_focus_color", "Button", black);
		theme.SetStylebox("normal", "Button", GetItemStyleBox(Color.Color8(255, 247, 222, 255)));
		theme.SetStylebox("hover", "Button", GetItemStyleBox(Color.Color8(255, 189, 107, 255)));
		theme.SetStylebox("pressed", "Button", GetItemStyleBox(Color.Color8(140, 200, 200, 255)));
		this.Theme = theme;

		this.Hide();

		// Add the menu as a child of "CanvasLayer" to ensure it's drawn overtop of the game map. "CanvasLayer" should have been named
		// something like "UILayer" since it's the layer that contains all UI elements. "CanvasLayer" is actually its type.
		game.GetNode("CanvasLayer").AddChild(this);
	}

	public void Open(Vector2 position) {
		// Must show the container first in order to update its RectSize
		this.Show();

		// Move "position" if the menu would extend past the right or bottom edges of the screen
		Vector2 offScreen = position + this.Size - DisplayServer.WindowGetSize();
		if (offScreen.X > 0) {
			position.X = Mathf.Max(0, position.X - offScreen.X);
		}
		if (offScreen.Y > 0) {
			position.Y = Mathf.Max(0, position.Y - offScreen.Y);
		}
		this.SetPosition(position);

		// Godot 4.2.1 does not have an accessor for the position, so store it ourselves.
		this.position = position;
	}

	public void CloseAndDelete() {
		this.QueueFree();
	}

	private static StyleBoxFlat GetItemStyleBox(Color color) {
		return new StyleBoxFlat() {
			BgColor = color,
			ContentMarginLeft = 10f,
			ContentMarginTop = 0f,
			ContentMarginRight = 10f,
			ContentMarginBottom = 0f
		};
	}

	public Button AddItem(string text, System.Action action, Texture2D icon = null) {
		Button button = new Button();
		button.Text = text;
		if (icon != null) {
			button.Icon = icon;
		}
		button.Alignment = HorizontalAlignment.Left;
		if (action != null) {
			button.Pressed += action;
		}
		this.AddChild(button);
		return button;
	}

	protected void AddTreeSeparator() {
		var background = new ColorRect();
		background.Color = Color.Color8(255, 247, 222, 255);
		background.CustomMinimumSize = new Vector2(0, 12);
		background.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		background.MouseFilter = Control.MouseFilterEnum.Stop;

		var margin = new MarginContainer();
		margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		margin.MouseFilter = MouseFilterEnum.Stop;

		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_top", 5);
		margin.AddThemeConstantOverride("margin_bottom", 5);

		var line = new ColorRect();
		line.Color = Color.Color8(165, 165, 165, 255);
		line.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		line.CustomMinimumSize = new Vector2(0, 2);
		line.MouseFilter = Control.MouseFilterEnum.Stop;

		margin.AddChild(line);
		background.AddChild(margin);

		this.AddChild(background);
	}

	public void RemoveAll() {
		foreach (Node child in this.GetChildren()) {
			child.QueueFree();
		}
	}

	public override void _Input(InputEvent @event) {
		bool mouseOverMenu = new Rect2(Vector2.Zero, this.Size).HasPoint(this.GetLocalMousePosition());
		bool escapeKeyWasPressed = (@event is InputEventKey keyEvent) && keyEvent.Pressed && keyEvent.Keycode == Godot.Key.Escape;
		bool mouseClickedOutsideMenu = (@event is InputEventMouseButton mouseButtonEvent) && mouseButtonEvent.IsPressed() && !mouseOverMenu;

		if (escapeKeyWasPressed || mouseClickedOutsideMenu) {
			this.AcceptEvent(); // Prevents other controls from receiving this event
			CloseAndDelete();
			// Eat all events other than mouse events while the cursor is over the menu. We want the menu to grab all input while it's open but we
			// must make sure not to block mouse events from reaching its child buttons. (This had me confused for a while since the Godot docs
			// say that events reach children before their parents, but the catch is that there are three phases of input processing. The "input"
			// phase, this function, then "gui input", and finally "unhandled input". If a control eats an event during the "input" phase it won't
			// proceed to the "gui input" phase where buttons actually respond to it.)
		} else if (!((@event is InputEventMouse) && mouseOverMenu)) {
			this.AcceptEvent();
		}
	}
}

public partial class RightClickTileMenu : RightClickMenu {
	public RightClickTileMenu(Game game, Tile tile) : base(game) {
		ResetItems(tile);
	}

	private bool isUnitFortified(MapUnit unit, Dictionary<ID, bool> uiStates) {
		if (uiStates is null || !uiStates.ContainsKey(unit.id)) {
			return unit.isFortified;
		}
		return uiStates[unit.id];
	}

	private bool isUnitLoadedOnTransport(MapUnit unit) => unit.IsLoaded();

	private string getUnitAction(MapUnit unit, bool isFortified) {
		if (unit.owner == game.controller) {
			return isFortified ? "Wake" : "Activate";
		}
		return "Contact";
	}

	private static StyleBoxFlat AltItemStyleBox(Color color) {
		return new StyleBoxFlat() {
			BgColor = color,
			ContentMarginLeft = 20f,
			ContentMarginTop = 2f,
			ContentMarginRight = 4f,
			ContentMarginBottom = 2f
		};
	}

	// uiUpdatedUnitStates maps unit guid to a boolean that is true if they were fortified
	// and false if they were selected in the previous action. This is to update the UI
	// since the actions update the engine asynchronously and otherwise the UI may not
	// reflect these changes immediately.
	public void ResetItems(Tile tile, Dictionary<ID, bool> uiUpdatedUnitStates = null) {
		RemoveAll();

		AddItem($"Terrain Info", () => {
			game.ShowTileInfo(tile);
			CloseAndDelete();
		});

		AddTreeSeparator();

		bool observerMode = false;
		EngineStorage.ReadGameData((GameData gameData) => {
			observerMode = gameData.observerMode;
		});

		int fortifiedCount = 0;
		List<MapUnit> playerUnits = tile.unitsOnTile.FindAll(unit => unit.owner.id == game.controller.id || observerMode);
		List<MapUnit> nonPlayerUnits = tile.unitsOnTile.FindAll(unit => unit.owner.id != game.controller.id && !observerMode);

		// Sort by transport group
		playerUnits = playerUnits
			.GroupBy(u => u.CanTransport() ? u.id : u.loadedOnUnitId ?? ID.None("Other"))
			.SelectMany(g => g.OrderBy(u => u.CanTransport() ? int.MinValue : 0))
			.ToList();

		foreach (MapUnit unit in playerUnits) {
			bool isFortified = isUnitFortified(unit, uiUpdatedUnitStates);
			fortifiedCount += isFortified ? 1 : 0;
			string actionName = getUnitAction(unit, isFortified);
			var menuItem = AddItem($"{actionName} {unit.Describe()}", () => SelectUnit(unit.id));

			if (isUnitLoadedOnTransport(unit))
				ApplyAltItemOverrides(menuItem);
		}
		int unfortifiedCount = playerUnits.Count - fortifiedCount;

		if (fortifiedCount > 1) {
			AddItem($"Wake All ({fortifiedCount} units)", () => ForAll(tile.XCoordinate, tile.YCoordinate, false));
		}
		if (unfortifiedCount > 1) {
			AddItem($"Fortify All ({unfortifiedCount} units)", () => ForAll(tile.XCoordinate, tile.YCoordinate, true));
		}
		if (tile.cityAtTile?.owner == game.controller) {
			AddTreeSeparator();
			AddItem($"Zoom to {tile.cityAtTile.name}", () => {
				this.CloseAndDelete();
				EngineStorage.ReadGameData((GameData gameData) => {
					game.ShowCityScreenForCity(gameData, tile.cityAtTile);
				});
			});
			// TODO: Rename city
			AddTreeSeparator();
			AddItem("Change Production", () => {
				// Close the first menu before opening the second menu.
				this.CloseAndDelete();
				new RightClickChooseProductionMenu(game, tile.cityAtTile).Open(this.position);
			});
			AddItem("Hurry Production", () => {
				this.CloseAndDelete();
				EngineStorage.ReadGameData((GameData gameData) => {
					City.HurryProductionDetails details = tile.cityAtTile.GetHurryProductionDetails();
					new MsgDisplayHurryProductionPopup(tile.cityAtTile, details).send();
				});
			});
			// TODO: Contact Governor
			// TODO: Set Rally Point
			// TODO: Set Continental Rally Point
			// TODO: Clear Continental Rally Point

			AddItem("Abandon City", () => {
				this.CloseAndDelete();
				EngineStorage.ReadGameData((GameData gameData) => {
					// TODO: perhaps add some conditions as to when we are able to do that,
					// and if not add a popup message to explain why not (eg last city)
					new MsgDisplayAbandonCityPopup(tile.cityAtTile).send();
				});
			});

			// AddTreeSeparator();
			// TODO: Wikipedia links

		}

		// If we're looking at an enemy tile, then the behavior depends on whether the units
		// are in a city. We can see the full list of units outside of a city, but in a city
		// we can only see the top defender.
		if (nonPlayerUnits.Count > 0) {
			Action contactCiv = () => {
				this.CloseAndDelete();
				game.controller.EnsureRelationshipExists(nonPlayerUnits[0].owner);
				game.OnDiplomacySelected(new ParameterWrapper<ID>(nonPlayerUnits[0].owner.id));
			};

			if (tile.cityAtTile == null) {
				foreach (MapUnit unit in nonPlayerUnits) {
					AddItem($"{unit.owner.civilization.noun} {unit.Describe()}", null);
				}
			} else {
				// TODO: This isn't necessarily the top unit, get that code to an accessible
				// location and then use it here.
				MapUnit unit = nonPlayerUnits[0];
				AddItem($"{unit.owner.civilization.noun} {unit.Describe()}", null);
			}

			if (!nonPlayerUnits[0].owner.isBarbarians)
				AddItem($"Contact {nonPlayerUnits[0].owner.civilization.name}", contactCiv);
		}
	}

	private static void ApplyAltItemOverrides(Button menuItem) {
		var grey = Color.Color8(64, 64, 64, 255);
		menuItem.AddThemeColorOverride("font_color", grey);
		menuItem.AddThemeColorOverride("font_hover_color", grey);
		menuItem.AddThemeColorOverride("font_pressed_color", grey);
		menuItem.AddThemeColorOverride("font_focus_color", grey);
		menuItem.AddThemeStyleboxOverride("normal", AltItemStyleBox(Color.Color8(255, 247, 222, 255)));
		menuItem.AddThemeStyleboxOverride("hover", AltItemStyleBox(Color.Color8(255, 189, 107, 255)));
		menuItem.AddThemeStyleboxOverride("pressed", AltItemStyleBox(Color.Color8(140, 200, 200, 255)));
	}

	public void SelectUnit(ID id) {
		EngineStorage.ReadGameData((GameData gameData) => {
			MapUnit toSelect = gameData.mapUnits.Find(u => u.id == id);

			if (toSelect != null && toSelect.owner == game.controller) {
				game.SelectUnit(toSelect);

				new MsgSetFortification(toSelect.id, false).send();
				ResetItems(toSelect.location, new Dictionary<ID, bool>() { { toSelect.id, false } });
			}
		});
		if (!Input.IsKeyPressed(Godot.Key.Shift)) {
			CloseAndDelete();
		}
	}

	public void ForAll(int tileX, int tileY, bool isFortify) {
		EngineStorage.ReadGameData((GameData gameData) => {
			bool hasSelectedUnit = false;
			Tile tile = gameData.map.tileAt(tileX, tileY);
			Dictionary<ID, bool> modified = new Dictionary<ID, bool>();
			foreach (MapUnit unit in tile.unitsOnTile) {
				if (unit.isFortified != isFortify) {
					modified[unit.id] = isFortify;
					new MsgSetFortification(unit.id, isFortify).send();

					if (!hasSelectedUnit && !isFortify) {
						bool canMove = game.unitSelector.SetSelectedUnit(unit);
						if (!canMove) {
							new MsgShowTemporaryPopup(unit.owner.id, "This unit has already moved.", tile).send();
						}
					}
				}
			}

			ResetItems(tile, modified);
		});
		if (!Input.IsKeyPressed(Godot.Key.Shift)) {
			CloseAndDelete();
		}
	}
}

// A right click menu for the player's city when there are no units.
public partial class RightClickCityMenu : RightClickMenu {
	public RightClickCityMenu(Game game, Tile tile) : base(game) {
		ResetItems(tile);
	}

	public void ResetItems(Tile tile) {
		RemoveAll();

		// TODO: maybe look into unifying this with the other right click menu (when there are units present)
		// I don't like this much duplication
		AddItem($"Terrain Info", () => {
			game.ShowTileInfo(tile);
			CloseAndDelete();
		});

		AddTreeSeparator();

		if (tile.cityAtTile?.owner == game.controller) {
			AddItem($"Zoom to {tile.cityAtTile.name}", () => {
				this.CloseAndDelete();
				EngineStorage.ReadGameData((GameData gameData) => {
					game.ShowCityScreenForCity(gameData, tile.cityAtTile);
				});
			});
			AddTreeSeparator();
			AddItem("Change Production", () => {
				// Close the first menu before opening the second menu.
				this.CloseAndDelete();
				new RightClickChooseProductionMenu(game, tile.cityAtTile).Open(this.position);
			});
			AddItem("Hurry Production", () => {
				this.CloseAndDelete();
				EngineStorage.ReadGameData((GameData gameData) => {
					City.HurryProductionDetails details = tile.cityAtTile.GetHurryProductionDetails();
					new MsgDisplayHurryProductionPopup(tile.cityAtTile, details).send();
				});
			});
			AddItem("Abandon City", () => {
				this.CloseAndDelete();
				EngineStorage.ReadGameData((GameData gameData) => {
					// TODO: perhaps add some conditions as to when we are able to do that,
					// and if not add a popup message to explain why not (eg last city)
					new MsgDisplayAbandonCityPopup(tile.cityAtTile).send();
				});
			});
		}
	}
}

public partial class RightClickChooseProductionMenu : RightClickMenu {
	private ID cityID;

	public static ImageTexture GetProducibleIcon(IProducible producible, Player player) {
		if (producible is UnitPrototype proto) {
			return TextureLoader.Load("unit_icons", new ItemContext(proto, player), useCache: true);
		} else if (producible is Building b) {
			return TextureLoader.Load("building_icons.small", b, useCache: true);
		} else if (producible is Inflow inflow) {
			return TextureLoader.Load("building_icons.small", inflow, useCache: true);
		} else {
			return null;
		}
	}

	public RightClickChooseProductionMenu(Game game, City city) : base(game) {
		cityID = city.id;
		EngineStorage.ReadGameData((GameData gameData) => {
			foreach (IProducible option in city.ListProductionOptions(gameData)) {
				int buildTime = city.TurnsToProduce(option);
				AddItem($"{option.name} ({buildTime} turns)", () => ChooseProduction(option.name), GetProducibleIcon(option, city.owner));
			}
		});
	}

	public void ChooseProduction(string producibleName) {
		new MsgChooseProduction(cityID, producibleName).send();
		CloseAndDelete();
	}
}
