using C7Engine;
using C7GameData;
using Godot;
using System.Collections.Generic;
using System.Linq;
using C7.UIElements;
using static C7Engine.MsgChangeSliders;

[GlobalClass]
[Tool]
public partial class DomesticAdvisor : Control {
	[Export] TextureRect background;
	[Export] TextureButton close;
	[Export] TextureButton changeGovernment;
	[Export] Label governmentLabel;
	[Export] Label scienceStatus;
	[Export] Label treasury;
	[Export] Label incomeDetails;
	[Export] Label expenseDetails;
	[Export] Label incomeSummary;
	[Export] Label expenseSummary;
	[Export] Label sumSummary;
	[Export] Label growth;
	[Export] VBoxContainer cityListContainer;
	[Export] TextureButton eatenFood;
	[Export] TextureButton fullFood;
	[Export] TextureButton wastedShield;
	[Export] TextureButton goodShield;
	[Export] TextureButton wastedGold;
	[Export] TextureButton goodGold;
	[Export] TextureButton happyFace;
	[Export] TextureButton contentFace;
	[Export] TextureButton beaker;
	[Export] TextureButton treasuryIcon;
	[Export] Civ3HSlider scienceSlider;
	[Export] Civ3HSlider luxurySlider;

	PopupOverlay popupOverlay;

	Label scienceSliderLabel = new();
	Label luxurySliderLabel = new();

	TextureRect advisorHead = new();
	Label DialogBoxAdvise = new();

	// The Y position of the two sliders.
	private int scienceSliderY = 84;
	private int luxurySliderY = 130;

	private Player playerController = null;

	public DomesticAdvisor() {
		MouseFilter = MouseFilterEnum.Stop;
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		this.CreateUI();
	}

	private void CreateUI() {
		ImageTexture DomesticBackground = TextureLoader.Load("advisors.domestic.background");
		background.Texture = DomesticBackground;

		AdvisorUtils.CreateAdvisorTitle(background, background.Texture.GetWidth(), "DOMESTIC ADVISOR");

		advisorHead.Texture = AdvisorHead.GetPopupImage(AdvisorHead.Advisor.Domestic, AdvisorHead.Mood.Happy, eraIndex: 0);
		advisorHead.SetPosition(new Vector2(851, 0));
		background.AddChild(advisorHead);

		ImageTexture DialogBoxTexture = TextureLoader.Load("advisors.dialog_box");
		TextureButton DialogBox = new TextureButton();
		DialogBox.TextureNormal = DialogBoxTexture;
		DialogBox.SetPosition(new Vector2(806, 110));
		background.AddChild(DialogBox);

		//TODO: Multi-line capabilities
		DialogBoxAdvise.Text = "You are running OpenCiv3!";
		DialogBoxAdvise.SetPosition(new Vector2(815, 119));
		background.AddChild(DialogBoxAdvise);

		TextureLoader.SetButtonTextures(close, "ui.exit");
		close.Pressed += () => {
			GetParent<Advisors>().Hide();
		};

		TextureLoader.SetButtonTextures(changeGovernment, "advisors.domestic.button");
		changeGovernment.Pressed += ChangeGovernments;

		ImageTexture scienceSliderTexture = TextureLoader.Load("icons.science");
		ImageTexture luxurySliderTexture = TextureLoader.Load("icons.luxury");

		// Placeholder values
		int scienceRate = 5;
		int luxuryRate = 5;

		scienceSlider.Value = scienceRate;

		scienceSlider.rangeTheme
			.AddGrabber(scienceSliderTexture)
			.AddGrabberHighlight(scienceSliderTexture)
			.AddSliderStyleBox(new StyleBoxEmpty());

		scienceSlider.ValueChanged += value => {
			UpdateScienceSlider((int)value);
		};

		luxurySlider.Value = luxuryRate;

		luxurySlider.rangeTheme
			.AddGrabber(luxurySliderTexture)
			.AddGrabberHighlight(luxurySliderTexture)
			.AddSliderStyleBox(new StyleBoxEmpty());

		luxurySlider.ValueChanged += value => {
			UpdateLuxurySlider((int)value);
		};

		scienceSliderLabel.Text = $"{scienceRate * 10}%";
		scienceSliderLabel.SetPosition(new Vector2(760, scienceSliderY + 5));
		background.AddChild(scienceSliderLabel);

		luxurySliderLabel.Text = $"{luxuryRate * 10}%";
		luxurySliderLabel.SetPosition(new Vector2(760, luxurySliderY + 4));
		background.AddChild(luxurySliderLabel);

		var plusConfigKey = "advisors.domestic.plus";
		var minusConfigKey = "advisors.domestic.minus";

		TextureButton moreScience = new();
		TextureLoader.SetButtonTextures(moreScience, plusConfigKey);
		moreScience.SetPosition(new Vector2(732, scienceSliderY + 27));
		moreScience.Pressed += MoreScience;
		background.AddChild(moreScience);

		TextureButton lessScience = new();
		TextureLoader.SetButtonTextures(lessScience, minusConfigKey);
		lessScience.SetPosition(new Vector2(562, scienceSliderY + 29));
		lessScience.Pressed += LessScience;
		background.AddChild(lessScience);

		TextureButton moreLuxury = new();
		TextureLoader.SetButtonTextures(moreLuxury, plusConfigKey);
		moreLuxury.SetPosition(new Vector2(732, luxurySliderY - 5));
		moreLuxury.Pressed += MoreLuxury;
		background.AddChild(moreLuxury);

		TextureButton lessLuxury = new();
		TextureLoader.SetButtonTextures(lessLuxury, minusConfigKey);
		lessLuxury.SetPosition(new Vector2(562, luxurySliderY - 3));
		lessLuxury.Pressed += LessLuxury;
		background.AddChild(lessLuxury);

		// Column header icons.
		eatenFood.TextureNormal = TextureLoader.Load("icons.eaten_food");
		fullFood.TextureNormal = TextureLoader.Load("icons.full_food");
		wastedShield.TextureNormal = TextureLoader.Load("icons.wasted_shield");
		goodShield.TextureNormal = TextureLoader.Load("icons.good_shield");
		wastedGold.TextureNormal = TextureLoader.Load("icons.wasted_gold");
		goodGold.TextureNormal = TextureLoader.Load("icons.good_gold");
		happyFace.TextureNormal = TextureLoader.Load("icons.happy_face");
		contentFace.TextureNormal = TextureLoader.Load("icons.content_face");
		beaker.TextureNormal = TextureLoader.Load("icons.beaker");
		treasuryIcon.TextureNormal = TextureLoader.Load("icons.treasury");
	}

	private void UpdateScienceSlider(int value) {
		int scienceRate = playerController.scienceRate;

		if (value == scienceRate)
			return;

		if (value > scienceRate) {
			MoreScience();
		} else {
			LessScience();
		}
	}

	private void UpdateLuxurySlider(int value) {
		int luxuryRate = playerController.luxuryRate;

		if (value == luxuryRate)
			return;

		if (value > luxuryRate) {
			MoreLuxury();
		} else {
			LessLuxury();
		}
	}

	private void MoreScience() {
		new MsgChangeSliders(playerController.id, DomesticPolicyChoice.MoreScience).send();
	}

	private void MoreLuxury() {
		new MsgChangeSliders(playerController.id, DomesticPolicyChoice.MoreLuxury).send();
	}
	private void LessScience() {
		new MsgChangeSliders(playerController.id, DomesticPolicyChoice.LessScience).send();
	}

	private void LessLuxury() {
		new MsgChangeSliders(playerController.id, DomesticPolicyChoice.LessLuxury).send();
	}

	public void ShowAdvisor() {
		Show();

		EngineStorage.ReadGameData((GameData gameData) => {
			playerController = gameData.players.First(p => p.id == EngineStorage.uiControllerID);
			PlayerCommerceBreakdown totalIncome = playerController.AggregateFlows();

			int scienceRate = playerController.scienceRate;
			int luxuryRate = playerController.luxuryRate;

			scienceSlider.Value = scienceRate;
			scienceSliderLabel.Text = $"{scienceRate * 10}%";
			luxurySlider.Value = luxuryRate;
			luxurySliderLabel.Text = $"{luxuryRate * 10}%";

			governmentLabel.Text = $"{playerController.government.name}";
			scienceStatus.Text = playerController.SummarizeScience(gameData);
			treasury.Text = $"Treasury: {playerController.gold}";

			incomeDetails.Text = $"From cities: +{totalIncome.CityInflows()}\nFrom taxmen: +{totalIncome.taxmenTaxes}\nFrom other civs: +{totalIncome.fromOtherCivs}\nFrom interest: +{totalIncome.interest}";
			expenseDetails.Text = $"-{totalIncome.beakers}: Science\n-{totalIncome.happiness}: Entertainment\n-{totalIncome.corrupted}: Corruption\n-{totalIncome.maintenance}: Maintenance\n-{totalIncome.unitSupport}: Unit costs\n-{totalIncome.toOtherCivs}: To other civs";
			incomeSummary.Text = $"Income: {totalIncome.Inflows()}";
			expenseSummary.Text = $"Expenses: {totalIncome.Outflows()}";

			int goldPerTurn = playerController.CalculateGoldPerTurn();
			if (goldPerTurn > 0) {
				sumSummary.Text = $"Net gain: +{goldPerTurn}";
				growth.Text = "Growing!";
			} else if (goldPerTurn < 0) {
				sumSummary.Text = $"Net loss: {goldPerTurn}";
				growth.Text = "Shrinking!";
			} else {
				sumSummary.Text = $"Neutral: {goldPerTurn}";
				growth.Text = "Balanced";
			}

			//TODO: Randomize or set logically
			advisorHead.Texture = AdvisorHead.GetPopupImage(AdvisorHead.Advisor.Domestic, AdvisorHead.Mood.Happy, playerController.EraIndex());

			// Disable the change government button unless we have a government to
			// switch to.
			changeGovernment.Disabled = playerController.GetAvailableGovernments(gameData).Count == 1;

			if (playerController.government.transitionType && playerController.inAnarchyUntilTurn > gameData.turn) {
				DialogBoxAdvise.Text = $"{playerController.inAnarchyUntilTurn - gameData.turn} turns of anarchy left";
			}

			foreach (var node in cityListContainer.GetChildren()) {
				cityListContainer.RemoveChild(node);
				node.QueueFree();
			}

			foreach (City city in playerController.cities) {
				cityListContainer.AddChild(MakeCityRow(city));
			}
		});
	}

	private int CalculateSliderXPos(int sliderRate) {
		int minX = 570;
		int maxX = 725;

		return minX + (int)((maxX - minX) * (sliderRate / 10.0));
	}

	public void SetPopupOverlay(PopupOverlay po) {
		popupOverlay = po;
	}

	private void ChangeGovernments() {
		EngineStorage.ReadGameData((GameData gameData) => {
			Player player = gameData.GetFirstHumanPlayer();

			if (player.government.transitionType) {
				popupOverlay.ShowPopup(
					new InformationalPopup("You already started a revolution. Remember?"),
					PopupOverlay.PopupCategory.Advisor);
				return;
			}

			popupOverlay.ShowPopup(
				new ConfirmationPopup(
					"You say you want a revolution?",
					"Yes, you know it's gonna be alright.",
					"No. You can count me out.",
					() => { new StartGovernmentTransitionMsg(player).send(); }),
				PopupOverlay.PopupCategory.Advisor);
		});
	}

	private HBoxContainer MakeCityRow(City city) {
		HBoxContainer hboxContainer = new();

		// Use an empty pany container to make it blank, unlike an HSeparator
		PanelContainer hSeparator1 = new();
		hSeparator1.CustomMinimumSize = new Vector2(30, 45);
		hSeparator1.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
		hboxContainer.AddChild(hSeparator1);

		Button cityName = new();
		cityName.CustomMinimumSize = new Vector2(127, 0);
		cityName.Text = city.name;
		cityName.ClipText = true;
		cityName.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		cityName.Pressed += () => {
			GetParent<Advisors>().Hide();
			new MsgShowCityScreen(city).send();
		};
		hboxContainer.AddChild(cityName);

		Label foodLabel = new();
		foodLabel.CustomMinimumSize = new Vector2(40, 0);
		foodLabel.Text = SpaceAlignedDotFormat(city.FoodConsumedPerTurn(), city.FoodGrowthPerTurn());
		foodLabel.HorizontalAlignment = HorizontalAlignment.Center;
		hboxContainer.AddChild(foodLabel);

		Label shieldsLabel = new();
		shieldsLabel.CustomMinimumSize = new Vector2(40, 0);
		{
			CorruptableValue prod = city.CurrentProductionYield();
			shieldsLabel.Text = SpaceAlignedDotFormat(prod.corrupt, prod.useful);
		}
		shieldsLabel.HorizontalAlignment = HorizontalAlignment.Center;
		hboxContainer.AddChild(shieldsLabel);

		Label commerceLabel = new();
		commerceLabel.CustomMinimumSize = new Vector2(40, 0);
		CommerceBreakdown commerce = city.CurrentCommerceYield();
		commerceLabel.Text = SpaceAlignedDotFormat(commerce.corrupted, commerce.taxes + commerce.beakers + commerce.happiness);
		commerceLabel.HorizontalAlignment = HorizontalAlignment.Center;
		hboxContainer.AddChild(commerceLabel);

		// Use an empty pany container to make it blank, unlike an HSeparator
		PanelContainer hSeparator2 = new();
		hSeparator2.CustomMinimumSize = new Vector2(25, 0);
		hSeparator2.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
		hboxContainer.AddChild(hSeparator2);

		Label maintenanceLabel = new();
		maintenanceLabel.CustomMinimumSize = new Vector2(40, 0);
		maintenanceLabel.Text = "0";  // TODO: track maintenance
		maintenanceLabel.HorizontalAlignment = HorizontalAlignment.Center;
		maintenanceLabel.VerticalAlignment = VerticalAlignment.Center;
		maintenanceLabel.ClipText = true;
		hboxContainer.AddChild(maintenanceLabel);

		Label happinessLabel = new();
		happinessLabel.CustomMinimumSize = new Vector2(40, 0);
		{
			int happy = 0;
			int content = 0;
			foreach (CityResident cr in city.residents) {
				if (cr.citizenType.IsDefaultCitizen && cr.mood == CityResident.Mood.Happy) {
					++happy;
				}
				if (cr.citizenType.IsDefaultCitizen && cr.mood == CityResident.Mood.Content) {
					++content;
				}
			}
			happinessLabel.Text = SpaceAlignedDotFormat(happy, content);
		}
		happinessLabel.HorizontalAlignment = HorizontalAlignment.Center;
		happinessLabel.VerticalAlignment = VerticalAlignment.Center;
		happinessLabel.ClipText = true;
		hboxContainer.AddChild(happinessLabel);

		Label scienceLabel = new();
		scienceLabel.CustomMinimumSize = new Vector2(40, 0);
		scienceLabel.Text = $"{commerce.beakers}"; ;
		scienceLabel.HorizontalAlignment = HorizontalAlignment.Center;
		scienceLabel.VerticalAlignment = VerticalAlignment.Center;
		scienceLabel.ClipText = true;
		hboxContainer.AddChild(scienceLabel);

		Label taxesLabel = new();
		taxesLabel.CustomMinimumSize = new Vector2(40, 0);
		taxesLabel.Text = $"{commerce.taxes}"; ;
		taxesLabel.HorizontalAlignment = HorizontalAlignment.Center;
		taxesLabel.VerticalAlignment = VerticalAlignment.Center;
		taxesLabel.ClipText = true;
		hboxContainer.AddChild(taxesLabel);

		// Use an empty pany container to make it blank, unlike an HSeparator
		PanelContainer hSeparator3 = new();
		hSeparator3.CustomMinimumSize = new Vector2(25, 0);
		hSeparator3.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
		hboxContainer.AddChild(hSeparator3);

		Control popuplationContainer = new();
		popuplationContainer.CustomMinimumSize = new Vector2(220, 0);
		AddPopHeads(city, popuplationContainer, 220);
		hboxContainer.AddChild(popuplationContainer);

		PanelContainer productionContainer = new();
		productionContainer.CustomMinimumSize = new Vector2(45, 0);
		productionContainer.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
		hboxContainer.AddChild(productionContainer);

		Label productionLabel = new();
		productionLabel.CustomMinimumSize = new Vector2(75, 0);
		string turnsLeft = city.TurnsUntilProductionFinished() == int.MaxValue ? "(9999999 turns)" : $"({city.TurnsUntilProductionFinished()} turns)";
		productionLabel.Text = $"{city.itemBeingProduced.name}\n{turnsLeft}";
		productionLabel.VerticalAlignment = VerticalAlignment.Center;
		productionLabel.ClipText = true;
		productionLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		hboxContainer.AddChild(productionLabel);

		return hboxContainer;
	}

	// Returns a.b, always taking up 5 characters as long as a and b are less
	// than 100, adding leading or trailing spaces if necessary.
	string SpaceAlignedDotFormat(int a, int b) {
		string result = "";
		if (a < 10) {
			result += " ";
		}
		result += $"{a}.{b}";
		if (b < 10) {
			result += " ";
		}
		return result;
	}

	void AddPopHeads(City city, Node node, int maxWidth) {
		int eraNum = city.owner.EraIndex();

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

		// Figure out the actual spacing we'll use, to ensure we fit withing the
		// bounds of our container.
		int spacer = PopHead.HEAD_SIZE;
		if (width > maxWidth) {
			spacer = (int)((float)maxWidth / width * spacer);
		}

		int xPos = 0;

		// Add each of the default citizens. These are buttons with the idea that
		// we can eventually support clicking on the heads to view details, such
		// as the reason for unhappiness.
		foreach (CityResident cr in happyResidents) {
			xPos = AddCitizen(node, cr, xPos, spacer, eraNum);
		}
		if (happyResidents.Count > 0 && (contentResidents.Count > 0 || unhappyResidents.Count > 0)) {
			xPos += spacer;
		}
		foreach (CityResident cr in contentResidents) {
			xPos = AddCitizen(node, cr, xPos, spacer, eraNum);
		}
		if (contentResidents.Count > 0 && unhappyResidents.Count > 0) {
			xPos += spacer;
		}
		foreach (CityResident cr in unhappyResidents) {
			xPos = AddCitizen(node, cr, xPos, spacer, eraNum);
		}

		// Add space before specialists.
		xPos += spacer;

		// Add the specialists.
		foreach (CityResident cr in specialists) {
			xPos = AddCitizen(node, cr, xPos, spacer, eraNum);
		}
	}

	private int AddCitizen(Node node, CityResident cr, int xPos, int spacer, int eraNum) {
		TextureRect tr = new();
		tr.Texture = PopHead.GetTexture(cr, eraNum);
		tr.SetPosition(new Vector2(xPos, -3));
		node.AddChild(tr);
		return xPos + spacer;
	}
}
