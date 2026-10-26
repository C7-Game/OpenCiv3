using C7Engine;

namespace C7GameData {
	public class WorldSettings {
		public string worldSize;
		public bool customWorldSize;
		public WorldCharacteristics.Landform? landform;
		public WorldCharacteristics.OceanCoverage? oceanCoverage;
		public WorldCharacteristics.Climate? climate;
		public WorldCharacteristics.Temperature? temperature;
		public WorldCharacteristics.Age? age;
	}
}
