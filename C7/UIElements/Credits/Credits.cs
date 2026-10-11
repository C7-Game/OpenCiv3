using Godot;
using Serilog;

/// <summary>
/// The credits screen. The layout lives in Credits.tscn; this script only
/// loads the credits text and the Civ3/standalone artwork, wires up the exit
/// button, and slowly auto-scrolls the column.
/// </summary>
public partial class Credits : Node2D {
	private static ILogger log = Log.ForContext<Credits>();

	// How fast the credits scroll on their own, in pixels per second. Slow
	// enough to read at a relaxed pace.
	private const float AutoScrollSpeed = 24.0f;

	// After the player scrolls the credits themselves, leave the column alone
	// for a moment so we don't fight them.
	private const float PauseAfterManualScroll = 3.0f;

	[Export] private TextureRect background;
	[Export] private ScrollContainer creditsScroll;
	[Export] private VBoxContainer creditsColumn;
	[Export] private RichTextLabel creditsLabel;
	[Export] private TextureButton exitButton;

	private float autoScrollPause;
	private float scrollRemainder;
	private int lastScrollPosition;

	public override void _Ready() {
		log.Information("Now rolling the credits!");

		background.Texture = TextureLoader.Load("credits.background");
		exitButton.TextureNormal = TextureLoader.Load("ui.exit.normal");
		exitButton.Pressed += ReturnToMenu;

		creditsLabel.Text = LoadCreditsText();
	}

	public override void _Process(double delta) {
		int current = creditsScroll.ScrollVertical;

		// Scrolling up means the player wants to read something, so give them
		// a few seconds before we resume.
		if (current < lastScrollPosition) {
			autoScrollPause = PauseAfterManualScroll;
		}
		lastScrollPosition = current;

		if (autoScrollPause > 0) {
			autoScrollPause -= (float)delta;
			return;
		}

		int maxScroll = Mathf.Max(0, (int)(creditsColumn.Size.Y - creditsScroll.Size.Y));
		if (current >= maxScroll) {
			return;
		}

		// Accumulate the fractional pixels so the scroll still advances when
		// the speed is lower than one pixel per frame.
		scrollRemainder += AutoScrollSpeed * (float)delta;
		int wholePixels = (int)scrollRemainder;
		if (wholePixels == 0) {
			return;
		}
		scrollRemainder -= wholePixels;

		int target = Mathf.Min(current + wholePixels, maxScroll);
		creditsScroll.ScrollVertical = target;
		lastScrollPosition = target;
	}

	private static string LoadCreditsText() {
		try {
			return System.IO.File.ReadAllText(CreditsFilePath());
		} catch (System.Exception ex) {
			log.Error(ex, "Failed to read from credits.txt!");
			return "Could not load credits file";
		}
	}

	// credits.txt ships with the game: it lives in the project directory while
	// developing, and next to the executable in an export.
	private static string CreditsFilePath() {
		return OS.HasFeature("editor")
			? ProjectSettings.GlobalizePath("res://Text/credits.txt")
			: System.IO.Path.Combine(GamePaths.BaseDir, "Text", "credits.txt");
	}

	public void ReturnToMenu() {
		log.Information("Returning to main menu");
		GetTree().ChangeSceneToFile("res://UIElements/MainMenu/main_menu.tscn");
	}
}
