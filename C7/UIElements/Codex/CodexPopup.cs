using System;
using System.Collections.Generic;
using C7Engine;
using C7GameData;
using Godot;

// The Codex (Civilopedia) popup, opened from the top-left toolbar.
public partial class CodexPopup : Popup {
	private const int WindowWidth = 1060;
	private const int WindowHeight = 700;
	private const int ContentMargin = 26;
	private const int LeftPaneWidth = 250;
	private const int IconSize = 64;
	private const int BodyFontSize = 13;

	private readonly Dictionary<CodexSection, Button> sectionButtons = new();
	private VBoxContainer indexList;
	private Label indexHeader;
	private CodexSection activeSection = CodexSection.Units;

	private TextureRect icon;
	private Label nameLabel;
	private Label statLabel;
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
		AddBackground(WindowWidth, WindowHeight);

		EngineStorage.ReadGameData(BuildUi);
	}

	private void BuildUi(GameData gameData) {
		if (gameData == null) {
			return;
		}

		MarginContainer root = new();
		root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		root.AddThemeConstantOverride("margin_left", ContentMargin);
		root.AddThemeConstantOverride("margin_top", ContentMargin);
		root.AddThemeConstantOverride("margin_right", ContentMargin);
		root.AddThemeConstantOverride("margin_bottom", ContentMargin);
		AddChild(root);

		VBoxContainer column = new() {
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		column.AddThemeConstantOverride("separation", 8);
		root.AddChild(column);

		column.AddChild(BuildHeader());
		column.AddChild(new HSeparator());

		HBoxContainer body = new() {
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		body.AddThemeConstantOverride("separation", 12);
		column.AddChild(body);

		body.AddChild(BuildLeftPane(gameData));
		body.AddChild(new VSeparator());
		body.AddChild(BuildRightPane());

		SelectSection(gameData, CodexSection.Units);
	}

	private HBoxContainer BuildHeader() {
		HBoxContainer header = new() {
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		header.AddThemeConstantOverride("separation", 6);

		header.AddChild(MakeTextButton("\u25c0", 14, () => { }, disabled: true, tooltip: "Back (coming soon)"));
		header.AddChild(MakeTextButton("\u25b6", 14, () => { }, disabled: true, tooltip: "Forward (coming soon)"));

		LineEdit search = new() {
			PlaceholderText = "Search (coming soon)",
			Editable = false,
			CustomMinimumSize = new Vector2(220, 0),
		};
		header.AddChild(search);

		header.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

		Label title = new() { Text = "Codex" };
		title.AddThemeFontSizeOverride("font_size", 24);
		title.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		header.AddChild(title);

		header.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

		TextureButton close = new();
		TextureLoader.SetButtonTextures(close, "ui.exit");
		close.Pressed += Close;
		header.AddChild(close);

		return header;
	}

	private VBoxContainer BuildLeftPane(GameData gameData) {
		VBoxContainer left = new() {
			CustomMinimumSize = new Vector2(LeftPaneWidth, 0),
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		left.AddThemeConstantOverride("separation", 4);

		Label sectionsTitle = new() { Text = "SECTIONS" };
		sectionsTitle.AddThemeFontSizeOverride("font_size", 11);
		left.AddChild(sectionsTitle);

		VBoxContainer sectionList = new();
		sectionList.AddThemeConstantOverride("separation", 0);
		left.AddChild(sectionList);

		foreach (CodexSection section in CodexCatalog.SectionOrder) {
			Button button = MakeTextButton(CodexCatalog.SectionTitle(section), 13, null);
			button.Alignment = HorizontalAlignment.Left;
			CodexSection captured = section;
			button.Pressed += () => SelectSection(gameData, captured);
			sectionList.AddChild(button);
			sectionButtons[section] = button;
		}

		left.AddChild(new HSeparator());

		indexHeader = new Label();
		indexHeader.AddThemeFontSizeOverride("font_size", 11);
		left.AddChild(indexHeader);

		ScrollContainer scroll = new() {
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		left.AddChild(scroll);

		indexList = new VBoxContainer {
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		indexList.AddThemeConstantOverride("separation", 0);
		scroll.AddChild(indexList);

		return left;
	}

	private VBoxContainer BuildRightPane() {
		VBoxContainer right = new() {
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		right.AddThemeConstantOverride("separation", 6);

		HBoxContainer header = new();
		header.AddThemeConstantOverride("separation", 12);

		icon = new TextureRect {
			CustomMinimumSize = new Vector2(IconSize, IconSize),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		header.AddChild(icon);

		nameLabel = new Label {
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		nameLabel.AddThemeFontSizeOverride("font_size", 22);
		header.AddChild(nameLabel);

		right.AddChild(header);
		right.AddChild(new HSeparator());

		statLabel = new Label {
			AutowrapMode = TextServer.AutowrapMode.Word,
		};
		statLabel.AddThemeFontSizeOverride("font_size", 13);
		right.AddChild(statLabel);
		right.AddChild(new HSeparator());

		CreateArticleLabel();
		right.AddChild(articleLabel);

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
		right.AddChild(pager);

		return right;
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

		icon.Texture = article.Icon;
		icon.Visible = article.Icon != null;

		statLabel.Text = article.HasStats ? string.Join("\n", article.StatLines) : "";
		statLabel.Visible = article.HasStats;

		pages = article.Pages;
		pageIndex = 0;
		UpdatePage();
	}

	private void ClearArticle() {
		nameLabel.Text = "";
		icon.Texture = null;
		icon.Visible = false;
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
}
