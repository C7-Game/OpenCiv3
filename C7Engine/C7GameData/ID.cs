using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using C7GameData.Save;

namespace C7GameData {
	public interface IHasID {
		ID id { get; }
	}

	public class ID {
		// at runtime, any ID parsed from a string as "<key>-none" will be
		// compared to other IDs by -0xC7C7
		private static readonly int magicNoneIdNumber = -0xC7C7;

		private readonly string key;

		private readonly int n;

		public int GetN => n;

		public override string ToString() {
			return n != magicNoneIdNumber ? $"{key}-{n}" : $"{key}-none";
		}

		internal ID(string key, int n) {
			this.key = key;
			this.n = n;
		}

		public static ID None(string key) {
			return new ID(key, magicNoneIdNumber);
		}

		public static ID FromString(string str) {
			List<string> split = str.Split('-').ToList();
			// To handle units like "Man-O-War" we need to only look at the final
			// element of the .Split call to get the id.
			string key = string.Join("-", split.GetRange(0, split.Count - 1));
			int n = split[split.Count - 1] == "none" ? magicNoneIdNumber : int.Parse(split[split.Count - 1]);
			if (n < 0) {
				throw new Exception($"ID cannot have a negative number, got {n}");
			}
			return new ID(key, n);
		}

		public override bool Equals(object obj) {
			return obj switch {
				null => false,
				ID id => id.n == n && id.key == key,
				_ => false,
			};
		}

		// c# string hash is based on value, not reference
		public override int GetHashCode() => ToString().GetHashCode();

		public static bool operator ==(ID lhs, ID rhs) {
			if (lhs is null && rhs is null) {
				return true; // null == null, no footguns
			}
			if (lhs is null || rhs is null) {
				return false;
			}
			return lhs.n == rhs.n && lhs.key == rhs.key;
		}

		public static bool operator !=(ID lhs, ID rhs) {
			return !(lhs == rhs);
		}

		public class Factory {
			private Dictionary<string, int> keyCounter;

			public Factory() {
				keyCounter = new Dictionary<string, int>();
			}

			// When creating an ID Factory for a loaded save game, 
			// this constructor finds the largest n-value for each ID key.
			// ie. loading a save with units ["warrior-1", "warrior-3", "worker-2"]
			//     should result in an id factory with
			// keyCounter = {
			//     "warrior": 4,
			//     "worker": 3,
			// }
			public Factory(SaveGame save) {
				IEnumerable<IHasID> entities = new List<IEnumerable<IHasID>>() {save.Units, save.Cities}
												.SelectMany(list=> list); // flattening the list

				keyCounter = entities
					.GroupBy(entity => entity.id.key)
					.ToDictionary(
						group => group.Key,
						group => group.Select(entity => entity.id.n).Max() + 1
					);
			}

			public ID CreateID(string key) {
				int n = keyCounter.GetValueOrDefault(key, 1);
				keyCounter[key] = n + 1;
				return new ID(key, n);
			}
		}
	}

	public class IDJsonConverter : JsonConverter<ID> {
		public override void Write(Utf8JsonWriter writer, ID id, JsonSerializerOptions options) => writer.WriteStringValue(id.ToString());

		public override ID Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => ID.FromString(reader.GetString());

		// Lets IDs be used as dictionary keys in saves.
		public override void WriteAsPropertyName(Utf8JsonWriter writer, ID id, JsonSerializerOptions options) => writer.WritePropertyName(id.ToString());

		public override ID ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => ID.FromString(reader.GetString());
	}
}
