using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData.Save;
using C7Engine;
using C7Engine.Lua;

namespace C7GameData;

public class Terraform {
	public struct ScriptContext(Player player, Tile tile, Terraform terraform) {
		public Player player = player;
		public Tile tile = tile;
		public Terraform terraform = terraform;
	}

	public ID Id;
	public string Name;
	public string CivilopediaEntry;
	public int TurnsToComplete;
	public ID RequiredTech;

	public List<Resource> RequiredResources = [];

	public TerrainImprovement Improvement;

	private Action<ScriptContext> Effect;
	private List<Func<ScriptContext, bool>> ActionValidators = [];
	private Func<ScriptContext, int> AIScore;

	public readonly MapUnit.AnimatedAction? Animation;

	// Key of the UI action associated with the Terraform
	public readonly string UIAction;

	// Path to the texture definition in the texture config
	public readonly string ButtonTexture;

	private SaveTerraform dataSource;

	public Terraform(SaveTerraform saveTerraform, GameData gameData) {
		Id = saveTerraform.Id;
		Name = saveTerraform.Name;
		CivilopediaEntry = saveTerraform.CivilopediaEntry;
		TurnsToComplete = saveTerraform.TurnsToComplete;
		RequiredTech = saveTerraform.RequiredTech;
		Animation = saveTerraform.Animation;
		UIAction = saveTerraform.UIAction;
		ButtonTexture = saveTerraform.ButtonTexture;
		dataSource = saveTerraform;
		RequiredResources = saveTerraform.RequiredResources.ConvertAll(resKey => gameData.Resources.Find(res => res.Key == resKey));

		if (saveTerraform.Improvement != null)
			Improvement = gameData.terrainImprovements.Find(ti => ti.key == saveTerraform.Improvement);

		SetRules(gameData.luaBehaviorEngine);
	}

	public string ToString() {
		return Name;
	}

	private void SetRules(BehaviorEngine engine) {
		foreach (string functionPath in dataSource.Effects) {
			Effect += engine.ImportFunc<Action<ScriptContext>>(functionPath);
		}

		foreach (string functionPath in dataSource.Validators) {
			ActionValidators.Add(engine.ImportFunc<Func<ScriptContext, bool>>(functionPath));
		}

		AIScore = engine.ImportFunc<Func<ScriptContext, int>>(dataSource.AIScore);
	}

	public bool MeetsRequirements(Player player, Tile tile) {
		var hasTech = player.HasTech(RequiredTech);

		bool hasResources = RequiredResources.All(
			res => EngineStorage.gameData.GetTradeNetwork().HasTradeAccess(tile, player, res)
		);

		bool canAddImprovement = Improvement == null || tile.overlays.CanAdd(tile, Improvement);

		return hasTech && hasResources
				&& canAddImprovement && ActionValidators.All(func => func(new(player, tile, this)));
	}

	public void OnComplete(Player player, Tile tile) {
		Effect?.Invoke(new(player, tile, this));

		if (Improvement != null) {
			if (!tile.overlays.CanAdd(tile, Improvement))
				throw new InvalidOperationException($"Cannot add {Improvement.key} to the tile {tile}");
			tile.overlays.Add(Improvement);

			var cityTile = tile.neighbors.FirstOrDefault(n => n.Value.HasCity()).Value;
			if (Improvement.layer == TerrainImprovement.Layer.Roads && cityTile != null) {
				if (this.MeetsRequirements(cityTile.cityAtTile.owner, cityTile))
					cityTile.overlays.Add(Improvement);
			}

			// update all the players' knowledge that can actively see this tile
			EngineStorage.ReadGameData(data => {
				foreach (Player dataPlayer in data.players.Where(dataPlayer => dataPlayer.tileKnowledge.activeTiles.Contains(tile))) {
					dataPlayer.tileKnowledge.knownTerrainImprovements[tile.Id] = tile.overlays.GetImprovements().Select(ti => ti.key).ToList();
				}
			});
		}
	}

	public int CalculateAIScore(Player player, Tile tile) {
		return AIScore(new(player, tile, this));
	}

	public SaveTerraform ToSaveTerraform() {
		return dataSource;
	}

	public bool ProvidesRoad() {
		if (Improvement == null) return false;

		return Improvement.layer == TerrainImprovement.Layer.Roads && Improvement.upgradesFrom == null;
	}
}
