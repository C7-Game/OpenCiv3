using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;
using Godot;
using Resource = C7GameData.Resource;

public enum CodexSection {
	Civilizations,
	Technologies,
	GreatWonders,
	SmallWonders,
	CityImprovements,
	Governments,
	Units,
	TerrainImprovements,
	Terrain,
	Resources,
	GameConcepts,
}

// One renderable Codex article: a name, optional icon, stat block, and markdown pages.
public class CodexArticle {
	public string Key;
	public string Name;
	public ImageTexture Icon;
	public List<string> StatLines = new();
	public List<string> Pages = new();

	public bool HasStats => StatLines.Count > 0;
	public bool HasPages => Pages.Count > 0;
}

public static class CodexCatalog {
	public static readonly CodexSection[] SectionOrder = {
		CodexSection.Units,
		CodexSection.CityImprovements,
		CodexSection.GreatWonders,
		CodexSection.SmallWonders,
		CodexSection.Technologies,
		CodexSection.Civilizations,
		CodexSection.Governments,
		CodexSection.Terrain,
		CodexSection.Resources,
		CodexSection.TerrainImprovements,
		CodexSection.GameConcepts,
	};

	public static string SectionTitle(CodexSection section) {
		return section switch {
			CodexSection.Units => "Units",
			CodexSection.CityImprovements => "City Improvements",
			CodexSection.GreatWonders => "Great Wonders",
			CodexSection.SmallWonders => "Small Wonders",
			CodexSection.Technologies => "Technologies",
			CodexSection.Civilizations => "Civilizations",
			CodexSection.Governments => "Governments",
			CodexSection.Terrain => "Terrain",
			CodexSection.Resources => "Resources",
			CodexSection.TerrainImprovements => "Terrain Improvements",
			CodexSection.GameConcepts => "Game Concepts",
			_ => section.ToString(),
		};
	}

	public static List<CodexArticle> Build(GameData gameData, CodexSection section) {
		List<CodexArticle> articles = section switch {
			CodexSection.Units => BuildUnits(gameData),
			CodexSection.CityImprovements => BuildBuildings(gameData, BuildingKind.CityImprovement),
			CodexSection.GreatWonders => BuildBuildings(gameData, BuildingKind.GreatWonder),
			CodexSection.SmallWonders => BuildBuildings(gameData, BuildingKind.SmallWonder),
			CodexSection.Technologies => BuildTechnologies(gameData),
			CodexSection.Civilizations => BuildCivilizations(gameData),
			CodexSection.Governments => BuildGovernments(gameData),
			CodexSection.Terrain => BuildTerrain(gameData),
			CodexSection.Resources => BuildResources(gameData),
			CodexSection.TerrainImprovements => BuildTerraforms(gameData),
			CodexSection.GameConcepts => BuildGameConcepts(gameData),
			_ => new List<CodexArticle>(),
		};

		// Concepts keep their authored order; the other sections are indexes.
		if (section != CodexSection.GameConcepts) {
			articles.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
		}
		return articles;
	}

	private enum BuildingKind {
		CityImprovement,
		GreatWonder,
		SmallWonder,
	}

	private static List<CodexArticle> BuildUnits(GameData gameData) {
		List<CodexArticle> articles = new();
		foreach (UnitPrototype unit in gameData.unitPrototypes) {
			if (string.IsNullOrEmpty(unit.name)) {
				continue;
			}

			CodexArticle article = NewArticle(gameData, unit.civilopediaEntry, unit.name);
			article.Icon = TryLoad(() => LoadUnitIcon(unit));

			if (unit.attack > 0) article.StatLines.Add($"Attack: {unit.attack}");
			if (unit.defense > 0) article.StatLines.Add($"Defense: {unit.defense}");
			if (unit.movement > 0) article.StatLines.Add($"Movement: {unit.movement}");
			if (unit.bombard > 0) article.StatLines.Add($"Bombard: {unit.bombard} (rate {unit.rateOfFire}, range {unit.bombardRange})");
			if (unit.capacity > 0) article.StatLines.Add($"Capacity: {unit.capacity}");
			if (unit.shieldCost > 0) article.StatLines.Add($"Cost: {unit.shieldCost} shields");
			if (unit.populationCost > 0) article.StatLines.Add($"Population cost: {unit.populationCost}");
			if (unit.requiredTech != null) article.StatLines.Add($"Requires: {unit.requiredTech.Name}");
			if (unit.requiredResources.Count > 0) article.StatLines.Add($"Resources: {Join(unit.requiredResources.Select(r => r.Name))}");
			if (unit.upgradesTo.Count > 0) article.StatLines.Add($"Upgrades to: {Join(unit.upgradesTo.Select(u => u.name))}");

			articles.Add(article);
		}
		return articles;
	}

	private static ImageTexture LoadUnitIcon(UnitPrototype unit) {
		string path = unit.art.pediaArt.large;
		if (string.IsNullOrEmpty(path)) {
			path = unit.art.pediaArt.small;
		}
		return string.IsNullOrEmpty(path) ? null : TextureLoader.LoadByPath(path);
	}

	private static List<CodexArticle> BuildBuildings(GameData gameData, BuildingKind kind) {
		List<CodexArticle> articles = new();
		foreach (Building building in gameData.Buildings) {
			if (string.IsNullOrEmpty(building.name) || KindOf(building) != kind) {
				continue;
			}

			CodexArticle article = NewArticle(gameData, building.civilopediaEntry, building.name);
			article.Icon = TryLoad(() => TextureLoader.Load("building_icons.large", building, useCache: true));

			if (building.shieldCost > 0) article.StatLines.Add($"Cost: {building.shieldCost} shields");
			if (building.maintenanceCost > 0) article.StatLines.Add($"Maintenance: {building.maintenanceCost} gold/turn");
			if (building.culturePerTurn > 0) article.StatLines.Add($"Culture: +{building.culturePerTurn} per turn");
			if (building.contentFacesInCity > 0) article.StatLines.Add($"Content citizens: +{building.contentFacesInCity}");
			if (building.unhappyFacesInCity > 0) article.StatLines.Add($"Unhappy citizens: {building.unhappyFacesInCity}");
			if (building.requiredTech != null) article.StatLines.Add($"Requires: {building.requiredTech.Name}");
			if (building.requiredResources.Count > 0) article.StatLines.Add($"Resources: {Join(building.requiredResources.Select(r => r.Name))}");
			if (building.renderedObsoleteBy != null) article.StatLines.Add($"Obsolete with: {building.renderedObsoleteBy.Name}");

			articles.Add(article);
		}
		return articles;
	}

	private static BuildingKind KindOf(Building building) {
		if (building.greatWonderProperties != null) {
			return BuildingKind.GreatWonder;
		}
		return building.isSmallWonder ? BuildingKind.SmallWonder : BuildingKind.CityImprovement;
	}

	private static List<CodexArticle> BuildTechnologies(GameData gameData) {
		List<CodexArticle> articles = new();
		foreach (Tech tech in gameData.techs) {
			if (string.IsNullOrEmpty(tech.Name)) {
				continue;
			}

			CodexArticle article = NewArticle(gameData, tech.CivilopediaEntry, tech.Name);
			article.Icon = TryLoad(() => TextureLoader.Load("tech_icons.small", tech, useCache: true));

			if (tech.Cost > 0) article.StatLines.Add($"Cost: {tech.Cost} beakers");
			if (!string.IsNullOrEmpty(tech.EraCivilopediaName)) article.StatLines.Add($"Era: {PrettifyKey(tech.EraCivilopediaName, "ERA_")}");
			if (tech.Prerequisites.Count > 0) article.StatLines.Add($"Prerequisites: {Join(tech.Prerequisites.Select(t => t.Name))}");
			if (tech.RequiredForEraAdvancement) article.StatLines.Add("Required for era advancement");
			if (tech.BonusTechToFirstCivThatResearches) article.StatLines.Add("Free tech for the first civ to research it");
			if (tech.EnablesBridges) article.StatLines.Add("Allows roads to act as bridges");
			if (tech.DoublesWealthProduction) article.StatLines.Add("Doubles wealth production");

			articles.Add(article);
		}
		return articles;
	}

	private static List<CodexArticle> BuildCivilizations(GameData gameData) {
		List<CodexArticle> articles = new();
		foreach (Civilization civ in gameData.Civilizations) {
			if (string.IsNullOrEmpty(civ.name) || civ.isBarbarian) {
				continue;
			}

			CodexArticle article = NewArticle(gameData, civ.civilopediaEntry, civ.name);
			article.Icon = TryLoad(() => TextureLoader.Load("leader_heads", civ, useCache: true));

			if (!string.IsNullOrEmpty(civ.leader)) article.StatLines.Add($"Leader: {civ.leader}");
			if (!string.IsNullOrEmpty(civ.adjective)) article.StatLines.Add($"Adjective: {civ.adjective}");
			if (!string.IsNullOrEmpty(civ.noun)) article.StatLines.Add($"Noun: {civ.noun}");
			if (civ.traits.Count > 0) article.StatLines.Add($"Traits: {Join(civ.traits.Select(t => t.ToString()))}");

			articles.Add(article);
		}
		return articles;
	}

	private static List<CodexArticle> BuildGovernments(GameData gameData) {
		List<CodexArticle> articles = new();
		foreach (Government gov in gameData.governments) {
			if (string.IsNullOrEmpty(gov.name)) {
				continue;
			}

			CodexArticle article = NewArticle(gameData, gov.civilopediaEntry, gov.name);

			article.StatLines.Add($"Corruption: {gov.corruptionType}");
			article.StatLines.Add($"Hurry production: {PrettifyHurry(gov.hurryingType)}");
			if (gov.draftLimit > 0) article.StatLines.Add($"Draft limit: {gov.draftLimit}");
			if (gov.militaryPoliceLimit > 0) article.StatLines.Add($"Military police limit: {gov.militaryPoliceLimit}");
			article.StatLines.Add($"Worker rate: {gov.workerRate}");
			if (gov.allUnitsFree) {
				article.StatLines.Add("All units free");
			} else {
				article.StatLines.Add($"Free units (town/city/metropolis): {gov.freeUnitsPerTown}/{gov.freeUnitsPerCity}/{gov.freeUnitsPerMetropolis}");
				if (gov.unitCost > 0) article.StatLines.Add($"Unit support cost: {gov.unitCost} gold/turn");
			}

			articles.Add(article);
		}
		return articles;
	}

	private static List<CodexArticle> BuildTerrain(GameData gameData) {
		List<CodexArticle> articles = new();
		foreach (TerrainType terrain in gameData.terrainTypes) {
			if (string.IsNullOrEmpty(terrain.DisplayName)) {
				continue;
			}

			// Terrain types do not carry a civilopedia key, so these render as
			// generated stats only.
			CodexArticle article = new() {
				Key = null,
				Name = terrain.DisplayName,
			};

			article.StatLines.Add($"Food: {terrain.baseFoodProduction}");
			article.StatLines.Add($"Shields: {terrain.baseShieldProduction}");
			article.StatLines.Add($"Commerce: {terrain.baseCommerceProduction}");
			article.StatLines.Add($"Movement cost: {terrain.movementCost}");
			if (terrain.defenseBonus.amount != 0) article.StatLines.Add($"Defense bonus: +{terrain.defenseBonus.amount * 100:0}%");
			if (terrain.impassable) article.StatLines.Add("Impassable");
			if (!terrain.allowCities) article.StatLines.Add("Cities cannot be built");

			articles.Add(article);
		}
		return articles;
	}

	private static List<CodexArticle> BuildResources(GameData gameData) {
		List<CodexArticle> articles = new();
		foreach (Resource resource in gameData.Resources) {
			if (string.IsNullOrEmpty(resource.Name) || resource.Key == "NONE") {
				continue;
			}

			CodexArticle article = NewArticle(gameData, resource.CivilopediaEntry, resource.Name);
			article.Icon = TryLoad(() => TextureLoader.Load("resources.large", resource, useCache: true));

			article.StatLines.Add($"Category: {PrettifyCategory(resource.Category)}");
			if (resource.FoodBonus != 0) article.StatLines.Add($"Food bonus: +{resource.FoodBonus}");
			if (resource.ShieldsBonus != 0) article.StatLines.Add($"Shields bonus: +{resource.ShieldsBonus}");
			if (resource.CommerceBonus != 0) article.StatLines.Add($"Commerce bonus: +{resource.CommerceBonus}");
			Tech prerequisite = gameData.GetTech(resource.Prerequisite);
			if (prerequisite != null) article.StatLines.Add($"Requires: {prerequisite.Name}");

			articles.Add(article);
		}
		return articles;
	}

	private static List<CodexArticle> BuildTerraforms(GameData gameData) {
		List<CodexArticle> articles = new();
		foreach (Terraform terraform in gameData.Terraforms) {
			if (string.IsNullOrEmpty(terraform.Name)) {
				continue;
			}

			CodexArticle article = NewArticle(gameData, terraform.CivilopediaEntry, terraform.Name);

			if (terraform.TurnsToComplete > 0) article.StatLines.Add($"Turns to complete: {terraform.TurnsToComplete}");
			Tech prerequisite = gameData.GetTech(terraform.RequiredTech);
			if (prerequisite != null) article.StatLines.Add($"Requires: {prerequisite.Name}");
			if (terraform.RequiredResources.Count > 0) article.StatLines.Add($"Resources: {Join(terraform.RequiredResources.Select(r => r.Name))}");
			if (terraform.Improvement != null) article.StatLines.Add($"Creates: {PrettifyKey(terraform.Improvement.key, null)}");

			articles.Add(article);
		}
		return articles;
	}

	private static List<CodexArticle> BuildGameConcepts(GameData gameData) {
		List<CodexArticle> articles = new();
		if (gameData.Codex == null) {
			return articles;
		}

		foreach (string key in gameData.Codex.GameConceptKeys) {
			CodexEntry entry = gameData.Codex.GetEntry(key);
			if (entry == null) {
				continue;
			}

			CodexArticle article = new() {
				Key = key,
				Name = string.IsNullOrEmpty(entry.DisplayName) ? PrettifyKey(key, "GCON_") : entry.DisplayName,
			};
			if (!string.IsNullOrWhiteSpace(entry.Body)) {
				article.Pages.Add(entry.Body);
			}
			if (!string.IsNullOrWhiteSpace(entry.Description)) {
				article.Pages.Add(entry.Description);
			}
			articles.Add(article);
		}
		return articles;
	}

	private static CodexArticle NewArticle(GameData gameData, string key, string name) {
		CodexArticle article = new() {
			Key = key,
			Name = name,
		};

		CodexEntry entry = gameData.Codex?.GetEntry(key);
		if (entry != null) {
			if (!string.IsNullOrEmpty(entry.DisplayName)) {
				article.Name = entry.DisplayName;
			}
			if (!string.IsNullOrWhiteSpace(entry.Body)) {
				article.Pages.Add(entry.Body);
			}
			if (!string.IsNullOrWhiteSpace(entry.Description)) {
				article.Pages.Add(entry.Description);
			}
		}

		return article;
	}

	private static ImageTexture TryLoad(Func<ImageTexture> loader) {
		try {
			return loader();
		} catch (Exception) {
			// Missing or modded icon art; the Codex renders without an icon.
			return null;
		}
	}

	private static string Join(IEnumerable<string> values) {
		return string.Join(", ", values.Where(v => !string.IsNullOrEmpty(v)));
	}

	private static string PrettifyKey(string key, string prefix) {
		if (string.IsNullOrEmpty(key)) {
			return "";
		}
		if (prefix != null && key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
			key = key.Substring(prefix.Length);
		}
		return key.Replace('_', ' ');
	}

	private static string PrettifyHurry(Government.HurryProductionType type) {
		return type switch {
			Government.HurryProductionType.CannotHurry => "Cannot hurry",
			Government.HurryProductionType.ForcedLabor => "Forced labor",
			Government.HurryProductionType.PaidLabor => "Paid labor",
			_ => type.ToString(),
		};
	}

	private static string PrettifyCategory(ResourceCategory category) {
		return category switch {
			ResourceCategory.BONUS => "Bonus",
			ResourceCategory.LUXURY => "Luxury",
			ResourceCategory.STRATEGIC => "Strategic",
			ResourceCategory.NONE => "None",
			_ => category.ToString(),
		};
	}
}
