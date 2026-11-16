using Godot;
using Serilog;

/// <summary>
/// The credits screen. The layout lives in Credits.tscn; this script only
/// loads the credits text and the Civ3/standalone artwork, wires up the exit
/// button, and slowly auto-scrolls the column.
/// </summary>
public partial class Credits : Node2D {
	private static ILogger log = Log.ForContext<Credits>();

	[Export] private TextureRect background;
	[Export] private ScrollContainer creditsScrollContainer;
	[Export] private VBoxContainer creditsContainer;
	[Export] private RichTextLabel creditsLabel;
	[Export] private TextureButton exitButton;

	private string creditsText = "Could not load credits file";
	private string assetCreditsText = "Could not load asset credits file";
	private string licencesText = "Could not load licences text file";
	private string specialText = "Could not load special text file";

	private Theme theme;
	private AutoScroller _autoScroller;

	public override void _Ready() {
		log.Information("Now rolling the credits!");

		LoadCreditsText();
		GenerateCreditsTheme();
		ShowCredits();

		background.Texture = TextureLoader.Load("credits.background");
		TextureLoader.SetButtonTextures(exitButton, "ui.exit");
		exitButton.Pressed += ReturnToMenu;

		_autoScroller = new AutoScroller(creditsScrollContainer, creditsContainer);
	}

	private void LoadCreditsText() {
		try {
			creditsText = System.IO.File.ReadAllText("./Text/credits.txt");
		} catch (System.Exception ex) {
			log.Error(ex, "Failed to read from credits.txt!");
		}
		try {
			assetCreditsText = System.IO.File.ReadAllText("./Text/asset_credits.md");
		} catch (System.Exception ex) {
			log.Warning(ex, "Failed to read from asset_credits.md!");
		}
		try {
			licencesText = System.IO.File.ReadAllText("../NOTICE.md");
		} catch (System.Exception ex) {
			log.Warning(ex, "Failed to read from ../NOTICE.md!");
			try {
				licencesText = System.IO.File.ReadAllText("./Text/NOTICE.md");
			} catch (System.Exception ex2) {
				log.Warning(ex2, "Failed to read from ./Text/NOTICE.md!");
			}
		}
		try {
			var encoded = "W2NlbnRlcl0KCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKKipZVU1CTz8qKgoKCgoKCgoKCgoKCgoKCgoK";
			byte[] decodedBytes = System.Convert.FromBase64String(encoded);
			specialText = System.Text.Encoding.UTF8.GetString(decodedBytes);
		} catch (System.Exception ex) {
			log.Warning(ex, "Failed to read special text!");
		}
	}

	private void ShowCredits() {
		string combined = string.Join(System.Environment.NewLine,
			[creditsText, assetCreditsText, licencesText, specialText]);

		creditsLabel.Position = new Vector2(80, 120);
		creditsLabel.Size = new Vector2(864, 528);
		creditsLabel.Theme = theme;
		creditsLabel.Set("markdown_text", combined);
		creditsLabel.BbcodeEnabled = true;
	}

	public override void _Process(double delta) {
		_autoScroller.ScrollByDelta(delta);
	}

	private void GenerateCreditsTheme() {
		FontFile regularFont = new FontFile();
		FontFile boldFont = new FontFile();
		FontFile italicFont = new FontFile();
		FontFile boldItalicFont = new FontFile();
		regularFont.Data = ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-Regular.ttf").Data;
		boldFont.Data = ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-Bold.ttf").Data;
		italicFont.Data = ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-Italic.ttf").Data;
		boldItalicFont.Data = ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-BoldItalic.ttf").Data;

		theme = new Theme();
		theme.SetFont("normal_font", "RichTextLabel", regularFont);
		theme.SetFont("bold_font", "RichTextLabel", boldFont);
		theme.SetFont("italics_font", "RichTextLabel", italicFont);
		theme.SetFont("bold_italics_font", "RichTextLabel", boldItalicFont);

		Color black = new Color(0, 0, 0);
		theme.SetColor("default_color", "RichTextLabel", black);
	}

	public void ReturnToMenu() {
		log.Information("Returning to main menu");
		GetTree().ChangeSceneToFile("res://UIElements/MainMenu/main_menu.tscn");
	}
}
