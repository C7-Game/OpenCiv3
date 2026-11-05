using System.Linq;
using Godot;
using Serilog;
using C7Engine;
using C7GameData;

/**
 * Handles managing the advisor screens.
 * Showing them, hiding them... maybe some other things eventually.
 * This is part of the effort to de-centralize from Game.cs and be more event driven.
 */
public partial class Advisors : CenterContainer {
	private ILogger log = LogManager.ForContext<Advisors>();

	private string latest = C7Action.ShowDomesticAdvisor;

	[Export] public DomesticAdvisor domesticAdvisor;
	[Export] public TradeAdvisor tradeAdvisor;
	[Export] public MilitaryAdvisor militaryAdvisor;
	[Export] public ForeignAdvisor foreignAdvisor;
	[Export] public CulturalAdvisor culturalAdvisor;
	[Export] public ScienceAdvisor scienceAdvisor;

	public override void _Ready() {
		Hide();
	}

	private void OnShowGameView(string gameView) {
		HideAdvisors();
		Hide();
	}
	private void OnShowGameView() {
		OnShowGameView(null);
	}

	private void ShowLatestAdvisor() {
		OnShowSpecificAdvisor(latest);
	}

	private void OnShowSpecificAdvisor(string advisorType) {
		if (advisorType != latest) {
			latest = advisorType;
			HideAdvisors();
		}

		switch (advisorType) {
			case C7Action.ShowDomesticAdvisor:
				domesticAdvisor.ShowAdvisor();
				break;
			case C7Action.ShowTradeAdvisor:
				tradeAdvisor.ShowAdvisor();
				break;
			case C7Action.ShowForeignAdvisor:
				foreignAdvisor.ShowAdvisor();
				break;
			case C7Action.ShowMilitaryAdvisor:
				militaryAdvisor.ShowAdvisor();
				break;
			case C7Action.ShowCulturalAdvisor:
				culturalAdvisor.ShowAdvisor();
				break;
			case C7Action.ShowScienceAdvisor:
				scienceAdvisor.ShowAdvisor();
				break;
			default:
				log.Warning("Unknown advisor type: " + advisorType);
				break;
		}

		Show();
	}

	private Control[] AllAdvisors() {
		return new Control[] { domesticAdvisor, tradeAdvisor, militaryAdvisor, foreignAdvisor, culturalAdvisor, scienceAdvisor };
	}

	private void HideAdvisors() {
		foreach (Control advisor in AllAdvisors()) {
			advisor.Hide();
		}
	}

	/// <summary>
	/// The one way out of an advisor: every route that leaves the open advisor (X, Escape, a function
	/// key) goes through here. The advisor decides whether it may close; onClosed runs once it does.
	/// </summary>
	/// <param name="opening">The advisor about to be shown, if any. Reopening the one already showing isn't leaving it.</param>
	public void RequestClose(System.Action onClosed, string opening = null) {
		Control visible = AllAdvisors().FirstOrDefault(a => a.Visible);
		if (opening == latest || visible is not IGuardedView guarded) {
			onClosed();
			return;
		}
		guarded.RequestClose(onClosed);
	}

	public void OnHide() {
		RequestClose(OnShowGameView);
	}
}
