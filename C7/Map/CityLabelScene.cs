using C7.Textures;
using C7GameData;
using Godot;

namespace C7.Map;

public partial class CityLabelScene : Node2D {
	City city;
	public Vector2I tileCenter;
	Color civColor;

	const byte TRANSPARENCY = 192; // 25%
	const int CITY_LABEL_HEIGHT = 23;
	const int LEFT_RIGHT_BOXES_WIDTH = 24;
	const int LEFT_RIGHT_BOXES_HEIGHT = CITY_LABEL_HEIGHT - 2;
	const int CENTRAL_PANEL_SEPARATOR_WIDTH = 4;

	static ImageTexture nonEmbassyStar;
	static Theme smallFontTheme = new();
	static Theme popThemeRed = new();
	static Theme popSizeTheme = new();

	PanelContainer labelPanel = new();
	HBoxContainer mainContainer = new();
	PanelContainer popSizePanel = new();
	VBoxContainer centerContainer = new();
	PanelContainer capitalPanel = new();
	HSeparator centerDivider = new();

	HSeparator borderTop = new();
	HSeparator borderBottom = new();

	VSeparator leftSeparator = new();
	VSeparator rightSeparator = new();

	Label cityNameLabel = new();
	Label productionLabel = new();
	Label popSizeLabel = new();

	static FontFile smallFont = new();
	static FontFile midSizedFont = new();

	Color topRowGrey = Color.Color8(32, 32, 32, TRANSPARENCY);
	Color bottomRowGrey = Color.Color8(48, 48, 48, TRANSPARENCY);
	Color backgroundGrey = Color.Color8(64, 64, 64, TRANSPARENCY);
	Color borderGrey = Color.Color8(80, 80, 80, TRANSPARENCY);

	static CityLabelScene() {
		smallFontTheme.DefaultFont = smallFont;
		smallFontTheme.SetColor("font_color", "Label", Color.Color8(255, 255, 255, 255));
		smallFontTheme.SetFontSize("font_size", "Label", 11);
		popSizeTheme.DefaultFont = midSizedFont;
		popSizeTheme.SetColor("font_color", "Label", Color.Color8(255, 255, 255, 255));
		popSizeTheme.SetFontSize("font_size", "Label", 18);
		popThemeRed.DefaultFont = midSizedFont;
		popThemeRed.SetColor("font_color", "Label", Color.Color8(255, 0, 0, 255));
		popThemeRed.SetFontSize("font_size", "Label", 18);

		//Mid-Size font skips the cache as it sets a custom size
		midSizedFont = ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-Regular.ttf");

		//Small font doesn't, because otherwise it makes everything small
		smallFont = ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-Regular.ttf", null, ResourceLoader.CacheMode.Ignore);
		//Must set the FixedSize so Godot can calculate the width of the font for city labels
		smallFont.FixedSize = 11;

		nonEmbassyStar = TextureLoader.Load("icons.capital_star");
	}

	public CityLabelScene(City city) {
		this.city = city;

		civColor = new Color(TextureLoader.LoadColor(city.owner.GetPlayerColor()), TRANSPARENCY);

		// Set up UI hierarchy
		AddChild(labelPanel);
		labelPanel.AddChild(mainContainer);

		// Left side (population)
		mainContainer.AddChild(popSizePanel);
		popSizePanel.AddChild(popSizeLabel);

		// Center (city name, production and population growth)
		mainContainer.AddChild(leftSeparator);
		mainContainer.AddChild(centerContainer);
		mainContainer.AddChild(rightSeparator);
		SetupCenterPanel();

		mainContainer.AddThemeConstantOverride("separation", 0);
		centerContainer.AddThemeConstantOverride("separation", 0);

		borderTop.AddThemeConstantOverride("separation", 1);
		borderBottom.AddThemeConstantOverride("separation", 1);
		centerDivider.AddThemeConstantOverride("separation", 1);

		popSizeLabel.HorizontalAlignment = HorizontalAlignment.Center;
		popSizeLabel.VerticalAlignment = VerticalAlignment.Center;
		popSizePanel.CustomMinimumSize = new Vector2(LEFT_RIGHT_BOXES_WIDTH, LEFT_RIGHT_BOXES_HEIGHT);

		labelPanel.MouseFilter = Control.MouseFilterEnum.Ignore;

		cityNameLabel.Theme = smallFontTheme;
		productionLabel.Theme = smallFontTheme;
		popSizeLabel.Theme = popSizeTheme;

		ApplyStyles();
	}

	private void SetupCenterPanel() {
		centerContainer.AddChild(borderTop);
		centerContainer.AddChild(cityNameLabel);
		centerContainer.AddChild(centerDivider);
		centerContainer.AddChild(productionLabel);
		centerContainer.AddChild(borderBottom);

		centerContainer.CustomMinimumSize = new Vector2(60, -1);

		cityNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
		cityNameLabel.VerticalAlignment = VerticalAlignment.Center;

		productionLabel.HorizontalAlignment = HorizontalAlignment.Center;
		productionLabel.VerticalAlignment = VerticalAlignment.Center;
	}

	private void SetupCapitalPanel() {
		capitalPanel = new PanelContainer();

		StyleBoxFlat capitalStyle = new() {
			BgColor = civColor
		};

		capitalPanel.AddThemeStyleboxOverride("panel", capitalStyle);
		capitalPanel.CustomMinimumSize = new Vector2(LEFT_RIGHT_BOXES_WIDTH, LEFT_RIGHT_BOXES_HEIGHT);

		TextureRect starTextureRect = new() {
			Texture = nonEmbassyStar,
			StretchMode = TextureRect.StretchModeEnum.KeepCentered
		};

		capitalPanel.AddChild(starTextureRect);
	}

	private void ApplyStyles() {
		// style for the main panel
		StyleBoxFlat labelStyle = new() {
			BgColor = backgroundGrey,
			BorderColor = borderGrey,
			BorderWidthBottom = 1,
			BorderWidthLeft = 1,
			BorderWidthRight = 1,
			BorderWidthTop = 1
		};

		StyleBoxFlat popStyle = new() {
			BgColor = civColor
		};

		StyleBoxLine centerSeparatorStyle = new() {
			Color = civColor,
			GrowBegin = CENTRAL_PANEL_SEPARATOR_WIDTH,
			GrowEnd = CENTRAL_PANEL_SEPARATOR_WIDTH,
		};

		StyleBoxLine bottomBorderStyle = new() {
			Color = bottomRowGrey,
			GrowBegin = CENTRAL_PANEL_SEPARATOR_WIDTH,
			GrowEnd = CENTRAL_PANEL_SEPARATOR_WIDTH,
		};

		StyleBoxLine topBorderStyle = new() {
			Color = topRowGrey,
			GrowBegin = CENTRAL_PANEL_SEPARATOR_WIDTH,
			GrowEnd = CENTRAL_PANEL_SEPARATOR_WIDTH,
		};

		StyleBoxLine separatorStyle = new() {
			Color = new Color(0, 0, 0, 0), // transparent,
			Thickness = CENTRAL_PANEL_SEPARATOR_WIDTH,
			Vertical = true
		};

		labelPanel.AddThemeStyleboxOverride("panel", labelStyle);
		popSizePanel.AddThemeStyleboxOverride("panel", popStyle);
		centerDivider.AddThemeStyleboxOverride("separator", centerSeparatorStyle);
		borderTop.AddThemeStyleboxOverride("separator", topBorderStyle);
		borderBottom.AddThemeStyleboxOverride("separator", bottomBorderStyle);
		leftSeparator.AddThemeStyleboxOverride("separator", separatorStyle);
		rightSeparator.AddThemeStyleboxOverride("separator", separatorStyle);
	}

	public override void _Draw() {
		UpdateContent();
	}

	private void UpdateContent() {
		int turnsUntilGrowth = city.TurnsUntilGrowth();
		string turnsUntilGrowthText = turnsUntilGrowth == int.MaxValue || turnsUntilGrowth < 0 ? "- -" : "" + turnsUntilGrowth;

		cityNameLabel.Text = $"{city.name} : {turnsUntilGrowthText}";
		if (city.itemBeingProduced != null) {
			productionLabel.Text = $"{city.itemBeingProduced.name} : {city.TurnsUntilProductionFinished()}";
		} else {
			productionLabel.Text = "-- : --";
		}

		if (city.TurnsUntilProductionFinished() == int.MaxValue && city.itemBeingProduced != null) {
			productionLabel.Text = $"{city.itemBeingProduced.name} : --";
		}
		popSizeLabel.Text = city.residents.Count.ToString();

		// Update population label color based on growth
		if (city.TurnsUntilGrowth() < 0) {
			popSizeLabel.Theme = popThemeRed;
		} else {
			popSizeLabel.Theme = popSizeTheme;
		}

		// Update the panel with the capital star
		bool hasCapitalIndicator = capitalPanel.GetParent() == mainContainer;

		if (city.IsCapital() && !hasCapitalIndicator) {
			SetupCapitalPanel();
			mainContainer.AddChild(capitalPanel);
		} else if (!city.IsCapital() && hasCapitalIndicator) {
			mainContainer.RemoveChild(capitalPanel);
		}

		labelPanel.Position = new Vector2(tileCenter.X - labelPanel.Size.X / 2, tileCenter.Y + 24);

		// Force the layout to recalculate
		labelPanel.Size = Vector2.Zero;
	}
}
