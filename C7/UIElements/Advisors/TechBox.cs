using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using Godot;
using static C7GameData.EraUtils;

public partial class TechBox : TextureButton {
	private Tech tech;
	private TechState techState;

	private readonly Dictionary<ID, List<Building>> Buildings = new();
	private readonly Dictionary<ID, List<Building>> ObsoleteBuildings = new();
	private readonly Dictionary<ID, List<Terraform>> Terraforms = new();
	private readonly Dictionary<ID, List<UnitPrototype>> Units = new();

	private static readonly Dictionary<string, string> TruncatedToFullTechTextMap = new();
	private static readonly Dictionary<string, ImageTexture> CachedObsoleteBuildingTextures = new();

	private FontFile smallFont = new();
	private Theme smallFontTheme = new();
	private int smallFontSize = 11;

	private Label techNameLabel;

	private int queueNumber;

	public enum TechState {
		// This tech is known to the player.
		kKnown,
		// The player is actively researching this tech.
		kInProgress,
		// The player could research this tech.
		kPossible,
		// The player needs to research the prerequisites before this tech can
		// be researched.
		kBlocked,
		// The player has queued this tech for research.
		kQueued,
	}

	public TechBox(Tech tech, TechState techState, int queueNumber = 0) {
		this.tech = tech;
		this.techState = techState;
		this.queueNumber = queueNumber;
	}

	public override void _Ready() {
		smallFont = ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-Regular.ttf");

		if (!tech.RequiredForEraAdvancement)
			smallFont = ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-Italic.ttf");

		if (tech.id == EngineStorage.gameData.GetFirstHumanPlayer().currentlyResearchedTech)
			smallFont = ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-Bold.ttf");

		smallFontTheme.DefaultFont = smallFont;
		smallFontTheme.SetColor("font_color", "Label", Colors.Black);
		smallFontTheme.SetFontSize("font_size", "Label", smallFontSize);


		int techBoxSizeCost = CalculateTechBoxSizeCost(EngineStorage.gameData);
		string techBoxSize = CostToStringKey(techBoxSizeCost);
		string era = CalculateTechEraTexture(tech.EraCivilopediaName);

		ImageTexture knownTechBox = TextureLoader.Load($"tech_boxes.known.{era}.{techBoxSize}");
		ImageTexture inProgressTechBox = TextureLoader.Load($"tech_boxes.in_progress.{era}.{techBoxSize}");
		ImageTexture possibleTechBox = TextureLoader.Load($"tech_boxes.possible.{era}.{techBoxSize}");
		ImageTexture blockedTechBox = TextureLoader.Load($"tech_boxes.blocked.{era}.{techBoxSize}");

		TextureNormal = techState switch {
			TechState.kKnown => knownTechBox,
			TechState.kInProgress => inProgressTechBox,
			TechState.kPossible => possibleTechBox,
			TechState.kBlocked => blockedTechBox,
			TechState.kQueued => inProgressTechBox,
			_ => throw new ArgumentOutOfRangeException("Invalid tech state")
		};

		ImageTexture techIconTexture = TextureLoader.Load("tech_icons.small", tech, useCache: true);
		TextureRect icon = new() { Texture = techIconTexture };
		icon.SetPosition(new Vector2(12, 32));
		AddChild(icon);

		// Figure out how many characters can fit in the tech box's width.
		// We use this for truncation when creating the label below.
		//
		// We could be calculating this every time based on the font size,
		// but I don't think it's worth the computation cost
		// a larger char length, means a smaller Tech name (more chars will be truncated)
		float averageCharLength = 6.0f;
		int boxBorderWidth = 16;
		int charLimitOfCurrentBox = (int)((TextureNormal.GetWidth() - boxBorderWidth) / averageCharLength);

		int estimatedTurns = EngineStorage.gameData.GetFirstHumanPlayer().EstimateTurnsToResearch(EngineStorage.gameData, tech);
		string estimatedTurnsString = estimatedTurns > 50 ? $"(-- turns)" : $"({estimatedTurns} turns)";

		string techName = tech.Name;
		string prepend = techState is TechState.kInProgress or TechState.kQueued ? $"{queueNumber}." : "";

		if (techState is TechState.kInProgress) {
			techName = $"{prepend} {tech.Name} {estimatedTurnsString}";
		} else if (techState is TechState.kQueued) {
			techName = $"{prepend} {tech.Name}";
		} else if (techState is TechState.kPossible) {
			techName = $"{tech.Name} {estimatedTurnsString}";
		}

		UpdateLabelTheme();

		techNameLabel = new() {
			Text = TruncateAndCacheName(techName, charLimitOfCurrentBox),
			OffsetLeft = 12,
			OffsetTop = 13,
			Theme = smallFontTheme,
		};

		this.MouseEntered += ShowTooltip;
		this.MouseExited += UpdateLabelTheme;

		int offsetX = techIconTexture.GetWidth() + 7;
		int offsetY = 0;
		int counter = 0;

		List<ImageTexture> techEffects = TechEffectTextures();

		for (int i = 0; i < techEffects.Count; i++) {
			TextureRect tr = new() { Texture = techEffects.ElementAt(i), };
			tr.SetPosition(new Vector2(12 + offsetX, 32 + offsetY));
			AddChild(tr);

			if (techBoxSizeCost > 4) {
				if (i == 2) {
					offsetX = 7;
					offsetY += techIconTexture.GetHeight() + 1;
				}
			}

			offsetX += (int)tr.GetSize().X + 1;
		}

		AddChild(techNameLabel);

		if (!tech.RequiredForEraAdvancement) {
			TextureRect notRequired = new() { Texture = TextureLoader.Load("tech_boxes.non_required"), };
			notRequired.SetPosition(new Vector2(TextureNormal.GetWidth() - 20, 0));
			AddChild(notRequired);
		}
	}

	private void ShowTooltip() {
		smallFontTheme.SetColor("font_color", "Label", new Color(0.71f, 0.35f, 0.13f));

		var customTheme = new Theme();
		customTheme.SetStylebox("panel", "TooltipPanel", TemporaryPopup.InfoStyleBox());
		customTheme.SetColor("font_color", "TooltipLabel", Colors.Black);
		customTheme.SetFontSize("font_size", "TooltipLabel", 12);
		this.Theme = customTheme;

		if (TruncatedToFullTechTextMap.TryGetValue(tech.Name, out string fullText))
			this.TooltipText = $"{fullText}";
	}

	private void UpdateLabelTheme() {
		Color color = Colors.Black;
		bool isTechEraBeyondPlayerEra = GetEraIndex(tech.EraCivilopediaName) > GetEraIndex(EngineStorage.gameData.GetFirstHumanPlayer().eraCivilopediaName);

		if (techState is TechState.kKnown)
			color = Colors.MediumBlue;
		else if (techState is TechState.kQueued or TechState.kInProgress)
			color = Colors.RoyalBlue;
		else if (techState is TechState.kPossible)
			color = Colors.DarkOliveGreen;
		else if (isTechEraBeyondPlayerEra)
			color = Colors.DimGray;

		smallFontTheme.SetColor("font_color", "Label", color);
	}

	private string TruncateAndCacheName(string input, int limit) {
		if (input.Length > limit) {
			if (TruncatedToFullTechTextMap.ContainsKey(tech.Name)) {
				TruncatedToFullTechTextMap[tech.Name] = input;
			} else {
				TruncatedToFullTechTextMap.TryAdd(tech.Name, input);
			}
			return $"{input.Trim().Substring(0, limit - 1)}...";
		}
		TruncatedToFullTechTextMap.Remove(tech.Name);
		return input;
	}

	// TODO: When we figure out how to load the data, load the correct textures
	private List<ImageTexture> TechEffectTextures() {
		List<ImageTexture> textures = new ();

		// Units
		foreach (UnitPrototype unit in Units[tech.id]) {
			ImageTexture texture = TextureLoader.LoadByPath(unit.art.pediaArt.small);
			textures.Add(texture);
		}

		// Buildings
		foreach (Building building in Buildings[tech.id]) {
			// TODO : load the correct textures
			// ImageTexture texture = ...
			// textures.Add(texture);
		}

		// Terraforms
		foreach (Terraform terraform in Terraforms[tech.id]) {
			textures.Add(TextureLoader.Load($"{terraform.ButtonTexture}.normal"));
		}

		// Obsolete buildings
		foreach (Building building in ObsoleteBuildings[tech.id]) {
			// if (cachedObsoleteBuildingTextures.TryGetValue(building.name, out ImageTexture texture)) {
			// 	textures.Add(texture);
			// } else {
			// 	// TODO : load the correct textures
			// 	// Image rawImage = ...
			// 	// Image xMarkedImage = DrawXOnImage(rawImage, new Color(1, 0, 0), 1);
			// 	// ImageTexture obsoleteBuilding = ImageTexture.CreateFromImage(xMarkedImage);
			// 	// cachedObsoleteBuildingTextures.TryAdd(building.name, obsoleteBuilding);
			// 	// textures.Add(texture);
			// }
		}

		return textures;
	}

	private int CalculateTechBoxSizeCost(GameData gameData) {
		int cost = 0;

		// List of building that require this tech to be built
		List<Building> buildings = gameData.Buildings.Where(b => b.requiredTech == tech).ToList();
		Buildings.TryAdd(tech.id, buildings);
		cost += buildings.Count;

		// List of Great Wonders this tech renders obsolete
		List<Building> obsoleteGrWonders = gameData.Buildings.Where(b => b.renderedObsoleteBy == tech).ToList();
		ObsoleteBuildings.TryAdd(tech.id, obsoleteGrWonders);
		cost += obsoleteGrWonders.Count;

		List<UnitPrototype> units = new();
		// List of units that require this tech to be built, taking into account that some are unique
		List<UnitPrototype> unitsRequiringTech = gameData.unitPrototypes.Where(u => u.requiredTech == tech).ToList();

		// Then add the correct units to the list
		foreach (UnitPrototype u in unitsRequiringTech) {
			if (u.producibleBy.Contains(EngineStorage.gameData.GetFirstHumanPlayer().civilization)) {
				units.Add(u);
			}
		}

		Units.TryAdd(tech.id, units);
		cost += units.Count;

		// List of terraform actions that require this tech to be done
		List<Terraform> terraforms = gameData.Terraforms.Where(t => t.RequiredTech == tech.id).ToList();
		Terraforms.TryAdd(tech.id, terraforms);
		cost += terraforms.Count;

		return cost;
	}

	private string CostToStringKey(int cost) {
		// at best the large container fits 6 items
		if (cost is > 4 and <= 6) {
			return "large";
		} else if (cost > 3) {
			return "long";
		} else if (cost > 1) {
			return "medium";
		} else {
			return "small";
		}
	}

	private string CalculateTechEraTexture(string techEra) {
		return techEra switch {
			ANCIENT_TIMES_CVLPD => "ancient",
			MIDDLE_AGES_CVLPD => "middle",
			INDUSTRIAL_AGE_CVLPD => "industrial",
			MODERN_ERA_CVLPD => "modern",
			_ => "ancient"
		};
	}

	private Image DrawXOnImage(Image image, Color color, int thickness) {
		// draw line from top left, to bottom right  ╲
		DrawLineOnImage(image, new Vector2I(0, 0), new Vector2I(image.GetSize().X, image.GetSize().Y), color, thickness);
		// draw line from top right, to bottom left  ╱
		DrawLineOnImage(image, new Vector2I(image.GetSize().X, 0), new Vector2I(0, image.GetSize().Y), color, thickness);
		return image;
	}

	private void DrawLineOnImage(Image img, Vector2I start, Vector2I end, Color color, int thickness) {
		Vector2 direction = new Vector2(end.X - start.X, end.Y - start.Y).Normalized();
		float distance = start.DistanceTo(end);

		for (int i = 0; i < distance; i++) {
			Vector2 pos = new Vector2(start.X, start.Y) + direction * i;
			Vector2I point = new Vector2I(Mathf.RoundToInt(pos.X), Mathf.RoundToInt(pos.Y));

			for (int dx = -thickness / 2; dx <= thickness / 2; dx++) {
				for (int dy = -thickness / 2; dy <= thickness / 2; dy++) {
					Vector2I pixel = point + new Vector2I(dx, dy);
					if (pixel.X >= 0 && pixel.X < img.GetWidth() && pixel.Y >= 0 && pixel.Y < img.GetHeight()) {
						img.SetPixel(pixel.X, pixel.Y, color);
					}
				}
			}
		}
	}
}
