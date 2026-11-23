using C7Engine;

namespace C7GameData;

public class VictoryConditions {
	// TODO: default/preferred victory conditions
	public bool AllowDominationVictory { get; set; }
	public bool AllowSpaceRaceVictory { get; set; }
	public bool AllowDiplomaticVictory { get; set; }
	public bool AllowConquestVictory { get; set; }
	public bool AllowCulturalVictory { get; set; }
	// Edited on the future Game Rules screen; stays enabled until that screen exists.
	public bool CultureVictoryRequiresDoubleLead { get; set; } = true;
	public int OneCityCultureWin { get; set; }
	public int AllCitiesCultureWin { get; set; }
	public bool AllowWonderVictory { get; set; }
	public bool CityElimination { get; set; }
	public bool Regicide { get; set; }
	public bool MassRegicide { get; set; }
	public bool VictoryLocations { get; set; }
	public bool CaptureTheFlag { get; set; }
	public bool ReverseCaptureTheFlag { get; set; }

	public static VictoryConditions FromSettings() {
		string allowConquestRaw = C7Settings.GetSettingsValueOrDefault(C7Settings.Rules.SectionName, C7Settings.Rules.AllowConquestVictory, "");
		string allowDominationRaw = C7Settings.GetSettingsValueOrDefault(C7Settings.Rules.SectionName, C7Settings.Rules.AllowDominationVictory, "");
		string allowCulturalRaw = C7Settings.GetSettingsValueOrDefault(C7Settings.Rules.SectionName, C7Settings.Rules.AllowCulturalVictory, "");
		string requireDoubleLeadRaw = C7Settings.GetSettingsValueOrDefault(C7Settings.Rules.SectionName, C7Settings.Rules.CultureVictoryRequiresDoubleLead, "true");

		VictoryConditions vc = new() {
			AllowConquestVictory = bool.TryParse(allowConquestRaw, out bool allowConquest) && allowConquest,
			AllowDominationVictory = bool.TryParse(allowDominationRaw, out bool allowDomination) && allowDomination,
			AllowCulturalVictory = bool.TryParse(allowCulturalRaw, out bool allowCultural) && allowCultural,
			CultureVictoryRequiresDoubleLead = bool.TryParse(requireDoubleLeadRaw, out bool requireDoubleLead) ? requireDoubleLead : true
		};

		return vc;
	}
}
