using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using Godot;

// The Codex (Civilopedia) popup, opened from the top-left toolbar.
// It reproduces the stock Civ3 Civilopedia's layout and navigation.
public partial class CodexPopup : Popup {
	private const int WindowWidth = 1024;
	private const int WindowHeight = 768;

	// civilopediaBackground.pcx pads its 1024x768 canvas with transparency; the
	// parchment panel only covers this region. The fallback must match it so both
	// modes occupy the same screen space.
	private const int PanelLeft = 172;
	private const int PanelTop = 11;
	private const int PanelWidth = 677;
	private const int PanelHeight = 734;

	// Regions measured from civilopediaBackground.pcx (1024x768).
	private const int TitleLeft = 198;
	private const int TitleTop = 26;
	private const int TitleWidth = 192;
	private const int TitleHeight = 34;
	private const int BackLeft = 412;
	private const int ForwardLeft = 440;
	private const int ArrowTop = 33;
	private const int NameBarLeft = 199;
	private const int NameBarTop = 70;
	private const int NameBarWidth = 648;
	private const int NameBarHeight = 30;
	private const int NameLeft = 222;
	private const int NameWidth = 470;
	private const int IconLeft = 695;
	private const int IconTop = 59;
	private const int IconSize = 128;
	private const int DescriptionLeft = 707;
	private const int DescriptionTop = 233;
	private const int UpLeft = 748;
	private const int UpTop = 343;
	private const int PreviousLeft = 727;
	private const int NextLeft = 777;
	private const int RowArrowTop = 362;
	private const int LetterLeft = 720;
	private const int LetterTop = 400;
	private const int LetterWidth = 20;
	private const int LetterHeight = 22;
	private const int LetterSpacingX = 20;
	private const int LetterSpacingY = 32;
	private const int CloseLeft = 728;
	private const int CloseTop = 684;
	private const int CloseWidth = 62;
	private const int CloseHeight = 34;
	private const int ListLeft = 212;
	private const int ListTop = 118;
	private const int ListWidth = 452;
	private const int ListHeight = 584;
	private const int TextLeft = 222;
	private const int TextWidth = 440;
	private const int BodyFontSize = 13;

	private static readonly Color BodyTextColor = new("2a2418");

	private enum PageKind {
		Main,
		Section,
		Entry,
		Index,
	}

	private readonly record struct Page(PageKind Kind, CodexSection? Section, CodexArticle Article, bool Description);

	private enum BarColor {
		Blue,
		Green,
		Grey,
		Orange,
		Purple,
		Red,
		Yellow,
	}

	private static readonly string ArrowSheet = "Art/Civilopedia/civilo_Up_Next_Back_ARROWS.pcx";
	private static readonly string BackSheet = "Art/Civilopedia/fwdBackarrows.pcx";

	private readonly Dictionary<CodexSection, List<CodexArticle>> articlesBySection = new();

	private Control pageLayer;
	private ColorRect nameBarFallback;
	private TextureRect nameBar;
	private Label nameLabel;
	private TextureRect largeIcon;
	private TextureButton backButton;
	private TextureButton forwardButton;
	private TextureButton upButton;
	private TextureButton previousButton;
	private TextureButton nextButton;
	private TextureButton descriptionButton;
	private Label descriptionLabel;
	private readonly List<Button> letterButtons = new();

	private ScrollContainer listScroll;
	private readonly List<(string name, Control row)> listRows = new();

	private readonly List<Page> history = new();
	private int historyIndex = -1;
	private Page current = new(PageKind.Main, null, null, false);

	private RichTextLabel articleLabel;
	private bool articleIsMarkdown;

	public CodexPopup() {
		// Shown via PopupOverlay.ShowPopup, which reads these before _Ready.
		alignment = BoxContainer.AlignmentMode.Center;
		margins = new Margins();
		SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		SizeFlagsVertical = SizeFlags.ShrinkCenter;
	}

	public override void _Ready() {
		base._Ready();

		AddTexture(WindowWidth, WindowHeight);
		if (!TrySetPediaBackground()) {
			AddFallbackBackground();
		}

		EngineStorage.ReadGameData(BuildUi);
	}

	private void BuildUi(GameData data) {
		if (data == null) {
			return;
		}

		foreach (CodexSection section in CodexCatalog.SectionOrder) {
			articlesBySection[section] = CodexCatalog.Build(data, section);
		}

		CreateChrome();
		Navigate(new Page(PageKind.Main, null, null, false));
	}

	private void CreateChrome() {
		pageLayer = new Control { MouseFilter = MouseFilterEnum.Ignore };
		pageLayer.SetPosition(Vector2.Zero);
		pageLayer.SetSize(new Vector2(WindowWidth, WindowHeight));
		AddChild(pageLayer);

		Label title = MakeLabel("CIVILOPEDIA", 24, Colors.Black);
		title.SetPosition(new Vector2(TitleLeft, TitleTop));
		title.SetSize(new Vector2(TitleWidth, TitleHeight));
		title.HorizontalAlignment = HorizontalAlignment.Center;
		AddChild(title);

		backButton = MakeArrowButton(BackSheet, 37, 69, 101, 1, 15, 24, new Vector2(BackLeft, ArrowTop), "Back", GoBack);
		forwardButton = MakeArrowButton(BackSheet, 53, 85, 117, 1, 15, 24, new Vector2(ForwardLeft, ArrowTop), "Forward", GoForward);

		nameBarFallback = new ColorRect {
			Position = new Vector2(NameBarLeft, NameBarTop),
			Size = new Vector2(NameBarWidth, NameBarHeight),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		AddChild(nameBarFallback);

		nameBar = new TextureRect {
			Position = new Vector2(NameBarLeft, NameBarTop),
			Size = new Vector2(NameBarWidth, NameBarHeight),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		AddChild(nameBar);

		nameLabel = MakeLabel("", 18, Colors.Black);
		nameLabel.SetPosition(new Vector2(NameLeft, NameBarTop));
		nameLabel.SetSize(new Vector2(NameWidth, NameBarHeight));
		nameLabel.VerticalAlignment = VerticalAlignment.Center;
		nameLabel.ClipText = true;
		AddChild(nameLabel);

		largeIcon = new TextureRect {
			Position = new Vector2(IconLeft, IconTop),
			Size = new Vector2(IconSize, IconSize),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		AddChild(largeIcon);

		descriptionButton = new TextureButton {
			Position = new Vector2(DescriptionLeft, DescriptionTop),
			Size = new Vector2(102, 17),
			IgnoreTextureSize = true,
			TooltipText = "Description",
		};
		descriptionButton.TextureNormal = Crop("Art/Civilopedia/descbox.pcx", 1, 1, 102, 17);
		descriptionButton.TextureHover = Crop("Art/Civilopedia/descbox.pcx", 104, 1, 102, 17);
		descriptionButton.TexturePressed = Crop("Art/Civilopedia/descbox.pcx", 207, 1, 102, 17);
		descriptionButton.Pressed += ToggleDescription;
		AddChild(descriptionButton);

		descriptionLabel = MakeLabel("Description", 12, Colors.Black);
		descriptionLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		descriptionLabel.HorizontalAlignment = HorizontalAlignment.Center;
		descriptionLabel.VerticalAlignment = VerticalAlignment.Center;
		descriptionButton.AddChild(descriptionLabel);

		upButton = MakeArrowButton(ArrowSheet, 1, 26, 51, 27, 24, 15, new Vector2(UpLeft, UpTop), "Up", GoUp);
		previousButton = MakeArrowButton(ArrowSheet, 1, 33, 65, 1, 15, 24, new Vector2(PreviousLeft, RowArrowTop), "Previous", () => GoToNeighbor(-1));
		nextButton = MakeArrowButton(ArrowSheet, 17, 49, 81, 1, 15, 24, new Vector2(NextLeft, RowArrowTop), "Next", () => GoToNeighbor(1));

		for (int i = 0; i < 26; i++) {
			char letter = (char)('A' + i);
			Button button = MakeFlatButton(letter.ToString(), 14);
			button.SetPosition(new Vector2(LetterLeft + (i % 4) * LetterSpacingX, LetterTop + (i / 4) * LetterSpacingY));
			button.SetSize(new Vector2(LetterWidth, LetterHeight));
			button.Pressed += () => GoToLetter(letter);
			AddChild(button);
			letterButtons.Add(button);
		}

		Button closeButton = MakeFlatButton("X", 22);
		closeButton.SetPosition(new Vector2(CloseLeft, CloseTop));
		closeButton.SetSize(new Vector2(CloseWidth, CloseHeight));
		closeButton.TooltipText = "Close";
		closeButton.AddThemeColorOverride("font_color", new Color("5a6e52"));
		closeButton.AddThemeColorOverride("font_hover_color", new Color("a85a20"));
		closeButton.AddThemeColorOverride("font_pressed_color", new Color("a85a20"));
		closeButton.Pressed += Close;
		AddChild(closeButton);
	}

	private Button MakeFlatButton(string text, int fontSize) {
		Button button = new() {
			Text = text,
			Flat = true,
			FocusMode = FocusModeEnum.None,
		};
		button.AddThemeFontSizeOverride("font_size", fontSize);
		button.AddThemeColorOverride("font_color", Colors.Black);
		button.AddThemeColorOverride("font_hover_color", new Color("b4641e"));
		button.AddThemeColorOverride("font_pressed_color", new Color("b4641e"));
		button.AddThemeColorOverride("font_focus_color", Colors.Black);
		button.AddThemeColorOverride("font_disabled_color", new Color("8a8a8a"));
		foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" }) {
			button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
		}
		return button;
	}

	private Label MakeLabel(string text, int fontSize, Color color) {
		Label label = new() {
			Text = text,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private TextureButton MakeArrowButton(string sheet, int normalX, int hoverX, int disabledX, int y, int width, int height, Vector2 position, string tooltip, Action action) {
		TextureButton button = new() {
			Position = position,
			Size = new Vector2(width, height),
			IgnoreTextureSize = true,
			TooltipText = tooltip,
			TextureNormal = Crop(sheet, normalX, y, width, height),
			TextureHover = Crop(sheet, hoverX, y, width, height),
			TexturePressed = Crop(sheet, hoverX, y, width, height),
			TextureDisabled = Crop(sheet, disabledX, y, width, height),
		};
		if (button.TextureNormal == null) {
			Label fallback = MakeLabel(tooltip switch {
				"Back" => "<",
				"Forward" => ">",
				"Up" => "^",
				"Previous" => "<",
				_ => ">",
			}, 16, Colors.Black);
			fallback.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			fallback.HorizontalAlignment = HorizontalAlignment.Center;
			fallback.VerticalAlignment = VerticalAlignment.Center;
			button.AddChild(fallback);
		}
		button.Pressed += action;
		AddChild(button);
		return button;
	}

	private static AtlasTexture Crop(string path, int x, int y, int width, int height) {
		try {
			ImageTexture sheet = TextureLoader.LoadByPath(path);
			if (sheet == null) {
				return null;
			}
			return new AtlasTexture {
				Atlas = sheet,
				Region = new Rect2(x, y, width, height),
			};
		} catch (Exception) {
			return null;
		}
	}

	private static ImageTexture LoadArt(string path) {
		try {
			return TextureLoader.LoadByPath(path);
		} catch (Exception) {
			return null;
		}
	}

	// ---- Navigation ----

	private List<CodexArticle> Articles(CodexSection section) {
		return articlesBySection.TryGetValue(section, out List<CodexArticle> articles)
			? articles
			: new List<CodexArticle>();
	}

	private List<CodexArticle> AllArticles() {
		return articlesBySection.Values
			.SelectMany(articles => articles)
			.OrderBy(article => article.Name, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private void Navigate(Page page) {
		if (historyIndex >= 0 && history[historyIndex] == page) {
			DrawPage(page);
			return;
		}
		if (historyIndex < history.Count - 1) {
			history.RemoveRange(historyIndex + 1, history.Count - historyIndex - 1);
		}
		history.Add(page);
		historyIndex = history.Count - 1;
		DrawPage(page);
	}

	private void GoBack() {
		if (historyIndex > 0) {
			historyIndex--;
			DrawPage(history[historyIndex]);
		}
	}

	private void GoForward() {
		if (historyIndex < history.Count - 1) {
			historyIndex++;
			DrawPage(history[historyIndex]);
		}
	}

	private void GoUp() {
		if (current.Kind == PageKind.Entry) {
			Navigate(new Page(PageKind.Section, current.Section, null, false));
		} else if (current.Kind == PageKind.Section || current.Kind == PageKind.Index) {
			Navigate(new Page(PageKind.Main, null, null, false));
		}
	}

	private void GoToNeighbor(int step) {
		if (current.Kind == PageKind.Entry) {
			List<CodexArticle> entries = Articles(current.Section.Value);
			int index = entries.FindIndex(entry => ReferenceEquals(entry, current.Article));
			if (index < 0 || entries.Count == 0) {
				return;
			}
			CodexArticle neighbor = entries[(index + step + entries.Count) % entries.Count];
			Navigate(new Page(PageKind.Entry, current.Section, neighbor, false));
		} else if (current.Kind == PageKind.Section) {
			int index = Array.IndexOf(CodexCatalog.SectionOrder, current.Section.Value);
			CodexSection neighbor = CodexCatalog.SectionOrder[(index + step + CodexCatalog.SectionOrder.Length) % CodexCatalog.SectionOrder.Length];
			Navigate(new Page(PageKind.Section, neighbor, null, false));
		}
	}

	private void GoToLetter(char letter) {
		if (current.Kind == PageKind.Main) {
			Navigate(new Page(PageKind.Index, null, null, false));
			ScrollListTo(letter);
			return;
		}
		if (current.Kind == PageKind.Section || current.Kind == PageKind.Index) {
			ScrollListTo(letter);
			return;
		}
		CodexArticle first = Articles(current.Section.Value)
			.FirstOrDefault(article => StartsWith(article.Name, letter));
		if (first != null) {
			Navigate(new Page(PageKind.Entry, current.Section, first, false));
		}
	}

	private static bool StartsWith(string name, char letter) {
		if (string.IsNullOrWhiteSpace(name)) {
			return false;
		}
		return char.ToUpperInvariant(name.TrimStart()[0]) == letter;
	}

	private void ToggleDescription() {
		if (current.Kind != PageKind.Entry || current.Article.Pages.Count < 2) {
			return;
		}
		Page toggled = current with { Description = !current.Description };
		if (historyIndex >= 0) {
			history[historyIndex] = toggled;
		}
		DrawPage(toggled);
	}

	// ---- Drawing pages ----

	private void DrawPage(Page page) {
		current = page;
		ClearPage();

		CodexArticle entry = page.Article;
		CodexSection? section = entry?.Section ?? page.Section;
		BarColor bar = page.Kind == PageKind.Main || page.Kind == PageKind.Index
			? BarColor.Red
			: BarColorOf(section.Value);

		nameBar.Texture = BarTexture(bar);
		nameBarFallback.Visible = nameBar.Texture == null;
		nameBarFallback.Color = BarFallbackColor(bar);
		nameLabel.AddThemeColorOverride("font_color",
			nameBar.Texture == null ? new Color("f7efce") : BarTextColor(bar));
		nameLabel.Text = page.Kind switch {
			PageKind.Main => "Main",
			PageKind.Index => "Index",
			PageKind.Section => CodexCatalog.SectionTitle(section.Value),
			_ => entry.Name,
		};
		largeIcon.Texture = page.Kind switch {
			PageKind.Main => LoadArt("Art/Civilopedia/Icons/menu_red large.pcx"),
			PageKind.Index => LoadArt("Art/Civilopedia/Icons/menus large.pcx"),
			PageKind.Section => CodexCatalog.SectionIcon(section.Value, large: true),
			_ => entry.Icon ?? CodexCatalog.SectionIcon(entry.Section, large: true),
		};

		backButton.Disabled = historyIndex <= 0;
		forwardButton.Disabled = historyIndex >= history.Count - 1;
		upButton.Disabled = page.Kind == PageKind.Main;
		previousButton.Disabled = page.Kind == PageKind.Main || page.Kind == PageKind.Index;
		nextButton.Disabled = page.Kind == PageKind.Main || page.Kind == PageKind.Index;

		bool hasDescription = entry != null && entry.Pages.Count > 1;
		descriptionButton.Visible = hasDescription;
		descriptionLabel.Text = page.Description ? "Effects" : "Description";

		char? letter = entry != null && !string.IsNullOrEmpty(entry.Name)
			? char.ToUpperInvariant(entry.Name[0])
			: null;
		for (int i = 0; i < letterButtons.Count; i++) {
			bool isCurrent = letter == (char)('A' + i);
			letterButtons[i].AddThemeColorOverride("font_color", isCurrent ? new Color("3a78c8") : Colors.Black);
		}

		switch (page.Kind) {
			case PageKind.Main:
				DrawMain();
				break;
			case PageKind.Section:
				DrawSection(section.Value);
				break;
			case PageKind.Index:
				DrawIndex();
				break;
			case PageKind.Entry:
				DrawEntry(entry, page.Description);
				break;
		}
	}

	private void ClearPage() {
		foreach (Node child in pageLayer.GetChildren()) {
			pageLayer.RemoveChild(child);
			child.QueueFree();
		}
		listScroll = null;
		listRows.Clear();
	}

	private void DrawMain() {
		for (int i = 0; i < CodexCatalog.SectionOrder.Length; i++) {
			CodexSection section = CodexCatalog.SectionOrder[i];
			float y = 124 + i * 38;

			ColorRect backing = new() {
				Color = new Color("c4382a"),
				Position = new Vector2(228, y - 2),
				Size = new Vector2(36, 36),
				MouseFilter = MouseFilterEnum.Ignore,
			};
			pageLayer.AddChild(backing);

			TextureRect icon = new() {
				Texture = CodexCatalog.SectionIcon(section, large: false),
				Position = new Vector2(230, y),
				Size = new Vector2(32, 32),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			pageLayer.AddChild(icon);

			Button button = MakeFlatButton(CodexCatalog.SectionTitle(section), 17);
			button.Alignment = HorizontalAlignment.Left;
			button.SetPosition(new Vector2(272, y + 1));
			button.SetSize(new Vector2(300, 30));
			CodexSection captured = section;
			button.Pressed += () => Navigate(new Page(PageKind.Section, captured, null, false));
			pageLayer.AddChild(button);
		}
	}

	private void DrawSection(CodexSection section) {
		List<CodexArticle> entries = Articles(section);
		ScrollContainer scroll = CreateList();
		VBoxContainer column = new() { CustomMinimumSize = new Vector2(TextWidth, 0) };
		column.AddThemeConstantOverride("separation", 2);
		scroll.AddChild(column);

		if (section == CodexSection.Resources) {
			// Civ3 lists resources by kind.
			string[] groups = { "Bonus Resources", "Luxury Resources", "Strategic Resources" };
			foreach (string group in groups) {
				List<CodexArticle> ofKind = entries.Where(entry => entry.Group == group).ToList();
				if (ofKind.Count == 0) {
					continue;
				}
				column.AddChild(MakeLabel(group, 15, BodyTextColor));
				AddListGrid(column, ofKind, columns: 2, icons: true);
			}

			List<CodexArticle> ungrouped = entries.Where(entry => !groups.Contains(entry.Group)).ToList();
			if (ungrouped.Count > 0) {
				AddListGrid(column, ungrouped, columns: 2, icons: true);
			}
			return;
		}

		AddListGrid(column, entries, columns: 2, icons: true);
	}

	private void DrawIndex() {
		ScrollContainer scroll = CreateList();
		VBoxContainer column = new() { CustomMinimumSize = new Vector2(TextWidth, 0) };
		column.AddThemeConstantOverride("separation", 2);
		scroll.AddChild(column);
		AddListGrid(column, AllArticles(), columns: 3, icons: false);
	}

	private ScrollContainer CreateList() {
		ScrollContainer scroll = new() {
			Position = new Vector2(ListLeft, ListTop),
			Size = new Vector2(ListWidth, ListHeight),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		pageLayer.AddChild(scroll);
		listScroll = scroll;
		return scroll;
	}

	private void AddListGrid(Container parent, List<CodexArticle> entries, int columns, bool icons) {
		GridContainer grid = new() { Columns = columns };
		grid.AddThemeConstantOverride("h_separation", 6);
		grid.AddThemeConstantOverride("v_separation", icons ? 4 : 0);
		parent.AddChild(grid);

		float width = (TextWidth - (columns - 1) * 6f) / columns;
		foreach (CodexArticle entry in entries) {
			HBoxContainer row = new() { CustomMinimumSize = new Vector2(width, icons ? 34 : 18) };
			row.AddThemeConstantOverride("separation", 6);
			if (icons) {
				row.AddChild(new TextureRect {
					Texture = entry.SmallIcon ?? CodexCatalog.SectionIcon(entry.Section, large: false),
					CustomMinimumSize = new Vector2(32, 32),
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					MouseFilter = MouseFilterEnum.Ignore,
				});
			}

			Button name = MakeFlatButton(entry.Name, icons ? 14 : 12);
			name.Alignment = HorizontalAlignment.Left;
			name.ClipText = true;
			name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			name.TooltipText = entry.Name;
			CodexArticle captured = entry;
			name.Pressed += () => Navigate(new Page(PageKind.Entry, captured.Section, captured, false));
			row.AddChild(name);
			grid.AddChild(row);
			listRows.Add((entry.Name, row));
		}
	}

	private void ScrollListTo(char letter) {
		if (listScroll == null) {
			return;
		}
		foreach ((string name, Control row) in listRows) {
			if (!StartsWith(name, letter)) {
				continue;
			}
			// Rows only know their positions after layout.
			Callable.From(() => {
				if (IsInstanceValid(row) && IsInstanceValid(listScroll)) {
					listScroll.ScrollVertical = (int)(row.GetGlobalRect().Position.Y - listScroll.GetGlobalRect().Position.Y + listScroll.ScrollVertical);
				}
			}).CallDeferred();
			return;
		}
	}

	private void DrawEntry(CodexArticle entry, bool description) {
		ScrollContainer scroll = new() {
			Position = new Vector2(TextLeft, ListTop),
			Size = new Vector2(TextWidth, ListHeight),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		pageLayer.AddChild(scroll);

		VBoxContainer content = new() {
			CustomMinimumSize = new Vector2(TextWidth, 0),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		content.AddThemeConstantOverride("separation", 6);
		scroll.AddChild(content);

		if (!description && entry.HasStats) {
			Label stats = new() {
				Text = string.Join("\n", entry.StatLines),
				AutowrapMode = TextServer.AutowrapMode.Word,
			};
			stats.AddThemeFontSizeOverride("font_size", BodyFontSize);
			stats.AddThemeColorOverride("font_color", BodyTextColor);
			content.AddChild(stats);
			if (entry.HasPages) {
				content.AddChild(new HSeparator());
			}
		}

		if (entry.HasPages) {
			articleLabel = CreateArticleLabel();
			articleLabel.FitContent = true;
			articleLabel.ScrollActive = false;
			articleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			articleLabel.SizeFlagsVertical = SizeFlags.ShrinkBegin;
			SetArticleText(description && entry.Pages.Count > 1 ? entry.Pages[1] : entry.Pages[0]);
			content.AddChild(articleLabel);
		}
	}

	private RichTextLabel CreateArticleLabel() {
		GDScript markdownScript = ResourceLoader.Load<GDScript>("res://addons/markdownlabel/addons/markdownlabel/markdownlabel.gd");
		RichTextLabel label = null;
		if (markdownScript != null) {
			try {
				label = markdownScript.New().As<RichTextLabel>();
				// Do not let the markdown renderer open key:/URLs; navigation is a later phase.
				label.Set("automatic_links", false);
				articleIsMarkdown = true;
			} catch (Exception) {
				label = null;
			}
		}

		if (label == null) {
			label = new RichTextLabel();
			articleIsMarkdown = false;
		}

		// RichTextLabel uses a separate theme font size per style, and MarkdownLabel
		// scales headings off normal_font_size, so set them all to keep the article uniform.
		label.AddThemeFontSizeOverride("normal_font_size", BodyFontSize);
		label.AddThemeFontSizeOverride("bold_font_size", BodyFontSize);
		label.AddThemeFontSizeOverride("italics_font_size", BodyFontSize);
		label.AddThemeFontSizeOverride("bold_italics_font_size", BodyFontSize);
		label.AddThemeFontSizeOverride("mono_font_size", BodyFontSize);
		label.BbcodeEnabled = true;
		label.TabSize = 5;
		label.AddThemeColorOverride("default_color", BodyTextColor);
		return label;
	}

	private void SetArticleText(string text) {
		if (articleIsMarkdown) {
			articleLabel.Set("markdown_text", text);
		} else {
			articleLabel.Text = text;
		}
	}

	// ---- Civ3 art ----

	private static BarColor BarColorOf(CodexSection section) => section switch {
		CodexSection.Civilizations => BarColor.Orange,
		CodexSection.Technologies => BarColor.Blue,
		CodexSection.GreatWonders or CodexSection.SmallWonders => BarColor.Yellow,
		CodexSection.CityImprovements => BarColor.Orange,
		CodexSection.Governments => BarColor.Grey,
		CodexSection.Units => BarColor.Purple,
		CodexSection.TerrainImprovements or CodexSection.Terrain or CodexSection.Resources => BarColor.Green,
		CodexSection.GameConcepts => BarColor.Grey,
		_ => BarColor.Red,
	};

	private static ImageTexture BarTexture(BarColor color) {
		string file = color switch {
			BarColor.Blue => "bar-blue.pcx",
			BarColor.Green => "bar-green.pcx",
			BarColor.Grey => "bar-grey.pcx",
			BarColor.Orange => "bar-orange.pcx",
			BarColor.Purple => "bar-purple.pcx",
			BarColor.Red => "bar-red.pcx",
			_ => "bar-yello.pcx",
		};
		return LoadArt("Art/Civilopedia/" + file);
	}

	private static Color BarFallbackColor(BarColor color) => color switch {
		BarColor.Blue => new Color("2a3a8a"),
		BarColor.Green => new Color("2f6a2f"),
		BarColor.Grey => new Color("4a4a4a"),
		BarColor.Orange => new Color("b86820"),
		BarColor.Purple => new Color("4a2a7a"),
		BarColor.Red => new Color("c4382a"),
		_ => new Color("b8a020"),
	};

	private static Color BarTextColor(BarColor color) => color switch {
		BarColor.Blue => new Color("1a2a8a"),
		BarColor.Green => new Color("1f5a1f"),
		BarColor.Grey => new Color("303030"),
		BarColor.Orange => new Color("7a3a08"),
		BarColor.Purple => new Color("1c1c8c"),
		BarColor.Red => new Color("8a1a14"),
		_ => new Color("80782a"),
	};

	// Standalone mode ships no pedia art, so stand in a panel at the Civ3 panel's rect.
	private void AddFallbackBackground() {
		ColorRect frame = new() {
			Color = new Color("214a31"),
			Position = new Vector2(PanelLeft, PanelTop),
			Size = new Vector2(PanelWidth, PanelHeight),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		AddChild(frame);

		ColorRect page = new() {
			Color = new Color("f7efce"),
			Position = new Vector2(PanelLeft + 4, PanelTop + 4),
			Size = new Vector2(PanelWidth - 8, PanelHeight - 8),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		AddChild(page);
	}

	private bool TrySetPediaBackground() {
		try {
			ImageTexture background = TextureLoader.Load("civilopedia_screen.background");
			if (background == null) {
				return false;
			}

			TextureRect art = new() {
				Texture = background,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				SizeFlagsVertical = SizeFlags.ExpandFill,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(art);
			MoveChild(art, 0);
			return true;
		} catch (Exception) {
			return false;
		}
	}

	private void Close() {
		(GetParent() as PopupOverlay)?.OnHidePopup();
	}

	public override void _UnhandledInput(InputEvent @event) {
		if (@event is not InputEventKey key || !key.Pressed) {
			return;
		}

		bool handled = true;
		switch (key.Keycode) {
			case Key.Escape:
				Close();
				break;
			case Key.Left when key.AltPressed:
				GoBack();
				break;
			case Key.Right when key.AltPressed:
				GoForward();
				break;
			case Key.Left:
				GoToNeighbor(-1);
				break;
			case Key.Right:
				GoToNeighbor(1);
				break;
			case Key.Up:
				GoUp();
				break;
			case Key.Backspace:
				GoBack();
				break;
			default:
				handled = false;
				break;
		}

		if (handled) {
			GetViewport().SetInputAsHandled();
		}
	}
}
