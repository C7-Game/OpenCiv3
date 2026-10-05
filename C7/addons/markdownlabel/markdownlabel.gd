@tool
class_name MarkdownLabel
extends RichTextLabel
## A control for displaying Markdown-style text.
##
## A custom node that extends [RichTextLabel] to use Markdown instead of BBCode.
## [br][br]
## [b][u]Usage:[/u][/b]
## Simply add a MarkdownLabel to the scene and write its [member markdown_text] field in Markdown format. Alternatively, you can use the  [method display_file] method to automatically import the contents of a Markdown file.
## [br][br]
## On its [RichTextLabel] properties: [member RichTextLabel.bbcode_enabled] property must be enabled. Do not touch the [member RichTextLabel.text] property, since it's used by MarkdownLabel to properly format its text. You can use the rest of its properties as normal.
## [br][br]
## You can still use BBCode tags that don't have a Markdown equivalent, such as `[u]underlined text[/u]`, allowing you to have the full functionality of RichTextLabel with the simplicity and readibility of Markdown.
## [br][br]
## Check out the full guide in the Github repo readme file (linked below). If encountering any unreported bug or unexpected bahaviour, please ensure that your Markdown is written as clean as possible, following best practices.
##
## @tutorial(Github repository): https://github.com/daenvil/MarkdownLabel

const _ESCAPE_PLACEHOLDER := ";$\uFFFD:%s$;"
const _ESCAPEABLE_CHARACTERS := "\\*_~`[]()\"<>#-+.!"
const _ESCAPEABLE_CHARACTERS_REGEX := "[\\\\\\*\\_\\~`\\[\\]\\(\\)\\\"\\<\\>#\\-\\+\\.\\!]"
const _CHECKBOX_KEY := "markdownlabel-checkbox"
const _FRONTMATTER_REGEX := r"^(?:(?:---|\+\+\+)\r?\n([\s\S]*?)\r?\n(?:---|\+\+\+)\r?\n)?(?:\r?\n)?([\s\S]*)$"

#region Public:
## Emitted when the node does not handle a click on a link. Can be used to execute custom functions when a link is clicked. [param meta] is the link metadata (in a regular link, it would be the URL).
signal unhandled_link_clicked(meta: Variant)
## Emitted when a task list checkbox is clicked. Arguments are:
## the id of the checkbox (used internally),
## the line number it is on (within the original Markdown text),
## a boolean representing whether the checkbox is now checked (true) or unchecked (false),
## and a string containing the text after the checkbox (within the same line).
signal task_checkbox_clicked(id: int, line: int, checked: bool, task_string: String)

## The text to be displayed in Markdown format.[br][br]
## [i]Note: the [member RichTextLabel.text] property is overridden so modifying it through code has the same effect as modifying [member markdown_text].[/i]
@export_multiline var markdown_text: String : set = _set_markdown_text

## If enabled, links will be automatically handled by this node, without needing to manually connect them. Valid header anchors will make the label scroll to that header's position. Valid URLs and e-mails will be opened according to the user's default settings.
@export var automatic_links := true
## If enabled, unrecognized links will be opened as HTTPS URLs (e.g. "example.com" will be opened as "https://example.com"). If disabled, unrecognized links will be left unhandled (emitting the [signal unhandled_link_clicked] signal). Ignored if [member automatic_links] is disabled.
@export var assume_https_links := true

@export_group("Header formats")
## Formatting options for level-1 headers
@export var h1 := H1Format.new() : set = _set_h1_format
## Formatting options for level-2 headers
@export var h2 := H2Format.new() : set = _set_h2_format
## Formatting options for level-3 headers
@export var h3 := H3Format.new() : set = _set_h3_format
## Formatting options for level-4 headers
@export var h4 := H4Format.new() : set = _set_h4_format
## Formatting options for level-5 headers
@export var h5 := H5Format.new() : set = _set_h5_format
## Formatting options for level-6 headers
@export var h6 := H6Format.new() : set = _set_h6_format

@export_group("Task lists")
## Whether task list checkboxes are clickable or not.
@export var enable_checkbox_clicks := true :
	set(new_value):
		enable_checkbox_clicks = new_value
		queue_update()
## String that will be displayed for unchecked task list items. Accepts BBCode and Markdown.
@export var unchecked_item_character := "☐" :
	set(new_value):
		unchecked_item_character = new_value
		queue_update()
## String that will be displayed for checked task list items. Accepts BBCode and Markdown.
@export var checked_item_character := "☑" :
	set(new_value):
		checked_item_character = new_value
		queue_update()

@export_group("Horizontal rules", "hr_")
## Height of horizontal rules. Only for Godot 4.5+.
@export_range(0, 99, 1, "suffix:px") var hr_height: int = 2 :
	set(new_value):
		hr_height = new_value
		queue_update()
## Width of horizontal rules, as a percentage of the label's width. Only for Godot 4.5+.
@export_range(0, 100, 1, "suffix:%") var hr_width: float = 90 :
	set(new_value):
		hr_width = new_value
		queue_update()
## Alignment of horizontal rules. Only for Godot 4.5+.
@export_enum("left", "center", "right") var hr_alignment: String = "center" :
	set(new_value):
		hr_alignment = new_value
		queue_update()
## Color of horizontal rules. Only for Godot 4.5+.
@export var hr_color: Color = Color.WHITE :
	set(new_value):
		hr_color = new_value
		queue_update()

#endregion

#region Private:
var _dirty: bool = false
var _converted_text: String
var _indent_level: int
var _escaped_characters_map := {}
var _current_paragraph: int = 0
var _header_anchor_paragraph := {}
var _header_anchor_count := {}
var _within_table := false
var _table_row := -1
var _skip_line_break := false
var _checkbox_id: int = 0
var _current_line: int = 0
var _checkbox_record := {}
var _debug_mode := false
var _frontmatter := ""
#endregion

#region Built-in methods:
@warning_ignore("shadowed_variable")
func _init(markdown_text: String = "") -> void:
	bbcode_enabled = true
	self.markdown_text = markdown_text
	if automatic_links:
		meta_clicked.connect(_on_meta_clicked)

func _ready() -> void:
	h1.changed.connect(queue_update)
	h2.changed.connect(queue_update)
	h3.changed.connect(queue_update)
	h4.changed.connect(queue_update)
	h5.changed.connect(queue_update)
	h6.changed.connect(queue_update)
	if Engine.is_editor_hint():
		bbcode_enabled = true

func _process(_delta: float) -> void:
	if _dirty:
		_update()

func _on_meta_clicked(meta: Variant) -> void:
	if typeof(meta) != TYPE_STRING:
		unhandled_link_clicked.emit(meta)
		return
	if meta.begins_with("{") and _CHECKBOX_KEY in meta:
		var parsed: Dictionary = JSON.parse_string(meta)
		if parsed[_CHECKBOX_KEY] and _checkbox_record[int(parsed.id)]:
			_on_checkbox_clicked(int(parsed.id), parsed.checked)
	if not automatic_links:
		unhandled_link_clicked.emit(meta)
		return
	if meta.begins_with("#") and meta in _header_anchor_paragraph:
		self.scroll_to_paragraph(_header_anchor_paragraph[meta])
		return
	var url_pattern := RegEx.new()
	url_pattern.compile("^(ftp|http|https):\\/\\/[^\\s\\\"]+$")
	var result := url_pattern.search(meta)
	if not result:
		url_pattern.compile("^mailto:[^\\s]+@[^\\s]+\\.[^\\s]+$")
		result = url_pattern.search(meta)
	if result:
		OS.shell_open(meta)
		return
	if assume_https_links:
		OS.shell_open("https://" + meta)
	else:
		unhandled_link_clicked.emit(meta)

func _validate_property(property: Dictionary) -> void:
	if property.name == "bbcode_enabled":
		property.usage = PROPERTY_USAGE_NO_EDITOR
	elif property.name == "text":
		property.usage = PROPERTY_USAGE_NONE # Don't store in scene file (markdown_text is already stored) nor show in editor

func _set(property: StringName, value: Variant) -> bool:
	if property == "text":
		_set_text(value)
		return true
	return false

func _get(property: StringName) -> Variant:
	if property == "text":
		return markdown_text
	return null

func _notification(what: int) -> void:
	if what == NOTIFICATION_TRANSLATION_CHANGED:
		queue_update()

#endregion

#region Public methods:
## Reads the specified file and displays it as markdown.[br][br]
## If [param handle_frontmatter] is [code]true[/code] (default), any valid YAML or TOML front-matter won't be displayed
## and will be saved to optionally be later retrieved using [method get_frontmatter].
## Otherwise, the document will be displayed as-is.
## Returns a [enum @GlobalScope.Error].
func display_file(file_path: String, handle_frontmatter: bool = true) -> Error:
	var result: Error = OK
	var content: String = FileAccess.get_file_as_string(file_path)
	if not content:
		result = FileAccess.get_open_error()
	if handle_frontmatter and result == OK:
		var regex := RegEx.create_from_string(_FRONTMATTER_REGEX)
		var regex_match := regex.search(content)
		if regex_match:
			_frontmatter = regex_match.get_string(1).strip_edges()
			markdown_text = regex_match.get_string(2)
			return OK
		else:
			result = ERR_BUG
	_frontmatter = ""
	markdown_text = content
	return result

## Returns the stored front-matter of the last file that was displayed using [method display_file], or an empty string otherwise.
func get_frontmatter() -> String:
	return _frontmatter

## Requests a redraw of the label, reprocessing the Markdown text.
func queue_update() -> void:
	_dirty = true
	queue_redraw()

#endregion

#region Private methods:
func _set_text(new_text: String) -> void:
	markdown_text = new_text
	queue_update()

func _set_markdown_text(new_text: String) -> void:
	markdown_text = new_text
	queue_update()

func _update() -> void:
	_dirty = false
	super.clear()
	var bbcode_text: String = _convert_markdown(TranslationServer.translate(markdown_text) as String if _can_auto_translate() else markdown_text)
	super.parse_bbcode(bbcode_text)

func _can_auto_translate() -> bool:
	var version := Engine.get_version_info()
	if version.hex >= 0x040300 and has_method("can_auto_translate"):
		return call("can_auto_translate")
	return auto_translate

func _set_h1_format(new_format: H1Format) -> void:
	h1 = new_format
	queue_update()

func _set_h2_format(new_format: H2Format) -> void:
	h2 = new_format
	queue_update()

func _set_h3_format(new_format: H3Format) -> void:
	h3 = new_format
	queue_update()

func _set_h4_format(new_format: H4Format) -> void:
	h4 = new_format
	queue_update()

func _set_h5_format(new_format: H5Format) -> void:
	h5 = new_format
	queue_update()

func _set_h6_format(new_format: H6Format) -> void:
	h6 = new_format
	queue_update()

func _convert_markdown(source_text: String = "") -> String:
	if not bbcode_enabled:
		push_warning("WARNING: MarkdownLabel node will not format Markdown syntax if it doesn't have 'bbcode_enabled=true'")
		return source_text
	_converted_text = ""
	var lines := source_text.split("\n")
	_current_line = 0
	_indent_level = -1
	var indent_spaces := []
	var indent_types := []
	var within_backtick_block := false
	var within_tilde_block := false
	var within_code_block := false
	var current_code_block_char_count: int
	_within_table = false
	_table_row = -1
	_skip_line_break = false
	_checkbox_id = 0

	for line: String in lines:
		line = line.trim_suffix("\r")
		_debug("Parsing line: '%s'" % line)
		within_code_block = within_tilde_block or within_backtick_block
		if _current_line > 0 and not _skip_line_break:
			_converted_text += "\n"
			_current_paragraph += 1
		_skip_line_break = false
		_current_line += 1
		
		line = _preprocess_line(line)
		
		# Handle fenced code blocks:
		if not within_tilde_block and _denotes_fenced_code_block(line, "`"):
			if within_backtick_block:
				if line.strip_edges().length() >= current_code_block_char_count:
					_converted_text = _converted_text.trim_suffix("\n")
					_current_paragraph -= 1
					_converted_text += "[/code]"
					within_backtick_block = false
					_debug("... closing backtick block")
					continue
			else:
				_converted_text += "[code]"
				within_backtick_block = true
				current_code_block_char_count = line.strip_edges().length()
				_debug("... opening backtick block")
				continue
		elif not within_backtick_block and _denotes_fenced_code_block(line, "~"):
			if within_tilde_block:
				if line.strip_edges().length() >= current_code_block_char_count:
					_converted_text = _converted_text.trim_suffix("\n")
					_current_paragraph -= 1
					_converted_text += "[/code]"
					within_tilde_block = false
					_debug("... closing tilde block")
					continue
			else:
				_converted_text += "[code]"
				within_tilde_block = true
				current_code_block_char_count = line.strip_edges().length()
				_debug("... opening tilde block")
				continue
		if within_code_block: #ignore any formatting inside code block
			_converted_text += _escape_bbcode(line)
			continue
		
		var _processed_line := line
		# Escape characters:
		_processed_line = _process_escaped_characters(_processed_line)
		
		# Process syntax:
		_processed_line = _process_table_syntax(_processed_line)
		_processed_line = _process_list_syntax(_processed_line, indent_spaces, indent_types)
		_processed_line = _process_inline_code_syntax(_processed_line)
		_processed_line = _process_image_syntax(_processed_line)
		_processed_line = _process_link_syntax(_processed_line)
		_processed_line = _process_hr_syntax(_processed_line)
		_processed_line = _process_text_formatting_syntax(_processed_line)
		_processed_line = _process_header_syntax(_processed_line)
		_processed_line = _process_custom_syntax(_processed_line)
		
		# Re-insert escaped characters:
		_processed_line = _reset_escaped_chars(_processed_line)
		
		_converted_text += _processed_line
	# end for line loop
	# Close any remaining open list:
	_debug("... end of text, closing all opened lists")
	for i in range(_indent_level, -1, -1):
		_converted_text += "[/%s]" % indent_types[i]
	# Close any remaining open tables:
	_debug("... end of text, closing all opened tables")
	if _within_table:
		_converted_text += "\n[/table]"
	
	_debug("** ORIGINAL:")
	_debug(source_text)
	_debug(_converted_text)
	return _converted_text

## This method is called before any syntax is processed by the node and does nothing by default. It can be overridden to customize how the node handles each line.
func _preprocess_line(line: String) -> String:
	return line

## This method is called after all Markdown syntax is processed by the node and does nothing by default. It can be overridden to handle custom additional syntax.
func _process_custom_syntax(line: String) -> String:
	return line

func _process_list_syntax(line: String, indent_spaces: Array, indent_types: Array) -> String:
	var processed_line := ""
	if line.length() == 0 and _indent_level >= 0:
		for i in range(_indent_level, -1, -1):
			_converted_text += "[/%s]" % indent_types[_indent_level]
			_indent_level -= 1
			indent_spaces.pop_back()
			indent_types.pop_back()
		_converted_text += "\n"
		_debug("... empty line, closing all list tags")
		return ""
	if _indent_level == -1:
		if line.length() > 2 and line[0] in "-*+" and line[1] == " ":
			_indent_level = 0
			indent_spaces.append(0)
			indent_types.append("ul")
			_converted_text += "[ul]"
			processed_line = line.substr(2)
			_debug("... opening unordered list at level 0")
			processed_line = _process_task_list_item(processed_line)
		elif line.length() > 3 and line[0] == "1" and line[1] == "." and line[2] == " ":
			_indent_level = 0
			indent_spaces.append(0)
			indent_types.append("ol")
			_converted_text += "[ol]"
			processed_line = line.substr(3)
			_debug("... opening ordered list at level 0")
		else:
			processed_line = line
		return processed_line
	var n_s := 0
	for _char in line:
		if _char == " " or _char == "\t":
			n_s += 1
			continue
		elif _char in "-*+":
			if line.length() > n_s + 2 and line[n_s + 1] == " ":
				if n_s == indent_spaces[_indent_level]:
					processed_line = line.substr(n_s + 2)
					_debug("... adding list element at level %d" % _indent_level)
					processed_line = _process_task_list_item(processed_line)
					break
				elif n_s > indent_spaces[_indent_level]:
					_indent_level += 1
					indent_spaces.append(n_s)
					indent_types.append("ul")
					_converted_text += "[ul]"
					processed_line = line.substr(n_s + 2)
					_debug("... opening list at level %d and adding element" % _indent_level)
					processed_line = _process_task_list_item(processed_line)
					break
				else:
					for i in range(_indent_level, -1, -1):
						if n_s < indent_spaces[i]:
							_converted_text += "[/%s]" % indent_types[_indent_level]
							_indent_level -= 1
							indent_spaces.pop_back()
							indent_types.pop_back()
						else:
							break
					_converted_text += "\n"
					processed_line = line.substr(n_s + 2)
					_debug("...closing lists down to level %d and adding element" % _indent_level)
					processed_line = _process_task_list_item(processed_line)
					break
		elif _char in "123456789":
			if line.length() > n_s + 3 and line[n_s + 1] == "." and line[n_s + 2] == " ":
				if n_s == indent_spaces[_indent_level]:
					processed_line = line.substr(n_s + 3)
					_debug("... adding list element at level %d" % _indent_level)
					break
				elif n_s > indent_spaces[_indent_level]:
					_indent_level += 1
					indent_spaces.append(n_s)
					indent_types.append("ol")
					_converted_text += "[ol]"
					processed_line = line.substr(n_s + 3)
					_debug("... opening list at level %d and adding element" % _indent_level)
					break
				else:
					for i in range(_indent_level, -1, -1):
						if n_s < indent_spaces[i]:
							_converted_text += "[/%s]" % indent_types[_indent_level]
							_indent_level -= 1
							indent_spaces.pop_back()
							indent_types.pop_back()
						else:
							break
					_converted_text += "\n"
					processed_line = line.substr(n_s + 3)
					_debug("... closing lists down to level %d and adding element" % _indent_level)
					break
	#end for _char loop
	if processed_line.is_empty():
		for i in range(_indent_level, -1, -1):
			_converted_text += "[/%s]" % indent_types[i]
			_indent_level -= 1
			indent_spaces.pop_back()
			indent_types.pop_back()
		_converted_text += "\n"
		processed_line = line
		_debug("... regular line, closing all opened lists")
	return processed_line

func _process_task_list_item(item: String) -> String:
	if item.length() <= 3 or item[0] != "[" or item[2] != "]" or item[3] != " " or not item[1] in " x":
		return item
	var processed_item := item.erase(0, 3)
	var checkbox: String
	var meta := {
		_CHECKBOX_KEY: true,
		"id": _checkbox_id
	}
	_checkbox_record[_checkbox_id] = _current_line - 1 # _current_line is actually the next line here
	_checkbox_id += 1
	if item[1] == " ":
		checkbox = unchecked_item_character
		meta.checked = false
		_debug("... item is an unchecked task item")
	elif item[1] == "x":
		checkbox = checked_item_character
		meta.checked = true
		_debug("... item is a checked task item")
	if enable_checkbox_clicks:
		processed_item = processed_item.insert(0, "[url=%s]%s[/url]" % [JSON.stringify(meta), checkbox])
	else:
		processed_item = processed_item.insert(0, checkbox)
	return processed_item

func _process_inline_code_syntax(line: String) -> String:
	var regex := RegEx.create_from_string("(`+)(.+?)\\1")
	var processed_line := line
	while true:
		var result := regex.search(processed_line)
		if not result:
			break
		var _start := result.get_start()
		var _end := result.get_end()
		var unescaped_content := _reset_escaped_chars(result.get_string(2), true)
		unescaped_content = _escape_bbcode(unescaped_content)
		unescaped_content = _escape_chars(unescaped_content)
		processed_line = processed_line.erase(_start, _end - _start).insert(_start, "[code]%s[/code]" % unescaped_content)
		_debug("... in-line code: " + unescaped_content)
	return processed_line

func _process_image_syntax(line: String) -> String:
	var processed_line := line
	var regex := RegEx.new()
	while true:
		regex.compile("\\!\\[(.*?)\\]\\((.*?)\\)")
		var result := regex.search(processed_line)
		if not result:
			break
		var found_proper_match := false
		var _start := result.get_start()
		var _end := result.get_end()
		regex.compile("\\[(.*?)\\]")
		var texts := regex.search_all(result.get_string())
		for _text in texts:
			if result.get_string()[_text.get_end()] != "(":
				continue
			found_proper_match = true
			var alt_text := result.get_string(1)
			# Check if link has a title:
			regex.compile("\\\"(.*?)\\\"")
			var title_result := regex.search(result.get_string(2))
			var title: String
			var url := result.get_string(2)
			if title_result:
				title = title_result.get_string(1)
				url = url.rstrip(" ").trim_suffix(title_result.get_string()).rstrip(" ")
			url = _escape_chars(url)
			processed_line = processed_line.erase(_start, _end - _start).insert(_start, "[img%s%s]%s[/img]" % [
				" alt=\"%s\"" % alt_text if alt_text else "",
				" tooltip=\"%s\"" % title if title_result and title else "",
				url,
			])
			_debug("... image: " + result.get_string())
			break
		if not found_proper_match:
			break
	return processed_line

func _process_link_syntax(line: String) -> String:
	var processed_line := line
	var regex := RegEx.new()
	while true:
		regex.compile("\\[(.*?)\\]\\((.*?)\\)")
		var result := regex.search(processed_line)
		if not result:
			break
		var found_proper_match := false
		var _start := result.get_start()
		var _end := result.get_end()
		regex.compile("\\[(.*?)\\]")
		var texts := regex.search_all(result.get_string())
		for _text in texts:
			if result.get_string()[_text.get_end()] != "(":
				continue
			found_proper_match = true
			# Check if link has a title:
			regex.compile("\\\"(.*?)\\\"")
			var title_result := regex.search(result.get_string(2))
			var title: String
			var url := result.get_string(2)
			if title_result:
				title = title_result.get_string(1)
				url = url.rstrip(" ").trim_suffix(title_result.get_string()).rstrip(" ")
			url = _escape_chars(url)
			processed_line = processed_line.erase(
				_start + _text.get_start(),
				_end - _start - _text.get_start()
			).insert(
				_start + _text.get_start(),
				"[url=%s]%s[/url]" % [url, _text.get_string(1)]
			)
			if title_result and title:
				processed_line = processed_line.insert(
					_start + _text.get_start() +12 +url.length() + _text.get_string(1).length(),
					"[/hint]"
				).insert(_start + _text.get_start(), "[hint=%s]" % title)
			_debug("... hyperlink: " + result.get_string())
			break
		if not found_proper_match:
			break
	while true:
		regex.compile("\\<(.*?)\\>")
		var result := regex.search(processed_line)
		if not result:
			break
		var _start := result.get_start()
		var _end := result.get_end()
		var url := result.get_string(1)
		regex.compile("^\\s*?([^\\s]+\\@[^\\s]+\\.[^\\s]+)\\s*?$")
		var mail := regex.search(result.get_string(1))
		if mail:
			url = mail.get_string(1)
		url = _escape_chars(url)
		if mail:
			processed_line = processed_line.erase(_start, _end - _start).insert(_start, "[url=mailto:%s]%s[/url]" % [url, url])
			_debug("... mail link: " + result.get_string())
		else:
			processed_line = processed_line.erase(_start, _end - _start).insert(_start, "[url]%s[/url]" % url)
			_debug("... explicit link: " + result.get_string())
	return processed_line

func _process_text_formatting_syntax(line: String) -> String:
	var processed_line := line
	# Bold text
	var regex := RegEx.create_from_string("(\\*\\*|\\_\\_)(.+?)\\1")
	while true:
		var result := regex.search(processed_line)
		if not result:
			break
		var _start := result.get_start()
		var _end := result.get_end()
		processed_line = processed_line.erase(_start, 2).insert(_start, "[b]")
		processed_line = processed_line.erase(_end - 1, 2).insert(_end - 1, "[/b]")
		_debug("... bold text: "+result.get_string(2))
	
	# Italic text
	while true:
		regex.compile("(\\*|_)(.+?)\\1")
		var result := regex.search(processed_line)
		if not result:
			break
		var _start := result.get_start()
		var _end := result.get_end()
		# Sanitize nested bold+italics (Godot-specific, b and i tags must not be intertwined):
		var result_string := result.get_string(2)
		var open_b := false
		var close_b := false
		if result_string.begins_with("[b]") and result_string.find("[/b]") == -1:
			open_b = true
		elif result_string.ends_with("[/b]") and result_string.find("[b]") == -1:
			close_b = true
		if open_b:
			processed_line = processed_line.erase(_start, 4).insert(_start, "[b][i]")
			processed_line = processed_line.erase(_end - 2, 1).insert(_end - 2, "[/i]")
		elif close_b:
			processed_line = processed_line.erase(_start, 1).insert(_start, "[i]")
			processed_line = processed_line.erase(_end - 3, 5).insert(_end - 3, "[/i][/b]")
		else:
			processed_line = processed_line.erase(_start, 1).insert(_start, "[i]")
			processed_line = processed_line.erase(_end + 1, 1).insert(_end + 1, "[/i]")
			
		_debug("... italic text: "+result.get_string(2))
	
	# Strike-through text
	regex.compile("(\\~\\~)(.+?)\\1")
	while true:
		var result := regex.search(processed_line)
		if not result:
			break
		var _start := result.get_start()
		processed_line = processed_line.erase(_start, 2).insert(_start, "[s]")
		var _end := result.get_end()
		processed_line = processed_line.erase(_end - 1, 2).insert(_end - 1, "[/s]")
		_debug("... strike-through text: " + result.get_string(2))
	
	return processed_line

func _process_header_syntax(line: String) -> String:
	var processed_line := line
	var regex := RegEx.create_from_string("^#+\\s*[^\\s].*")
	while true:
		var result := regex.search(processed_line)
		if not result:
			break
		var n := 0
		for _char in result.get_string():
			if _char != "#" or n == 6:
				break
			n+=1
		var n_spaces := 0
		for _char in result.get_string().substr(n):
			if _char != " ":
				break
			n_spaces += 1
		var header_format: Resource = _get_header_format(n)
		var _start := result.get_start()
		var opening_tags := _get_header_tags(header_format)
		processed_line = processed_line.erase(_start, n + n_spaces).insert(_start, opening_tags)
		var _end := result.get_end()
		processed_line = processed_line.insert(_end - (n + n_spaces) + opening_tags.length(), _get_header_tags(header_format, true))
		_debug("... header level %d" % n)
		_header_anchor_paragraph[_get_header_reference(result.get_string())] = _current_paragraph
		if header_format.get("draw_horizontal_rule"):
			var version := Engine.get_version_info()
			if version.hex >= 0x040500:
				processed_line += "\n[hr height=%d width=%d%% align=left color=%s]" % [
					hr_height,
					hr_width,
					hr_color.to_html(),
				]
				_current_line += 1
				_current_paragraph += 1
	return processed_line

func _process_hr_syntax(line: String) -> String:
	var version := Engine.get_version_info()
	if version.hex < 0x040500:
		return line
	var processed_line := line
	var regex := RegEx.create_from_string(r"^[ ]{0,3}([\-_*])\1{2,}\s*$")
	var result := regex.search(processed_line)
	if result:
		processed_line = "[hr height=%d width=%d%% align=%s color=%s]" % [
			hr_height,
			hr_width,
			hr_alignment,
			hr_color.to_html(),
		]
		_debug("... horizontal rule")
	return processed_line

func _escape_bbcode(source: String) -> String:
	return source.replacen("[",_ESCAPE_PLACEHOLDER).replacen("]","[rb]").replacen(_ESCAPE_PLACEHOLDER,"[lb]")

func _escape_chars(_text: String) -> String:
	var escaped_text := _text
	for _char: String in _ESCAPEABLE_CHARACTERS:
		if not _char in _escaped_characters_map:
			_escaped_characters_map[_char] = _escaped_characters_map.size()
		escaped_text = escaped_text.replacen(_char, _ESCAPE_PLACEHOLDER % _escaped_characters_map[_char])
	return escaped_text

func _reset_escaped_chars(_text: String,code:=false) -> String:
	var unescaped_text := _text
	for _char in _ESCAPEABLE_CHARACTERS:
		if not _char in _escaped_characters_map:
			continue
		unescaped_text = unescaped_text.replacen(_ESCAPE_PLACEHOLDER%_escaped_characters_map[_char],"\\"+_char if code else _char)
	return unescaped_text

func _debug(string: String) -> void:
	if not _debug_mode:
		return
	print(string)

func _denotes_fenced_code_block(line: String, character: String) -> bool:
	var stripped_line := line.strip_edges()
	var count := stripped_line.count(character)
	if count >= 3 and count==stripped_line.length():
		return true
	else:
		return false

func _process_escaped_characters(line: String) -> String:
	var regex := RegEx.create_from_string("\\\\" + _ESCAPEABLE_CHARACTERS_REGEX)
	var processed_line := line
	while true:
		var result := regex.search(processed_line)
		if not result:
			break
		var _start := result.get_start()
		var _escaped_char := result.get_string()[1]
		if not _escaped_char in _escaped_characters_map:
			_escaped_characters_map[_escaped_char] = _escaped_characters_map.size()
		processed_line = processed_line.erase(_start, 2).insert(_start, _ESCAPE_PLACEHOLDER % _escaped_characters_map[_escaped_char])
	return processed_line

func _process_table_syntax(line: String) -> String:
	if line.count("|") < 2:
		if _within_table:
			_debug ("... end of table")
			_within_table = false
			return "[/table]\n"+line
		else:
			return line
	_debug("... table row: "+line)
	_table_row += 1
	var split_line := line.trim_prefix("|").trim_suffix("|").split("|")
	var processed_line := ""
	if not _within_table:
		processed_line += "[table=%d]\n" % split_line.size()
		_within_table = true
	elif _table_row == 1:
		# Handle delimiter row
		var is_delimiter := true
		for cell in split_line:
			var stripped_cell := cell.strip_edges()
			if stripped_cell.count("-")+stripped_cell.count(":") != stripped_cell.length():
				is_delimiter = false
				break
		if is_delimiter:
			_skip_line_break = true
			return ""
	for cell in split_line:
		processed_line += "[cell]%s[/cell]" % cell.strip_edges()
	return processed_line

func _get_header_format(level: int) -> Resource:
	match level:
		1:
			return h1
		2:
			return h2
		3:
			return h3
		4:
			return h4
		5:
			return h5
		6:
			return h6
	push_warning("Invalid header level: "+str(level))
	return null

func _get_header_tags(header_format: Resource, closing := false) -> String:
	if not header_format:
		return ""
	var tags: String = ""
	if closing:
		if header_format.is_underlined:
			tags += "[/u]"
		if header_format.is_italic:
			tags += "[/i]"
		if header_format.is_bold:
			tags += "[/b]"
		if header_format.font_size:
			tags += "[/font_size]"
		if header_format.override_font_color and header_format.font_color:
			tags += "[/color]"
	else:
		if header_format.override_font_color and header_format.font_color:
			tags += "[color=#%s]" % header_format.font_color.to_html()
		if header_format.font_size:
			tags += "[font_size=%d]" % int(header_format.font_size * self.get_theme_font_size("normal_font_size"))
		if header_format.is_bold:
			tags += "[b]"
		if header_format.is_italic:
			tags += "[i]"
		if header_format.is_underlined:
			tags += "[u]"
	return tags

func _get_header_reference(header_string: String) -> String:
	var anchor := "#" + header_string.lstrip("#").strip_edges().to_lower().replace(" ","-")
	if anchor in _header_anchor_count:
		_header_anchor_count[anchor] += 1
		anchor += "-" + str(_header_anchor_count[anchor]-1)
	else:
		_header_anchor_count[anchor] = 1
	return anchor

func _on_checkbox_clicked(id: int, was_checked: bool) -> void:
	var iline: int = _checkbox_record[id]
	var lines := markdown_text.split("\n")
	var old_string := "[x]" if was_checked else "[ ]"
	var new_string := "[ ]" if was_checked else "[x]"
	var i := lines[iline].find(old_string)
	if i == -1:
		push_error("Couldn't find the clicked task list checkbox (id=%d, line=%d)" % [id, iline]) # Shouldn't happen. Please report the bug if it happens.
		return
	lines[iline] = lines[iline].erase(i, old_string.length()).insert(i, new_string)
	_set("text", "\n".join(lines)) #calling [text] directly from this class does not use [_set()].
	task_checkbox_clicked.emit(id, iline, !was_checked, lines[iline].substr(i + 4))
#endregion
