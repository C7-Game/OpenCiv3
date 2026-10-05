using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using Godot;

[GlobalClass]
[Tool]
public partial class VictoryStatusView : Control {

	[Export] public TextureRect background;
	[Export] public GridContainer gridHeader;
	[Export] public GridContainer grid;

	private TextureButton _close;

	private const float LabelColumnWidth = 185f;
	private const float ValueColumnWidth = 40f;

	private const int GridHeaderColumns = 3;
	private const int GridColumns = 6;

	public VictoryStatusView() {
		MouseFilter = MouseFilterEnum.Stop;
	}

	public override void _Ready() {
		CreateUI();
		ConfigureGrid();
	}

	private void CreateUI() {
		background.Texture = TextureLoader.Load("screens.standing.victory_status.background");
		var histogramTexture = TextureLoader.Load("screens.standing.histogram.background");

		_close = AdvisorUtils.CreateExitButton(background);
		_close.Pressed += () => { this.GetParent<GameViews>().Hide(); };

		AdvisorUtils.CreateAdvisorTitle(background, background.Texture.GetWidth(), "VICTORY STATUS");
	}

	private void ConfigureGrid() {
		gridHeader.Columns = GridHeaderColumns;
		gridHeader.AddThemeConstantOverride("h_separation", 0); // horizontal gap between columns
		gridHeader.AddThemeConstantOverride("v_separation", 5);

		grid.Columns = GridColumns;
		grid.AddThemeConstantOverride("h_separation", 0); // horizontal gap between columns
		grid.AddThemeConstantOverride("v_separation", 5);
	}

	public void ShowView() {
		Show();

		EngineStorage.ReadGameData(DrawGrid);
	}

	private void DrawGrid(GameData gameData) {
		ClearGrid();

		Player player = gameData.GetFirstHumanPlayer();
		List<Player> rivals = gameData.GetKnownRivals(player);

		// Render

		AddTitleRow("To Win", player.civilization.name, "Top Rival");

		foreach (IVictory vc in gameData.victories) {
			ProcessVictoryCondition(vc, gameData, player, rivals);
		}
	}

	private void ProcessVictoryCondition(IVictory dv, GameData gameData, Player player, List<Player> rivals) {
		VictoryStatus status = dv.Evaluate(player, gameData);
		List<VictoryStatus> rivalStatuses = rivals.Select(r => dv.Evaluate(r, gameData)).ToList();

		AddHeaderRow(dv.Header());

		foreach (string[] output in dv.GenerateStatusRows(status, rivalStatuses)) {
			AddDataRow(output);
		}
	}

	private void ClearGrid() {
		foreach (Node child in gridHeader.GetChildren())
			child.QueueFree();

		foreach (Node child in grid.GetChildren())
			child.QueueFree();
	}

	// TODO: Make more use of Godot UI instead of character-base dynamic hackery

	private const string HeaderPadding = "     ";
	private const string Padding = "      ";

	/// Titles; column headers
	public void AddTitleRow(params string[] values) {
		for (int i = 0; i < GridHeaderColumns; i++) {
			var label = MakeLabel(values[i] + HeaderPadding, ValueColumnWidth, HorizontalAlignment.Right);
			label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			label.AddThemeFontSizeOverride("font_size", 18);
			gridHeader.AddChild(label);
		}
	}

	/// A bold, full-width section header row (e.g. "Domination", "Cultural").
	public void AddHeaderRow(string text) {
		var label = MakeLabel(text, LabelColumnWidth, HorizontalAlignment.Left);
		label.AddThemeFontSizeOverride("font_size", 20);
		grid.AddChild(label);

		for (int i = 1; i < GridColumns; i++) {
			bool isLabelCol = i % 2 == 0;
			grid.AddChild(MakeSpacer(isLabelCol ? LabelColumnWidth : ValueColumnWidth));
		}
	}

	private Label MakeLabel(string text, float width, HorizontalAlignment align) {
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = align,
			CustomMinimumSize = new Vector2(width, 0),
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin // don't stretch beyond min width
		};
		return label;
	}

	private Control MakeSpacer(float width) {
		return new Control { CustomMinimumSize = new Vector2(width, 0) };
	}

	/// A data row: label, value; label, value; label, value
	public void AddDataRow(params string[] values) {
		for (int i = 0; i < GridColumns; i++) {
			string valueText = i < values.Length ? values[i] : "";
			bool isLabelCol = i % 2 == 0;

			var valueNode = new Label
			{
				Text = isLabelCol ? valueText : valueText + Padding,
				HorizontalAlignment = isLabelCol ? HorizontalAlignment.Left : HorizontalAlignment.Right,
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			if (isLabelCol) {
				valueNode.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			}

			grid.AddChild(valueNode);
		}
	}
}
