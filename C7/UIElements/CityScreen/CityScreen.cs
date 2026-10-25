using Godot;
using System;
using Serilog;
using System.Collections.Generic;
using C7GameData;
using C7.Map;
using C7.Textures;
using C7Engine;
using C7Engine.AI;


// Handles the city screen, where citizens can be assigned and other details of
// the city can bee seen.
[Tool]
public partial class CityScreen : Control {
	private ILogger log = LogManager.ForContext<CityScreen>();
	public TileAssignmentLayer tileAssignmentLayer;
	public MapView mapView;
	public List<CitizenType> citizenTypes;
	private List<TextureButton> popHeads = new();
	private List<TextureRect> popHeadEffects = new();

	private const int POP_HEAD_OFFSET_Y = 433;
	private const string LUXURIES = "luxuries";
	private const string TAXES = "taxes";
	private const string RESEARCH = "research";
	private const string CORRUPTION = "corruption";
	private const string CONSTRUCTION = "construction";

	[Export] private TextureRect background;
	[Export] private VBoxContainer existingBuildings;
	[Export] private Label culturePerTurn;
	[Export] private Label totalCulture;
	[Export] private Label cityName;

	[Export] private TextureButton productionButton;
	[Export] private TextureButton close;
	[Export] private TextureButton previousCity;
	[Export] private TextureButton nextCity;

	[Export] private ProductionMenu productionMenu;

	[Export] private HBoxContainer strategicResources;
	[Export] private VBoxContainer luxuriesContainer;

	[Export] Label productionLabel;
	[Export] Label completeInLabel;
	[Export] Label growthInLabel;
	[Export] Label foodLabel;
	[Export] Label granaryLabel;
	[Export] GridContainer shieldsInBoxContainer;
	[Export] GridContainer foodInBoxContainer;
	[Export] GridContainer foodInGranaryContainer;

	[Export] Control shieldRowContainer;
	[Export] Control foodRowContainer;

	Theme yieldDetailsFontTheme = new();
	FontFile yieldDetailsFont = new();

	private Label foodDetails;
	private Label productionDetails;
	private Label commerceTaxesDetails;
	private Label commerceScienceDetails;
	private Label commerceHappinessDetails;

	private ImageTexture shieldTexture;
	private ImageTexture emptyShieldTexture;
	private ImageTexture corruptShieldTexture;
	private ImageTexture foodTexture;
	private ImageTexture emptyFoodTexture;
	private ImageTexture noFoodTexture;
	private ImageTexture eatenFoodTexture;

	private ImageTexture smileyFaceTexture;
	private ImageTexture goodShieldTexture;
	private ImageTexture beakerTexture;
	private ImageTexture goodGoldTexture;
	private ImageTexture wastedGoldTexture;

	private Dictionary<string, ImageTexture> effectIcons = new();

	private City lastInspectedCity; // the last city a player zoomed in

	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		background.Texture = TextureLoader.Load("city_screen.background");

		// The close button.
		TextureLoader.SetButtonTextures(close, "city_screen.buttons.close");
		close.Pressed += Hide;

		TextureLoader.SetButtonTextures(previousCity, "city_screen.buttons.previous");
		previousCity.Pressed += SwitchToPreviousCity;

		TextureLoader.SetButtonTextures(nextCity, "city_screen.buttons.next");
		nextCity.Pressed += SwitchToNextCity;

		// Load the font we'll use for the details.
		//
		// We skip the cache so that we can change the size without affecting other
		// code using the same font.
		yieldDetailsFont = ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-Regular.ttf", null, ResourceLoader.CacheMode.Ignore);
		yieldDetailsFont.FixedSize = 20;

		yieldDetailsFontTheme.DefaultFont = yieldDetailsFont;
		yieldDetailsFontTheme.SetColor("font_color", "Label", Colors.Black);
		yieldDetailsFontTheme.SetFontSize("font_size", "Label", 20);

		foodDetails = new Label() {
			OffsetLeft = 290,
			OffsetTop = 567,
			Theme = yieldDetailsFontTheme,
		};
		background.AddChild(foodDetails);

		productionDetails = new Label() {
			OffsetLeft = 290,
			OffsetTop = 520,
			Theme = yieldDetailsFontTheme,
		};
		background.AddChild(productionDetails);

		commerceTaxesDetails = new Label() {
			OffsetLeft = 290,
			OffsetTop = 620,
			Theme = yieldDetailsFontTheme,
		};
		background.AddChild(commerceTaxesDetails);

		commerceScienceDetails = new Label() {
			OffsetLeft = 290,
			OffsetTop = 653,
			Theme = yieldDetailsFontTheme,
		};
		background.AddChild(commerceScienceDetails);

		commerceHappinessDetails = new Label() {
			OffsetLeft = 290,
			OffsetTop = 685,
			Theme = yieldDetailsFontTheme,
		};
		background.AddChild(commerceHappinessDetails);

		TextureLoader.SetButtonTextures(productionButton, "city_screen.buttons.production");
		productionButton.Pressed += () => { this.productionMenu.Visible = !this.productionMenu.Visible; };

		shieldTexture = TextureLoader.Load("icons.good_shield");
		emptyShieldTexture = TextureLoader.Load("icons.empty_shield");
		corruptShieldTexture = TextureLoader.Load("icons.wasted_shield");
		foodTexture = TextureLoader.Load("icons.full_food");
		emptyFoodTexture = TextureLoader.Load("icons.empty_food");
		noFoodTexture = TextureLoader.Load("icons.no_food");
		eatenFoodTexture = TextureLoader.Load("icons.eaten_food");
		// effects
		smileyFaceTexture = TextureLoader.Load("icons.effect_smiley_face");
		goodGoldTexture = TextureLoader.Load("icons.effect_good_gold");
		beakerTexture = TextureLoader.Load("icons.effect_beaker");
		wastedGoldTexture = TextureLoader.Load("icons.effect_wasted_gold");
		goodShieldTexture = TextureLoader.Load("icons.effect_shield");

		effectIcons = new() {
			{LUXURIES,  smileyFaceTexture},
			{TAXES,  goodGoldTexture},
			{RESEARCH,  beakerTexture},
			{CORRUPTION,  wastedGoldTexture},
			{CONSTRUCTION,  goodShieldTexture}
		};

		RenderShieldBox(shieldCost: 30, shieldsInBox: 15);
		RenderShieldRow(goodShields: 10, corruptShields: 3);
		RenderFoodBox(foodNeededToGrow: 20, foodStored: 10, foodLostPerTurn: 2, hasGranary: true);
		RenderFoodRow(foodEatenPerTurn: 3, foodSurplus: 1);

		Hidden += OnExit;
	}

	public override void _UnhandledInput(InputEvent @event) {
		// Only capture mouse events if we're visible.
		if (!this.Visible) {
			return;
		}

		// If we left clicked on a tile, handle reassigning a citizen accordingly.
		if (@event is InputEventMouseButton eventMouseButton) {
			if (eventMouseButton.ButtonIndex == MouseButton.Left) {
				GetViewport().SetInputAsHandled();
				if (eventMouseButton.IsPressed()) {
					EngineStorage.ReadGameData((GameData gameData) => {
						if (productionMenu != null) {
							productionMenu.Visible = false;
						}

						Tile tile = mapView.tileOnScreenAt(gameData.map, eventMouseButton.Position);
						if (tile != null) {
							HandleReassignment(gameData, tile);

							// Recalculate moods after changing tile assignments.
							tileAssignmentLayer.city.RecalculateCitizenMoods(gameData);

							RenderPopHeads(tileAssignmentLayer.city);
							RenderFoodDetails(tileAssignmentLayer.city);
							RenderCommerceDetails(tileAssignmentLayer.city);
							RenderProductionDetails(gameData, tileAssignmentLayer.city);
						}
					});
				}
			}
		}
	}

	private void HandleReassignment(GameData gameData, Tile tile) {
		City city = tileAssignmentLayer.city;

		// We only support clicking on workable tiles or the city center.
		if (!city.GetWorkableTiles().Contains(tile) && tile != city.location) {
			return;
		}

		// We can't assign citizens to other cities.
		if (tile.cityAtTile != null && tile.cityAtTile != city) {
			return;
		}

		// We can't assign citizens to tiles worked by other cities.
		if (tile.personWorkingTile != null && tile.personWorkingTile.city != city) {
			return;
		}

		// If we're already working a tile and click on it, turn the citizen
		// into a specialist.
		if (tile.personWorkingTile != null && tile.personWorkingTile.city == city) {
			CityResident resident = tile.personWorkingTile;
			tile.personWorkingTile = null;
			resident.tileWorked = Tile.NONE;
			resident.citizenType = city.owner.GetKnownSpecialists(gameData)[0];
			return;
		}

		// We've clicked on an unworked tile, move the "worst" citizen to that
		// tile.
		if (tile.cityAtTile == null) {
			int worstYield = int.MaxValue;
			CityResident worst = null;

			foreach (CityResident cr in city.residents) {
				int tileYield = cr.tileWorked.FoodYield(city.owner).yield +
								cr.tileWorked.ProductionYield(city.owner).yield +
								cr.tileWorked.CommerceYield(city.owner).yield;
				if (tileYield < worstYield) {
					worstYield = tileYield;
					worst = cr;
				}
			}

			// Move the worst citizen to our new tile, being sure to update
			// backpointers from the tile.
			worst.tileWorked.personWorkingTile = null;
			worst.tileWorked = tile;
			tile.personWorkingTile = worst;
			worst.citizenType = citizenTypes.Find(x => x.IsDefaultCitizen);
			return;
		}

		// If we've clicked the city center, re-assign all the citizens using
		// the basic AI, but specify that we want to manage moods by using
		// entertainers if necessary.
		//
		// TODO: This throws away existing nationalities, fix that.
		if (tile.cityAtTile == city) {
			int numResidents = city.residents.Count;
			city.RemoveAllCitizens();

			for (int i = 0; i < numResidents; ++i) {
				CityResident newResident = new() {
					citizenType = citizenTypes.Find(x => x.IsDefaultCitizen),
					nationality = city.owner.civilization,
					city = city
				};
				city.AddCitizen(newResident);
				CityTileAssignmentAI.AssignNewCitizenToTile(gameData, newResident, manageMoods: true);
			}
		}
	}

	private void OnShowCityScreen(ParameterWrapper<City> city) {
		EngineStorage.ReadGameData((GameData gameData) => {
			OnShowCityScreenLocked(gameData, city);
		});
	}

	private void OnShowCityScreenLocked(GameData gameData, ParameterWrapper<City> city) {
		this.Show();
		lastInspectedCity = city.Value;
		mapView.centerCameraOnTile(city.Value.location.neighbors[TileDirection.SOUTH], MapView.CameraPosition.Absolute);
		tileAssignmentLayer.city = city.Value;
		cityName.Text = city.Value.name;
		RenderPopHeads(city.Value);
		RenderCulture(city.Value);
		RenderFoodDetails(city.Value);
		RenderCommerceDetails(city.Value);
		RenderProductionDetails(gameData, city.Value);
		RenderExistingBuildings(city.Value.GetBuildings());
		RenderStrategicResources(gameData, city.Value);
		RenderLuxuries(gameData, city.Value);
	}

	private void OnExit() {
		productionMenu.Hide();
		tileAssignmentLayer.city = null;
		mapView.centerCameraOnTile(lastInspectedCity.location.neighbors[TileDirection.SOUTH]);
	}

	private void RenderStrategicResources(GameData gameData, City city) {
		Dictionary<C7GameData.Resource, int> resourceCounter = city.GetStrategicResources(gameData);

		foreach (var child in strategicResources.GetChildren()) {
			strategicResources.RemoveChild(child);
		}

		foreach ((C7GameData.Resource resource, int count) in resourceCounter) {
			VBoxContainer resourceContainer = new();
			resourceContainer.AddThemeConstantOverride("separation", 0);

			var texture = (ImageTexture)TextureLoader.Load("resources.large", resource, useCache: true).Duplicate();
			texture.SetSizeOverride(new(45, 45));

			TextureRect resourceRect = new() {
				Texture = texture,
			};

			Label resourceLabel = new() {
				Text = count.ToString(),
				HorizontalAlignment = HorizontalAlignment.Center
			};

			resourceContainer.AddChild(resourceRect);
			resourceContainer.AddChild(resourceLabel);

			strategicResources.AddChild(resourceContainer);
		}
	}

	private void RenderLuxuries(GameData gameData, City city) {
		Dictionary<C7GameData.Resource, int> resourceCounter = city.GetLuxuries(gameData);

		foreach (var child in luxuriesContainer.GetChildren()) {
			luxuriesContainer.RemoveChild(child);
		}

		foreach ((C7GameData.Resource resource, int count) in resourceCounter) {
			HBoxContainer resourceContainer = new();

			Label resourceCount = new() {
				Text = "(" + count.ToString() + ")"
			};

			var texture = TextureLoader.Load("resources.small", resource, useCache: true);

			TextureRect resourceRect = new() {
				Texture = texture,
			};

			resourceContainer.AddChild(resourceCount);
			resourceContainer.AddChild(resourceRect);

			luxuriesContainer.AddChild(resourceContainer);
		}
	}

	private void RenderExistingBuildings(List<CityBuilding> buildings) {
		foreach (var node in existingBuildings.GetChildren()) {
			existingBuildings.RemoveChild(node);
		}

		foreach (CityBuilding building in buildings) {
			Label label = new() {
				Text = building.building.name
			};
			existingBuildings.AddChild(label);
		}
	}

	private void RenderFoodDetails(City city) {
		string growthStr;
		int foodLostPerTurn = 0;
		int foodSurplus = 0;
		int turnsUntilGrowth = city.TurnsUntilGrowth();
		if (turnsUntilGrowth == int.MaxValue) {
			growthStr = "Not growing.";
		} else if (turnsUntilGrowth == int.MinValue) {
			growthStr = "Starving!";
			foodLostPerTurn = Math.Abs(city.FoodGrowthPerTurn());
		} else {
			growthStr = $"Growth in {turnsUntilGrowth} turns.";
			foodSurplus = city.FoodGrowthPerTurn();
		}
		growthInLabel.Text = growthStr;
		foodLabel.Text = $"{city.CurrentFoodYield()} per turn";

		RenderFoodBox(city.FoodNeededToGrow(), city.foodStored, foodLostPerTurn, hasGranary: city.HasGranary());
		RenderFoodRow(city.FoodConsumedPerTurn(), foodSurplus);
	}

	private void RenderFoodRow(int foodEatenPerTurn, int foodSurplus) {
		foreach (Node child in foodRowContainer.GetChildren()) {
			foodRowContainer.RemoveChild(child);
			child.QueueFree();
		}

		int width = (int)foodRowContainer.Size.X;
		int iconWidth = eatenFoodTexture.GetWidth();
		int spacerWidth = foodSurplus > 0 ? 100 : 0;
		int spacePerIcon = (width - spacerWidth) / (foodEatenPerTurn + foodSurplus);

		int xOffset = 0;
		for (int i = 0; i < foodEatenPerTurn; ++i) {
			TextureRect icon = new() { Texture = eatenFoodTexture };
			foodRowContainer.AddChild(icon);
			icon.SetPosition(new Vector2(xOffset, 0));
			xOffset += Math.Min(spacePerIcon, iconWidth + 10);
		}

		xOffset = width - iconWidth;
		for (int i = 0; i < foodSurplus; ++i) {
			TextureRect icon = new() { Texture = foodTexture };
			foodRowContainer.AddChild(icon);
			icon.SetPosition(new Vector2(xOffset, 0));
			xOffset -= Math.Min(spacePerIcon, iconWidth + 10);
		}
	}

	private void RenderFoodBox(int foodNeededToGrow, int foodStored, int foodLostPerTurn, bool hasGranary) {
		if (hasGranary && foodStored >= foodNeededToGrow / 2) {
			RenderFoodBoxWithGranary(foodNeededToGrow, foodStored, foodLostPerTurn);
		} else {
			RenderFoodBoxNoGranary(foodNeededToGrow, foodStored, foodLostPerTurn);
		}
	}

	private void RenderFoodBoxNoGranary(int foodNeededToGrow, int foodStored, int foodLostPerTurn) {
		foreach (Node child in foodInBoxContainer.GetChildren()) {
			foodInBoxContainer.RemoveChild(child);
			child.QueueFree();
		}
		foreach (Node child in foodInGranaryContainer.GetChildren()) {
			foodInGranaryContainer.RemoveChild(child);
			child.QueueFree();
		}
		granaryLabel.Visible = false;
		foodInGranaryContainer.Visible = false;

		int width = 120;
		int height = 180;

		// Hardcode the common 20/40/60 sizes, but support custom rules as well.
		if (foodNeededToGrow == 20) {
			foodInBoxContainer.Columns = 2;
		} else if (foodNeededToGrow == 40) {
			foodInBoxContainer.Columns = 4;
		} else if (foodNeededToGrow == 60) {
			foodInBoxContainer.Columns = 6;
		} else {
			foodInBoxContainer.Columns = (int)Math.Ceiling(Math.Sqrt(foodNeededToGrow));
		}

		int itemsPerColumn = (int)Math.Ceiling((float)foodNeededToGrow / foodInBoxContainer.Columns);
		int iconSize = Math.Min(height / itemsPerColumn, width / foodInBoxContainer.Columns);

		int nonEmptySquares = 0;
		for (int i = 0; i < Math.Min(foodNeededToGrow, foodStored) - foodLostPerTurn; ++i) {
			foodInBoxContainer.AddChild(new TextureRect() {
				Texture = foodTexture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspect,
				CustomMinimumSize = new Vector2(iconSize, iconSize),
			});
			++nonEmptySquares;
		}
		for (int i = 0; i < foodLostPerTurn; ++i) {
			foodInBoxContainer.AddChild(new TextureRect() {
				Texture = noFoodTexture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspect,
				CustomMinimumSize = new Vector2(iconSize, iconSize),
			});
			++nonEmptySquares;
		}
		for (int i = 0; i < foodNeededToGrow - nonEmptySquares; ++i) {
			foodInBoxContainer.AddChild(new TextureRect() {
				Texture = emptyFoodTexture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspect,
				CustomMinimumSize = new Vector2(iconSize, iconSize)
			});
		}
	}

	private void RenderFoodBoxWithGranary(int foodNeededToGrow, int foodStored, int foodLostPerTurn) {
		foreach (Node child in foodInBoxContainer.GetChildren()) {
			foodInBoxContainer.RemoveChild(child);
			child.QueueFree();
		}
		foreach (Node child in foodInGranaryContainer.GetChildren()) {
			foodInGranaryContainer.RemoveChild(child);
			child.QueueFree();
		}
		granaryLabel.Visible = true;
		foodInGranaryContainer.Visible = true;

		int width = 120;
		int height = 80;

		// Hardcode the common 20/40/60 sizes, but support custom rules as well.
		if (foodNeededToGrow == 20) {
			foodInBoxContainer.Columns = 2;
		} else if (foodNeededToGrow == 40) {
			foodInBoxContainer.Columns = 4;
		} else if (foodNeededToGrow == 60) {
			foodInBoxContainer.Columns = 6;
		} else {
			foodInBoxContainer.Columns = (int)Math.Ceiling(Math.Sqrt(foodNeededToGrow));
		}
		foodInGranaryContainer.Columns = foodInBoxContainer.Columns;

		int itemsPerColumn = (int)Math.Ceiling((float)foodNeededToGrow / foodInBoxContainer.Columns) / 2;
		int iconSize = Math.Min(height / itemsPerColumn, width / foodInBoxContainer.Columns);

		// Start by filling the granary.
		int foodInGranary = foodNeededToGrow / 2;
		if (foodInGranary > foodStored) { throw new Exception($"not enough food {foodInGranary} {foodStored}"); }

		int foodLostInGranaryPerTurn = Math.Max(0, foodLostPerTurn - (foodStored - foodInGranary));

		for (int i = 0; i < foodInGranary - foodLostInGranaryPerTurn; ++i) {
			foodInGranaryContainer.AddChild(new TextureRect() {
				Texture = foodTexture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspect,
				CustomMinimumSize = new Vector2(iconSize, iconSize),
			});
		}
		for (int i = 0; i < foodLostInGranaryPerTurn; ++i) {
			foodInGranaryContainer.AddChild(new TextureRect() {
				Texture = noFoodTexture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspect,
				CustomMinimumSize = new Vector2(iconSize, iconSize),
			});
		}

		// Now fill the rest of the box.
		foodStored -= foodInGranary;
		foodLostPerTurn -= foodLostInGranaryPerTurn;

		for (int i = 0; i < foodStored - foodLostPerTurn; ++i) {
			foodInBoxContainer.AddChild(new TextureRect() {
				Texture = foodTexture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspect,
				CustomMinimumSize = new Vector2(iconSize, iconSize),
			});
		}
		for (int i = 0; i < foodLostPerTurn; ++i) {
			foodInBoxContainer.AddChild(new TextureRect() {
				Texture = noFoodTexture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspect,
				CustomMinimumSize = new Vector2(iconSize, iconSize),
			});
		}

		for (int i = 0; i < foodNeededToGrow / 2 - foodStored; ++i) {
			foodInBoxContainer.AddChild(new TextureRect() {
				Texture = emptyFoodTexture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspect,
				CustomMinimumSize = new Vector2(iconSize, iconSize)
			});
		}
	}

	private void RenderCommerceDetails(City city) {
		CommerceBreakdown breakdown = city.CurrentCommerceYield();
		commerceTaxesDetails.Text = $"{breakdown.taxes} gold/turn to taxes";
		commerceScienceDetails.Text = $"{breakdown.beakers} gold/turn to science  ({breakdown.corrupted} corrupt)";
		commerceHappinessDetails.Text = $"{breakdown.happiness} gold/turn to happiness";
	}

	private void RenderProductionDetails(GameData gameData, City city) {
		CorruptableValue shields = city.CurrentProductionYield();
		RenderShieldBox(city.owner.ShieldCost(city.itemBeingProduced), city.shieldsStored);
		RenderShieldRow(shields.useful, shields.corrupt);
		productionLabel.Text = $"PRODUCTION: {shields.useful + shields.corrupt} per turn";
		completeInLabel.Text = city.TurnsUntilProductionFinished() == int.MaxValue ? "--" : $"Complete in {city.TurnsUntilProductionFinished()} turns";

		foreach (Node child in productionButton.GetChildren()) {
			child.QueueFree();
		}

		int marginTop = 35;

		if (city.itemBeingProduced is UnitPrototype proto) {
			var unit = proto.GetInstance(gameData.GenerateID(proto.name), proto, city.owner);
			AnimationManager animationManager = mapView.game.animationController.civ3AnimData.forUnit(unit, MapUnit.AnimatedAction.DEFAULT).animationManager;
			ShaderMaterial material = PlayerTextureUtil.GetShaderMaterialForUnit(city.owner.GetPlayerColor());
			(ImageTexture baseImage, ImageTexture imageTint) = animationManager.GetAnimationFrameAndTintTextures(unit);

			// Add the base sprite.
			Sprite2D baseImageSprite = new();
			baseImageSprite.Texture = baseImage;
			baseImageSprite.Position = new Vector2(productionButton.TextureNormal.GetWidth() / 2.0f, marginTop);
			productionButton.AddChild(baseImageSprite);

			// Add the tint sprite, hooking up the shader.
			Sprite2D imageTintSprite = new Sprite2D();
			imageTintSprite.Texture = imageTint;
			imageTintSprite.Material = material;
			imageTintSprite.Position = baseImageSprite.Position;
			productionButton.AddChild(imageTintSprite);
		} else if (city.itemBeingProduced is Building b) {
			Sprite2D icon = new();
			icon.Texture = TextureLoader.Load("building_icons.large", b, useCache: true);
			icon.Position = new Vector2(productionButton.TextureNormal.GetWidth() / 2.0f, marginTop);
			productionButton.AddChild(icon);
		} else if (city.itemBeingProduced is Inflow inflow) {
			Sprite2D icon = new();
			icon.Texture = TextureLoader.Load("building_icons.large", inflow, useCache: true);
			icon.Position = new Vector2(productionButton.TextureNormal.GetWidth() / 2.0f, marginTop);
			productionButton.AddChild(icon);
		}

		Label productionButtonLabel = new();
		productionButton.AddChild(productionButtonLabel);
		productionButtonLabel.SetPosition(new Vector2(0, 65));
		productionButtonLabel.SetTextAndCenterLabel($"{city.itemBeingProduced.name}");

		productionMenu.AddItems(gameData, city, (IProducible p) => {
			EngineStorage.ReadGameData((GameData gameData) => {
				city.SetItemBeingProduced(p);
				RenderProductionDetails(gameData, city);
				RenderCulture(city);
			});
		});
	}

	private void RenderShieldRow(int goodShields, int corruptShields) {
		foreach (Node child in shieldRowContainer.GetChildren()) {
			shieldRowContainer.RemoveChild(child);
			child.QueueFree();
		}

		int width = (int)shieldRowContainer.Size.X;
		int iconWidth = shieldTexture.GetWidth();
		int spacerWidth = corruptShields > 0 ? 100 : 0;
		int spacePerIcon = (width - spacerWidth) / (goodShields + corruptShields);

		int xOffset = 0;
		for (int i = 0; i < corruptShields; ++i) {
			TextureRect icon = new() { Texture = corruptShieldTexture };
			shieldRowContainer.AddChild(icon);
			icon.SetPosition(new Vector2(xOffset, 0));
			xOffset += Math.Min(spacePerIcon, iconWidth);
		}

		xOffset = width - iconWidth;
		for (int i = 0; i < goodShields; ++i) {
			TextureRect icon = new() { Texture = shieldTexture };
			shieldRowContainer.AddChild(icon);
			icon.SetPosition(new Vector2(xOffset, 0));
			xOffset -= Math.Min(spacePerIcon, iconWidth);
		}
	}

	private void RenderShieldBox(int shieldCost, int shieldsInBox) {
		foreach (Node child in shieldsInBoxContainer.GetChildren()) {
			shieldsInBoxContainer.RemoveChild(child);
			child.QueueFree();
		}

		int itemsPerColumn = (int)Math.Ceiling((float)shieldCost / shieldsInBoxContainer.Columns);
		if (itemsPerColumn == 0) return;

		int width = (int)shieldsInBoxContainer.GetParent<CenterContainer>().Size.X;
		int height = (int)shieldsInBoxContainer.GetParent<CenterContainer>().Size.Y;

		shieldsInBoxContainer.Columns = (int)Math.Ceiling(Math.Sqrt(shieldCost));
		int iconSize = Math.Min(height / itemsPerColumn, width / shieldsInBoxContainer.Columns);

		for (int i = 0; i < Math.Min(shieldCost, shieldsInBox); ++i) {
			shieldsInBoxContainer.AddChild(new TextureRect() {
				Texture = shieldTexture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspect,
				CustomMinimumSize = new Vector2(iconSize, iconSize),
			});
		}
		for (int i = 0; i < shieldCost - shieldsInBox; ++i) {
			shieldsInBoxContainer.AddChild(new TextureRect() {
				Texture = emptyShieldTexture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspect,
				CustomMinimumSize = new Vector2(iconSize, iconSize)
			});
		}
	}

	private void RenderCulture(City city) {
		culturePerTurn.Text = $"{city.GetCulturePerTurn()}/turn";

		int nextCultureExpansion = (int)Math.Pow(10, city.GetBorderExpansionLevel());
		totalCulture.Text = $"Total: {city.GetCulture()}/{nextCultureExpansion}";
	}

	private void RenderPopHeads(City city) {
		// Reset any old heads.
		foreach (TextureButton head in popHeads) {
			background.RemoveChild(head);
			head.QueueFree();
		}
		// Reset any old head effects.
		foreach (TextureRect effect in popHeadEffects) {
			background.RemoveChild(effect);
			effect.QueueFree();
		}

		popHeads.Clear();
		popHeadEffects.Clear();

		int eraNum = city.owner.EraIndex();

		// The pop head textures are 50 x 50, but have a 1px border on all sides
		//
		// The texture file has 16 rows of the default citizen, in groups of 4
		// per era (content, happy, resisting, unhappy). There are 10 columns,
		// the first 5 are male heads of different regions for civs, the other 5
		// are female heads.
		//
		// After the 16 rows of default citizens there is one row per specialist
		// type, and again 10 columns per row. This time they are
		// (ancient, middle, industrial, modern, blank) for male and female heads
		//
		// TODO: handle per-civ regions
		// TODO: handle male/female citizens

		// Start by splitting the default residents from the specialists, since
		// they are spaced apart in the UI.
		List<CityResident> happyResidents =
			city.residents.FindAll(x => x.citizenType.IsDefaultCitizen && x.mood == CityResident.Mood.Happy);
		List<CityResident> contentResidents =
			city.residents.FindAll(x => x.citizenType.IsDefaultCitizen && x.mood == CityResident.Mood.Content);
		List<CityResident> unhappyResidents =
			city.residents.FindAll(x => x.citizenType.IsDefaultCitizen && x.mood == CityResident.Mood.Unhappy);
		List<CityResident> specialists = city.residents.FindAll(x => !x.citizenType.IsDefaultCitizen);

		// Leave a 1 head gap if we have specialists.
		int width = city.residents.Count * PopHead.HEAD_SIZE;
		if (specialists.Count > 0) {
			width += PopHead.HEAD_SIZE;
		}

		// Leave a 1 head gap between each section of moods.
		int numMoodsPresent = (happyResidents.Count > 0 ? 1 : 0)
			+ (contentResidents.Count > 0 ? 1: 0)
			+ (unhappyResidents.Count > 0 ? 1: 0);
		width += (numMoodsPresent - 1) * PopHead.HEAD_SIZE;

		// Track the x position of each head so that we're centered in the screen
		int xPos = background.Texture.GetWidth() / 2 + -width / 2;

		// Add each of the default citizens. These are buttons with the idea that
		// we can eventually support clicking on the heads to view details, such
		// as the reason for unhappiness.
		foreach (CityResident cr in happyResidents) {
			xPos = AddDefaultCitizen(cr, xPos, eraNum);
		}
		if (happyResidents.Count > 0 && (contentResidents.Count > 0 || unhappyResidents.Count > 0)) {
			xPos += PopHead.HEAD_SIZE;
		}
		foreach (CityResident cr in contentResidents) {
			xPos = AddDefaultCitizen(cr, xPos, eraNum);
		}
		if (contentResidents.Count > 0 && unhappyResidents.Count > 0) {
			xPos += PopHead.HEAD_SIZE;
		}
		foreach (CityResident cr in unhappyResidents) {
			xPos = AddDefaultCitizen(cr, xPos, eraNum);
		}

		// Add space before specialists.
		xPos += PopHead.HEAD_SIZE;

		// Add each of the specialists.
		//
		// TODO: Render the specialist effect (like a smiley for entertainers)
		// in the corner of the head.
		foreach (CityResident cr in specialists) {
			TextureButton tb = new();
			tb.TextureNormal = PopHead.GetTexture(cr, eraNum);
			tb.SetPosition(new Vector2(xPos, POP_HEAD_OFFSET_Y));

			List<CitizenType> specialistTypes = null;
			EngineStorage.ReadGameData((GameData gameData) => { specialistTypes = city.owner.GetKnownSpecialists(gameData); });

			int index = specialistTypes.FindIndex(x => x.Id == cr.citizenType.Id);
			tb.Pressed += () => {
				cr.citizenType = specialistTypes[(index + 1) % specialistTypes.Count];
				++index;
				RenderPopHeads(city);

				EngineStorage.ReadGameData((GameData gameData) => { RenderCommerceDetails(city); });
			};

			background.AddChild(tb);

			float iconOffset = 0.0f;
			foreach (KeyValuePair<string, int> entry in GetSpecialistEffectInfo(cr)) {
				if (!effectIcons.TryGetValue(entry.Key, out ImageTexture texture)) continue;
				for (int i = 0; i < entry.Value; i++) {
					TextureRect icon = new();
					icon.Texture = texture;

					float iconHalfWidth = icon.Texture.GetWidth() / 2.0f;
					float iconXPos = tb.Position.X + iconHalfWidth * i + iconOffset;
					float iconYPos = tb.Position.Y + PopHead.HEAD_SIZE - 10;

					icon.SetPosition(new Vector2(iconXPos, iconYPos));
					background.AddChild(icon);
					popHeadEffects.Add(icon);

					// If multiple effects are applicable we need to
					// calculate the total offset that comes before each type.
					// The offset is only calculated on the last iteration
					// because we don't want any extra offset between effects of the same type.
					if (i == entry.Value - 1) {
						iconOffset += iconHalfWidth * entry.Value;
					}
				}
			}

			popHeads.Add(tb);
			xPos += PopHead.HEAD_SIZE;
		}
	}

	private void SwitchToNextCity() {
		EngineStorage.ReadGameData((GameData gameData) => {
			City currentCity = tileAssignmentLayer.city;
			List<City> cities = currentCity.owner.cities;
			City nextCity = cities[(cities.IndexOf(currentCity) + 1) % cities.Count];
			nextCity.RecalculateCitizenMoods(gameData);
			OnShowCityScreenLocked(gameData, new ParameterWrapper<City>(nextCity));
		});
	}

	private void SwitchToPreviousCity() {
		EngineStorage.ReadGameData((GameData gameData) => {
			City currentCity = tileAssignmentLayer.city;
			List<City> cities = currentCity.owner.cities;
			City previousCity = cities[(cities.IndexOf(currentCity) + cities.Count - 1) % cities.Count];
			previousCity.RecalculateCitizenMoods(gameData);
			OnShowCityScreenLocked(gameData, new ParameterWrapper<City>(previousCity));
		});
	}

	private int AddDefaultCitizen(CityResident cr, int xPos, int eraNum) {
		TextureButton tb = new();
		tb.TextureNormal = PopHead.GetTexture(cr, eraNum);
		tb.SetPosition(new Vector2(xPos, POP_HEAD_OFFSET_Y));
		background.AddChild(tb);
		popHeads.Add(tb);
		return xPos + PopHead.HEAD_SIZE;
	}

	private Dictionary<string, int> GetSpecialistEffectInfo(CityResident cityResident) {
		Dictionary<string, int> specialistEffectInfo = new();
		specialistEffectInfo.TryAdd(LUXURIES, cityResident.citizenType.Luxuries);
		specialistEffectInfo.TryAdd(TAXES, cityResident.citizenType.Taxes);
		specialistEffectInfo.TryAdd(RESEARCH, cityResident.citizenType.Research);
		specialistEffectInfo.TryAdd(CORRUPTION, cityResident.citizenType.Corruption);
		specialistEffectInfo.TryAdd(CONSTRUCTION, cityResident.citizenType.Construction);

		return specialistEffectInfo;
	}
}
