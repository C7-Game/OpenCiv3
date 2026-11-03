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

	// The tech currently selected in the advisor. Starts as the player's
	// researched tech each time the advisor opens; changing it here must not
	// touch the player (selecting a different tech resets their beakers).
	// Selections only reach the engine when the advisor is closed.
	private ID selectedTech;

	// What the research queue would be if the pending selections were applied.
	private List<Tech> pendingQueue = new();

	// Clicks made since the advisor opened, replayed to the engine on close.
	private readonly List<(Tech tech, SelectionMode mode)> pendingPicks = new();

	private PopupOverlay popupOverlay;

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
		_close.Pressed += () => { this.GetParent<Advisors>().OnHide(); };
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
			// ShowAdvisor runs before Show(), so Visible means we're already open
			// and shouldn't throw away selections made so far.
			if (!Visible) {
				selectedTech = player.currentlyResearchedTech;
				pendingQueue = player.ResearchQueue.ToList();
				pendingPicks.Clear();
			}
			eraName = string.IsNullOrEmpty(lastOpenedEra) ? player.eraCivilopediaName : lastOpenedEra;
			this.DrawTechTree(eraName, player, allTechs, player.GetAvailableTechsToResearch(allTechs));
		});
	}

	void DrawTechTree(string eraName, Player player, List<Tech> allTechs, List<Tech> availableTechsToResearch) {
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

		List<Tech> queue = pendingQueue;

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
			} else if (selectedTech == tech.id) {
				techState = TechBox.TechState.kInProgress;
			} else if (queue.Count > 0 && queue.Contains(tech)) {
				techState = TechBox.TechState.kQueued;
			} else if (availableTechsToResearch.Contains(tech)) {
				techState = TechBox.TechState.kPossible;
			} else {
				techState = TechBox.TechState.kBlocked;
			}

			int queueNumber = queue.IndexOf(tech) + 1;

			TechBox techButton = new(tech, techState, queueNumber);
			techButton.SetPosition(new Vector2(tech.X, tech.Y));
			techButton.Pressed += () => {
				SelectionMode selection = Input.IsKeyPressed(Key.Shift) ? SelectionMode.Multi : SelectionMode.Single;
				ChoosePendingTech(tech, selection);
			};
			background.AddChild(techButton);
			techBoxes.Add(techButton);
		}
	}

	public void SetPopupOverlay(PopupOverlay po) {
		popupOverlay = po;
	}

	// Records a click locally and redraws; the player is untouched until the advisor closes.
	private void ChoosePendingTech(Tech tech, SelectionMode mode) {
		EngineStorage.ReadGameData((GameData gameData) => {
			Player player = gameData.GetFirstHumanPlayer();

			// Mirror the checks MsgChooseResearch will make when we replay this click.
			bool isTechEraBeyondPlayerEra = GetEraIndex(tech.EraCivilopediaName) > GetEraIndex(player.eraCivilopediaName);
			if (player.knownTechs.Contains(tech.id) || isTechEraBeyondPlayerEra) {
				return;
			}
			if (selectedTech == tech.id && pendingQueue.Count == 1) {
				return;
			}

			List<Tech> required = player.PreviewResearchQueueFor(tech);
			if (mode == SelectionMode.Single) {
				// A single pick replaces the whole queue, so earlier picks no longer matter.
				// Replaying them would reset the beakers of a tech we ended up back on.
				pendingPicks.Clear();
				pendingQueue = required;
			} else {
				pendingQueue.AddRange(required.Where(t => !pendingQueue.Contains(t)));
			}
			pendingPicks.Add((tech, mode));
			if (pendingQueue.Count > 0) {
				selectedTech = pendingQueue[0].id;
			}

			DrawTechTree(eraName, player, gameData.techs, player.GetAvailableTechsToResearch(gameData.techs));
		});
	}

	/// <summary>
	/// If closing would switch away from research in progress, asks the player to
	/// confirm. Returns true if the confirmation popup was shown, in which case the
	/// advisor must stay open; onConfirmed runs after the selections are committed.
	/// </summary>
	public bool AskToConfirmSwitch(System.Action onConfirmed) {
		if (pendingPicks.Count == 0 || popupOverlay == null) {
			return false;
		}

		bool asked = false;
		EngineStorage.ReadGameData((GameData gameData) => {
			Player player = gameData.GetFirstHumanPlayer();
			ID current = player.currentlyResearchedTech;
			// With nothing spent there's nothing to lose, so no need to ask.
			if (current == null || selectedTech == current || player.beakers == 0) {
				return;
			}

			Tech oldTech = gameData.techs.Find(t => t.id == current);
			Tech newTech = gameData.techs.Find(t => t.id == selectedTech);
			popupOverlay.ShowPopup(
				new ResearchSwitchConfirmation(player, oldTech, newTech, player.beakers, () => {
					CommitPendingResearch();
					onConfirmed();
				}),
				PopupOverlay.PopupCategory.Advisor);
			asked = true;
		});
		return asked;
	}

	/// <summary>
	/// Sends the selections made in the advisor to the engine, in click order.
	/// Returns whether anything was sent.
	/// </summary>
	public bool CommitPendingResearch() {
		if (pendingPicks.Count == 0) {
			return false;
		}
		foreach ((Tech tech, SelectionMode mode) in pendingPicks) {
			new MsgChooseResearch(tech, AdvisorState.DontShow, mode).send();
		}
		pendingPicks.Clear();
		return true;
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
