using System.Collections.Generic;

namespace QueryCiv3.Steam;

public class SteamLibrary {
	public string Path { get; set; }
	// App ids listed for this library in libraryfolders.vdf. Empty when
	// the file doesn't say (older formats), which is not proof that an
	// app is absent.
	public HashSet<string> Apps { get; } = new HashSet<string>();
}
