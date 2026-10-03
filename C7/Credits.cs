using Godot;
using Serilog;

public partial class Credits : Node2D {
	private string creditsText = "Could not load credits file";
	private string assetCreditsText = "Could not load asset credits file";
	private string licencesText = "Could not load licences file";

	private static ILogger log = Log.ForContext<Credits>();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		log.Information("Now rolling the credits!");
		try {
			creditsText = System.IO.File.ReadAllText("./Text/credits.txt");
		} catch (System.Exception ex) {
			log.Error(ex, "Failed to read from credits.txt!");
		}
		try {
			assetCreditsText = System.IO.File.ReadAllText("./Text/asset_credits.txt");
		} catch (System.Exception ex) {
			log.Warning(ex, "Failed to read from asset_credits.txt!");
		}
		try {
			licencesText = System.IO.File.ReadAllText("./Text/licences.txt");
		} catch (System.Exception ex) {
			log.Warning(ex, "Failed to read from licences.txt!");
		}
		ShowCredits();
	}

	private void ShowCredits() {
		ImageTexture creditsTexture = TextureLoader.Load("credits.background");
		ImageTexture goBackTexture = TextureLoader.Load("ui.exit.normal");

		TextureRect creditsBackground = new TextureRect();
		creditsBackground.Texture = creditsTexture;
		AddChild(creditsBackground);

		RichTextLabel creditsLabel = new RichTextLabel();
		creditsLabel.Position = new Vector2(80, 120);
		creditsLabel.Size = new Vector2(864, 528);
		creditsLabel.BbcodeEnabled = true;

		FontFile regularFont = new FontFile();
		FontFile boldFont = new FontFile();
		FontFile italicFont = new FontFile();
		FontFile boldItalicFont = new FontFile();
		regularFont.Data = ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-Regular.ttf").Data;
		boldFont.Data = ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-Bold.ttf").Data;
		italicFont.Data = ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-Italic.ttf").Data;
		boldItalicFont.Data = ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-BoldItalic.ttf").Data;

		Theme theme = new Theme();
		theme.SetFont("normal_font", "RichTextLabel", regularFont);
		theme.SetFont("bold_font", "RichTextLabel", boldFont);
		theme.SetFont("italics_font", "RichTextLabel", italicFont);
		theme.SetFont("bold_italics_font", "RichTextLabel", boldItalicFont);

		creditsLabel.Theme = theme;
		creditsLabel.Text = creditsText + assetCreditsText + licencesText;
		AddChild(creditsLabel);

		TextureButton goBackButton = new TextureButton();
		goBackButton.TextureNormal = goBackTexture;
		goBackButton.SetPosition(new Vector2(952, 720));
		AddChild(goBackButton);
		goBackButton.Pressed += ReturnToMenu;
	}
	public void ReturnToMenu() {
		log.Information("Returning to main menu");
		GetTree().ChangeSceneToFile("res://UIElements/MainMenu/main_menu.tscn");
	}
}
