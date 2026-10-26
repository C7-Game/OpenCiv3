using System;
using System.Collections.Generic;
using C7Engine;
using C7GameData;
using Godot;

// The Codex (Civilopedia) popup, opened from the top-left toolbar.
public partial class CodexPopup : Popup {
	private const int WindowWidth = 1024;
	private const int WindowHeight = 768;
	// Regions measured from civilopediaBackground.pcx (1024x768).
	private const int ContentInsetX = 12;
	private const int MainLeft = 199 + ContentInsetX;
	private const int MainRight = 671;
	private const int MainWidth = MainRight - MainLeft;
	// The art leaves the entry-title band transparent (x199..693); the name is inset to match the body.
	private const int TitleLeft = 199;
	private const int TitleTop = 71;
	private const int TitleWidth = 693 - TitleLeft;
	private const int TitleHeight = 28;
	private const int BodyTop = 112;
	private const int BodyHeight = 712 - BodyTop;
	private const int SideLeft = 695;
	private const int SideWidth = 128;
	private const int IconTop = 60;
	private const int IconSize = 128;
	// Sections list is capped to the art's striped band between the icon and the tan area.
	private const int SectionsHeight = 112;
	// Entries list fills the art's light-tan area.
	private const int EntriesHeight = 290;
	private const int SideHeight = 744 - IconTop;
	// Entry rows are indented inside their row, and the row itself is not resized.
	private const int EntryTextInset = 6;
	// Header spans the body column only, nudged down within the art's top band.
	private const int HeaderLeft = MainLeft;
	private const int HeaderTop = 22;
	private const int HeaderWidth = MainWidth;
	private const int HeaderHeight = 44;
	// Gap between the title text and the nav arrows: 113 + 2 x 6px separation = ~125px.
	private const int TitleToNavGap = 113;
	private const int CloseButtonBottomGap = 17;
	private const int CloseButtonShiftX = 6;
	private const int BodyFontSize = 13;
	// Parchment tone sampled next to the title cut-out, to fill the art's transparent title band.
	private static readonly Color TitleBackgroundColor = Color.Color8(190, 178, 145);
	// Draw outlines over the art regions to check alignment.
	private static readonly bool ShowLayoutGuides = false;

	private readonly Dictionary<CodexSection, Button> sectionButtons = new();
	private VBoxContainer indexList;
	private Label indexHeader;
	private CodexSection activeSection = CodexSection.Units;

	private TextureRect icon;
	private Label nameLabel;
	private Label statLabel;

	private static ImageTexture defaultIcon;

	private HBoxContainer pager;
	private Button prevPage;
	private Button nextPage;
	private Label pageLabel;
	private RichTextLabel articleLabel;
	private bool articleIsMarkdown;

	private List<string> pages = new();
	private int pageIndex;

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
			AddBackground(WindowWidth, WindowHeight);
		}

		EngineStorage.ReadGameData(BuildUi);
	}

	private void BuildUi(GameData gameData) {
		if (gameData == null) {
			return;
		}

		HBoxContainer header = BuildHeader();
		header.SetPosition(new Vector2(HeaderLeft, HeaderTop));
		header.SetSize(new Vector2(HeaderWidth, HeaderHeight));
		AddChild(header);

		AddChild(BuildTitleRow());

		VBoxContainer main = BuildMainPane();
		main.SetPosition(new Vector2(MainLeft, BodyTop));
		main.SetSize(new Vector2(MainWidth, BodyHeight));
		AddChild(main);

		VBoxContainer side = BuildSidePane(gameData);
		side.SetPosition(new Vector2(SideLeft, IconTop));
		side.SetSize(new Vector2(SideWidth, SideHeight));
		AddChild(side);

		if (ShowLayoutGuides) {
			AddLayoutGuides();
		}

		SelectSection(gameData, CodexSection.Units);
	}

	private Control BuildTitleRow() {
		Control row = new();
		row.SetPosition(new Vector2(TitleLeft, TitleTop));
		row.SetSize(new Vector2(TitleWidth, TitleHeight));

		ColorRect background = new() { Color = TitleBackgroundColor };
		background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		row.AddChild(background);

		nameLabel = new Label {
			VerticalAlignment = VerticalAlignment.Center,
			ClipText = true,
		};
		nameLabel.AddThemeFontSizeOverride("font_size", 20);
		nameLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		nameLabel.OffsetLeft = ContentInsetX;
		row.AddChild(nameLabel);

		return row;
	}

	private static ImageTexture DefaultIcon() {
		// Keeps the icon square occupied (and the column from shifting up) when an entry has no art.
		if (defaultIcon == null) {
			Image image = Image.CreateEmpty(IconSize, IconSize, false, Image.Format.Rgba8);
			image.Fill(TitleBackgroundColor);
			defaultIcon = ImageTexture.CreateFromImage(image);
		}
		return defaultIcon;
	}

	private void AddLayoutGuides() {
		AddGuide(HeaderLeft, HeaderTop, HeaderWidth, HeaderHeight);
		AddGuide(TitleLeft, TitleTop, TitleWidth, TitleHeight);
		AddGuide(MainLeft, BodyTop, MainWidth, BodyHeight);
		AddGuide(SideLeft, IconTop, SideWidth, IconSize);
		AddGuide(SideLeft, IconTop + IconSize, SideWidth, SectionsHeight);
		AddGuide(SideLeft, IconTop + IconSize + SectionsHeight, SideWidth, EntriesHeight);
	}

	private void AddGuide(int x, int y, int width, int height) {
		ReferenceRect guide = new() {
			BorderColor = new Color(1, 0, 0),
			BorderWidth = 1,
			EditorOnly = false,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		guide.SetPosition(new Vector2(x, y));
		guide.SetSize(new Vector2(width, height));
		AddChild(guide);
	}

	private HBoxContainer BuildHeader() {
		HBoxContainer header = new() {
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		header.AddThemeConstantOverride("separation", 6);

		Label title = new() { Text = "Codex" };
		title.AddThemeFontSizeOverride("font_size", 24);
		title.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		LineEdit search = new() {
			PlaceholderText = "Search (coming soon)",
			Editable = false,
			CustomMinimumSize = new Vector2(150, 0),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};

		header.AddChild(title);
		header.AddChild(new Control { CustomMinimumSize = new Vector2(TitleToNavGap, 0) });
		header.AddChild(MakeTextButton("\u25c0", 14, () => { }, disabled: true, tooltip: "Back (coming soon)"));
		header.AddChild(MakeTextButton("\u25b6", 14, () => { }, disabled: true, tooltip: "Forward (coming soon)"));
		header.AddChild(search);

		return header;
	}

	private VBoxContainer BuildSidePane(GameData gameData) {
		VBoxContainer right = new();
		right.AddThemeConstantOverride("separation", 6);

		// The icon fills the art's transparent top-right square.
		Control iconWrap = new() {
			CustomMinimumSize = new Vector2(IconSize, IconSize),
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
		};
		// Draw a black backdrop so circular leader heads don't show parchment behind the square.
		ColorRect iconBackdrop = new() {
			Color = Colors.Black,
		};
		iconBackdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		iconWrap.AddChild(iconBackdrop);
		icon = new TextureRect {
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
		};
		icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		iconWrap.AddChild(icon);
		right.AddChild(iconWrap);

		Label sectionsTitle = new() { Text = "SECTIONS" };
		sectionsTitle.AddThemeFontSizeOverride("font_size", 11);
		right.AddChild(IndentLabel(sectionsTitle, EntryTextInset));

		// Capped and scrollable so the sections list stays in the art's striped band.
		ScrollContainer sectionScroll = new() {
			CustomMinimumSize = new Vector2(0, SectionsHeight),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		right.AddChild(sectionScroll);

		VBoxContainer sectionList = new() {
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		sectionList.AddThemeConstantOverride("separation", 0);
		sectionScroll.AddChild(sectionList);

		foreach (CodexSection section in CodexCatalog.SectionOrder) {
			Button button = MakeTextButton(CodexCatalog.SectionTitle(section), 13, null);
			button.Alignment = HorizontalAlignment.Left;
			IndentButtonText(button, EntryTextInset);
			CodexSection captured = section;
			button.Pressed += () => SelectSection(gameData, captured);
			sectionList.AddChild(button);
			sectionButtons[section] = button;
		}

		indexHeader = new Label {
			ClipText = true,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
		};
		indexHeader.AddThemeFontSizeOverride("font_size", 11);
		right.AddChild(IndentLabel(indexHeader, EntryTextInset));

		ScrollContainer scroll = new() {
			CustomMinimumSize = new Vector2(0, EntriesHeight),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		right.AddChild(scroll);

		indexList = new VBoxContainer {
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		indexList.AddThemeConstantOverride("separation", 0);
		scroll.AddChild(indexList);

		right.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
		right.AddChild(BuildCloseButton());

		return right;
	}

	private MarginContainer BuildCloseButton() {
		MarginContainer wrap = new() {
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		wrap.AddThemeConstantOverride("margin_bottom", CloseButtonBottomGap);

		HBoxContainer row = new() {
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};

		TextureButton close = new();
		TextureLoader.SetButtonTextures(close, "ui.exit");
		close.Pressed += Close;
		row.AddChild(close);
		// A trailing spacer nudges the centered button left within the column.
		row.AddChild(new Control { CustomMinimumSize = new Vector2(CloseButtonShiftX * 2, 0) });

		wrap.AddChild(row);
		return wrap;
	}

	private VBoxContainer BuildMainPane() {
		VBoxContainer main = new();
		main.AddThemeConstantOverride("separation", 6);

		statLabel = new Label {
			AutowrapMode = TextServer.AutowrapMode.Word,
		};
		statLabel.AddThemeFontSizeOverride("font_size", 13);
		main.AddChild(statLabel);
		main.AddChild(new HSeparator());

		CreateArticleLabel();
		main.AddChild(articleLabel);

		pager = new HBoxContainer {
			Alignment = BoxContainer.AlignmentMode.Center,
			Visible = false,
		};
		pager.AddThemeConstantOverride("separation", 12);
		prevPage = MakeTextButton("< Previous", 13, () => ChangePage(-1));
		pager.AddChild(prevPage);
		pageLabel = new Label();
		pager.AddChild(pageLabel);
		nextPage = MakeTextButton("Next >", 13, () => ChangePage(1));
		pager.AddChild(nextPage);
		main.AddChild(pager);

		return main;
	}

	private void CreateArticleLabel() {
		GDScript markdownScript = ResourceLoader.Load<GDScript>("res://addons/markdownlabel/addons/markdownlabel/markdownlabel.gd");
		if (markdownScript != null) {
			try {
				RichTextLabel markdown = markdownScript.New().As<RichTextLabel>();
				// Do not let the markdown renderer open key:/URLs; navigation is a later phase.
				markdown.Set("automatic_links", false);
				articleLabel = markdown;
				articleIsMarkdown = true;
			} catch (Exception) {
				articleLabel = null;
			}
		}

		if (articleLabel == null) {
			articleLabel = new RichTextLabel();
			articleIsMarkdown = false;
		}

		articleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		articleLabel.SizeFlagsVertical = SizeFlags.ExpandFill;
		// RichTextLabel uses a separate theme font size per style, and MarkdownLabel
		// scales headings off normal_font_size, so set them all to keep the article uniform.
		articleLabel.AddThemeFontSizeOverride("normal_font_size", BodyFontSize);
		articleLabel.AddThemeFontSizeOverride("bold_font_size", BodyFontSize);
		articleLabel.AddThemeFontSizeOverride("italics_font_size", BodyFontSize);
		articleLabel.AddThemeFontSizeOverride("bold_italics_font_size", BodyFontSize);
		articleLabel.AddThemeFontSizeOverride("mono_font_size", BodyFontSize);
		articleLabel.BbcodeEnabled = true;
		articleLabel.ScrollActive = true;
		articleLabel.FitContent = false;
		// Civ3 tab tables are tuned to Civ3's font; 5-space stops best-fit the article's font.
		articleLabel.TabSize = 5;
		// The theme only styles Label, so match its font color explicitly.
		articleLabel.AddThemeColorOverride("default_color", new Color(0, 0, 0));
	}

	private void SelectSection(GameData gameData, CodexSection section) {
		activeSection = section;

		foreach (KeyValuePair<CodexSection, Button> pair in sectionButtons) {
			bool active = pair.Key == section;
			pair.Value.AddThemeColorOverride("font_color", active ? new Color(0.0f, 0.28f, 0.55f) : new Color(0.25f, 0.25f, 0.25f));
			pair.Value.AddThemeColorOverride("font_hover_color", new Color(0.55f, 0.27f, 0.0f));
		}

		indexHeader.Text = CodexCatalog.SectionTitle(section).ToUpperInvariant();

		foreach (Node child in indexList.GetChildren()) {
			indexList.RemoveChild(child);
			child.QueueFree();
		}

		List<CodexArticle> articles = CodexCatalog.Build(gameData, section);
		foreach (CodexArticle article in articles) {
			Button button = MakeTextButton(article.Name, 13, null);
			button.Alignment = HorizontalAlignment.Left;
			button.ClipText = true;
			button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
			IndentButtonText(button, EntryTextInset);
			CodexArticle captured = article;
			button.Pressed += () => ShowArticle(captured);
			indexList.AddChild(button);
		}

		if (articles.Count > 0) {
			ShowArticle(articles[0]);
		} else {
			ClearArticle();
		}
	}

	private void ShowArticle(CodexArticle article) {
		nameLabel.Text = string.IsNullOrEmpty(article.Name) ? "(unnamed)" : article.Name;

		icon.Texture = article.Icon ?? DefaultIcon();

		statLabel.Text = article.HasStats ? string.Join("\n", article.StatLines) : "";
		statLabel.Visible = article.HasStats;

		pages = article.Pages;
		pageIndex = 0;
		UpdatePage();
	}

	private void ClearArticle() {
		nameLabel.Text = "";
		icon.Texture = DefaultIcon();
		statLabel.Text = "";
		statLabel.Visible = false;
		pages = new List<string>();
		pageIndex = 0;
		UpdatePage();
	}

	private void ChangePage(int delta) {
		int next = pageIndex + delta;
		if (next < 0 || next >= pages.Count) {
			return;
		}
		pageIndex = next;
		UpdatePage();
	}

	private void UpdatePage() {
		string text = pages.Count > 0 ? pages[pageIndex] : "";
		if (articleIsMarkdown) {
			articleLabel.Set("markdown_text", text);
		} else {
			articleLabel.Text = text;
		}

		pager.Visible = pages.Count > 1;
		pageLabel.Text = pages.Count > 0 ? $"Page {pageIndex + 1} / {pages.Count}" : "";
		prevPage.Disabled = pageIndex <= 0;
		nextPage.Disabled = pageIndex >= pages.Count - 1;
	}

	private void Close() {
		(GetParent() as PopupOverlay)?.OnHidePopup();
	}

	public override void _UnhandledInput(InputEvent @event) {
		if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape) {
			Close();
			GetViewport().SetInputAsHandled();
		}
	}

	private static Button MakeTextButton(string text, int fontSize, Action onPressed, bool disabled = false, string tooltip = null) {
		Button button = new() {
			Text = text,
			Disabled = disabled,
			Flat = true,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		button.AddThemeFontSizeOverride("font_size", fontSize);
		if (tooltip != null) {
			button.TooltipText = tooltip;
		}
		if (onPressed != null) {
			button.Pressed += onPressed;
		}
		return button;
	}

	private static void IndentButtonText(Button button, int left) {
		// Shifts the text inside the row without changing the row's own size.
		foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" }) {
			if (button.GetThemeStylebox(state)?.Duplicate() is StyleBox style) {
				style.ContentMarginLeft = left;
				button.AddThemeStyleboxOverride(state, style);
			}
		}
	}

	private static MarginContainer IndentLabel(Label label, int left) {
		MarginContainer wrap = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		wrap.AddThemeConstantOverride("margin_left", left);
		wrap.AddChild(label);
		return wrap;
	}

	private bool TrySetPediaBackground() {
		try {
			Godot.ImageTexture bg = TextureLoader.Load("civilopedia_screen.background");
			if (bg == null) {
				return false;
			}

			// The parchment art is 1024x768 with transparent margins; draw it scaled to
			// fit the popup without distortion (letterboxed if needed).
			TextureRect background = new TextureRect {
				Texture = bg,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				SizeFlagsVertical = SizeFlags.ExpandFill,
			};
			background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(background);
			MoveChild(background, 0);
			return true;
		} catch (Exception) {
			return false;
		}
	}
}
