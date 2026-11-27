using System.Collections.Generic;
using C7GameData.Save;

namespace C7GameData {
	// The in-game representation of a tech that can be researched.
	public class Tech {
		public ID id;
		public string Name { get; set; }
		public string CivilopediaEntry { get; set; }
		public int Cost;
		public bool RequiredForEraAdvancement;

		//special abilities
		public bool BonusTechToFirstCivThatResearches;
		public bool DoublesWealthProduction;
		public bool EnablesBridges;
		public bool IrrigateWithoutWater;

		// The civilopedia name of the era this tech is part of
		// (like ERA_Ancient_Times). This is what art lookups are based on.
		public string EraCivilopediaName { get; set; }

		// The path, like "Art\tech chooser\Icons\39-Mapmaking-small.pcx", of
		// the small icon for this tech.
		public string SmallIconPath;

		// The position of this tech within the tech advisor UI.
		public int X;
		public int Y;

		public List<Tech> Prerequisites = new();

		// The backing save tech, for serialization purposes. This should not be
		// made public - add accessors or fields for what you need.
		public SaveTech DataSource { private get; set; }

		public C7GameData.Save.SaveTech ToSaveTech() {
			return DataSource;
		}

		public void FillInPrereqs(List<SaveTech> saveTechs, List<Tech> techs) {
			SaveTech st = saveTechs.Find(st => st.id == this.id);

			foreach (ID prereq in st.Prerequisites) {
				this.Prerequisites.Add(techs.Find(t => t.id == prereq));
			}
		}
	}
}
