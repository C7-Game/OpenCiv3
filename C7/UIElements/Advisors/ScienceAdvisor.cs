using C7Engine;
using C7GameData;
using Godot;
using System.Collections.Generic;
using System.Linq;
using static C7Engine.MsgChooseResearch;
using static C7GameData.EraUtils;

[GlobalClass]
[Tool]
public partial class ScienceAdvisor : Control {

	[Export] public TextureRect background;

	private ImageTexture AncientBackground;
	private ImageTexture MiddleBackground;
	private ImageTexture IndustrialBackground;
	private ImageTexture ModernBackground;

	private TextureButton _close;
	private TextureRect _advisorHead;
	private TextureButton _dialogBox;
	private Label _dialogBoxLabel;

	private TextureButton nextEra;
	private TextureButton previousEra;
	private List<TechBox> techBoxes = new();

	// Stored separately so we can modify this without mutating the player.
	private string eraName;

	// store the last opened era window so next time we open the advisor, it opens at the same era window
	private static string lastOpenedEra = string.Empty;

	public ScienceAdvisor() {
		MouseFilter = MouseFilterEnum.Stop;
	}

	public override void _Ready() {
		this.CreateUI();
	}

	private void CreateUI() {
		// science_industrial_new is used as the industrial tech tree is
		// different from vanilla civ3.
		AncientBackground = TextureLoader.Load("advisors.science.background.ancient");
		MiddleBackground = TextureLoader.Load("advisors.science.background.middle");
		IndustrialBackground = TextureLoader.Load("advisors.science.background.industrial");
		ModernBackground = TextureLoader.Load("advisors.science.background.modern");

		_advisorHead = AdvisorUtils.CreateAdvisorHead(background, AdvisorHead.Advisor.Science);
		_close = AdvisorUtils.CreateExitButton(background);
		_close.Pressed += () => { this.GetParent<Advisors>().Hide(); };
		(_dialogBox, _dialogBoxLabel) = AdvisorUtils.CreateAdvisorDialogBox(background);

		AdvisorUtils.CreateAdvisorTitle(background, AncientBackground.GetWidth(), "SCIENCE ADVISOR");

		CreatePreviousEraButton();
		CreateNextEraButton();
	}

	private void CreatePreviousEraButton() {
		previousEra = new();
		TextureLoader.SetButtonTextures(previousEra, "advisors.science.navigation.button");
		previousEra.SetPosition(new Vector2(512 - 128 - 100, 720));
		background.AddChild(previousEra);
		previousEra.Pressed += () => { ChangeEraAndDrawTree(-1); };

		TextureRect leftArrow = new() {
			Texture = TextureLoader.Load("advisors.science.navigation.arrow_previous")
		};
		previousEra.AddChild(leftArrow);
		leftArrow.SetPosition(new Vector2(-44, 13));

		Label previousEraLabel = new();
		previousEra.AddChild(previousEraLabel);
		previousEraLabel.SetTextAndCenterLabel("Previous Era");
		previousEraLabel.Position += new Vector2(0, 7);
	}

	private void CreateNextEraButton() {
		nextEra = new();
		TextureLoader.SetButtonTextures(nextEra, "advisors.science.navigation.button");
		nextEra.SetPosition(new Vector2(512 + 100, 720));
		background.AddChild(nextEra);
		nextEra.Pressed += () => { ChangeEraAndDrawTree(1); };

		TextureRect rightArrow = new() {
			Texture = TextureLoader.Load("advisors.science.navigation.arrow_next")
		};
		nextEra.AddChild(rightArrow);
		rightArrow.SetPosition(new Vector2(129, 13));

		Label nextEraLabel = new();
		nextEra.AddChild(nextEraLabel);
		nextEraLabel.SetTextAndCenterLabel("Next Era");
		nextEraLabel.Position += new Vector2(0, 7);
	}

	private void LoadTechTree() {
		EngineStorage.ReadGameData((GameData gameData) => {
			List<Tech> allTechs = gameData.techs;
			Player player = gameData.GetFirstHumanPlayer();
			eraName = string.IsNullOrEmpty(lastOpenedEra) ? player.eraCivilopediaName : lastOpenedEra;
			this.DrawTechTree(eraName, player, allTechs, player.GetAvailableTechsToResearch(allTechs));
		});
	}

	void DrawTechTree(string eraName, Player player, List<Tech> allTechs, HashSet<Tech> availableTechsToResearch) {
		// clear all tech-box items so we don't draw new ones over the old ones
		foreach (TechBox tb in techBoxes) {
			background.RemoveChild(tb);
			tb.QueueFree();
		}
		techBoxes.Clear();

		HashSet<ID> knownTechs = player.knownTechs;
		previousEra.Show();
		nextEra.Show();

		lastOpenedEra = eraName;

		Queue<Tech> queue = player.ResearchQueue;

		// Set the tech background based on the player's era.
		if (eraName == ANCIENT_TIMES_CVLPD) {
			previousEra.Hide();
			background.Texture = AncientBackground;
		} else if (eraName == MIDDLE_AGES_CVLPD) {
			background.Texture = MiddleBackground;
		} else if (eraName == INDUSTRIAL_AGE_CVLPD) {
			background.Texture = IndustrialBackground;
		} else if (eraName == MODERN_ERA_CVLPD) {
			background.Texture = ModernBackground;
			nextEra.Hide();
		}
		_advisorHead.Texture = AdvisorHead.GetPopupImage(AdvisorHead.Advisor.Science, AdvisorHead.Mood.Happy, player.EraIndex());

		foreach (Tech tech in allTechs) {
			if (tech.EraCivilopediaName != eraName) {
				continue;
			}

			TechBox.TechState techState = TechBox.TechState.kBlocked;
			if (knownTechs.Contains(tech.id)) {
				techState = TechBox.TechState.kKnown;
			} else if (player.currentlyResearchedTech == tech.id) {
				techState = TechBox.TechState.kInProgress;
			} else if (queue.Count > 0 && queue.Contains(tech)) {
				techState = TechBox.TechState.kQueued;
			} else if (availableTechsToResearch.Contains(tech)) {
				techState = TechBox.TechState.kPossible;
			} else {
				techState = TechBox.TechState.kBlocked;
			}

			int queueNumber = queue.ToList().IndexOf(tech) + 1;

			TechBox techButton = new(tech, techState, queueNumber);
			techButton.SetPosition(new Vector2(tech.X, tech.Y));
			techButton.Pressed += () => {
				SelectionMode selection = Input.IsKeyPressed(Key.Shift) ? SelectionMode.Multi : SelectionMode.Single;
				new MsgChooseResearch(tech, AdvisorState.Show, selection).send();
			};
			background.AddChild(techButton);
			techBoxes.Add(techButton);
		}
	}

	private void ChangeEraAndDrawTree(int delta) {
		foreach (TechBox tb in techBoxes) {
			background.RemoveChild(tb);
			tb.QueueFree();
		}
		techBoxes.Clear();

		EngineStorage.ReadGameData((GameData gameData) => {
			List<Tech> allTechs = gameData.techs;
			Player player = gameData.GetFirstHumanPlayer();
			eraName = string.IsNullOrEmpty(lastOpenedEra)
				? EraIndexToEra(GetEraIndex(eraName) + delta)
				: EraIndexToEra(GetEraIndex(lastOpenedEra) + delta);
			DrawTechTree(eraName, player, allTechs, player.GetAvailableTechsToResearch(allTechs));
		});
	}


	public void ShowAdvisor() {
		LoadTechTree();

		Show();

		EngineStorage.ReadGameData((GameData gameData) => {
			Player player = gameData.GetFirstHumanPlayer();

			// TODO: Choose advisor head
			// _advisorHead.Texture = AdvisorHead.GetPopupImage(AdvisorHead.Advisor.Science, AdvisorHead.Mood.Happy, player.EraIndex());
		});
	}
}
