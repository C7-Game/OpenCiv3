using System.Collections.Generic;

namespace C7GameData.Save {
	// A class representing a single technology that can be researched.
	//
	// This is intended for use in save games, so it does not have recursive
	// references for prereqs.
	public class SaveTech {
		public enum Flag {
			BonusTechToFirstCivThatResearches,
			DoublesWealthProduction,
			EnablesBridges,
			IrrigateWithoutWater
		}

		public ID id;
		public string Name { get; set; }
		public string CivilopediaEntry { get; set; }
		public int Cost;
		public bool RequiredForEraAdvancement;

		// The civilopedia name of the era this tech is part of
		// (like ERA_Ancient_Times). This is what art lookups are based on.
		public string EraCivilopediaName { get; set; }

		// The path, like "Art\tech chooser\Icons\39-Mapmaking-small.pcx", of
		// the small icon for this tech.
		public string SmallIconPath;

		// The position of this tech within the tech advisor UI.
		public int X;
		public int Y;

		public List<ID> Prerequisites = new();

		// Assorted boolean flags for the tech. They're stored in this set
		// rather than as booleans to avoid bloating the json file.
		public HashSet<Flag> flags = new();

		public C7GameData.Tech ToTechWithoutPrereqs() {
			C7GameData.Tech result = new() {
				id = this.id,
				Name = this.Name,
				CivilopediaEntry = this.CivilopediaEntry,
				Cost = this.Cost,
				RequiredForEraAdvancement = this.RequiredForEraAdvancement,
				BonusTechToFirstCivThatResearches = this.flags.Contains(Flag.BonusTechToFirstCivThatResearches),
				DoublesWealthProduction = this.flags.Contains(Flag.DoublesWealthProduction),
				EnablesBridges = this.flags.Contains(Flag.EnablesBridges),
				IrrigateWithoutWater = flags.Contains(Flag.IrrigateWithoutWater),
				EraCivilopediaName = this.EraCivilopediaName,
				SmallIconPath = this.SmallIconPath,
				X = this.X,
				Y = this.Y,
				DataSource = this,
			};
			return result;
		}
	}

}
