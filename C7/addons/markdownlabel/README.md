# MarkdownLabel

A custom [Godot](https://godotengine.org/) node that extends [RichTextLabel](https://docs.godotengine.org/en/stable/classes/class_richtextlabel.html) to use Markdown instead of BBCode.

Compatible with **Godot 4.2+**. Contains uid files for Godot 4.4+.

### Contents

- [Disclaimer](#disclaimer)
- [Installation](#installation)
- [Usage](#usage)
  - [Basic syntax](#basic-syntax)
  - [Code](#code)
  - [Headers](#headers)
  - [Links](#links)
  - [Horizontal rules](#horizontal-rules)
  - [Images](#images)
  - [Lists](#lists)
    - [Task list items (checkboxes)](#task-list-items)
  - [Tables](#tables)
  - [Escaping characters](#escaping-characters)
  - [Internationalization](#internationalization)
  - [Advanced usage](#advanced-usage)
- [Limitations](#limitations)
  - [Unsupported syntax elements](#unsupported-syntax-elements)
  - [Performance](#performance)
- [Acknowledgements](#acknowledgements)

## Disclaimer

I created this for my own use and figured out someone else might as well have some use for it. Obviously using BBCode will be better performance-wise since it's natively integrated in Godot. But using Markdown is much easier to write and read, so it can save development time in many cases.

I coded this quickly and without previous knowledge of how to parse Markdown properly, so there might be some inefficiencies and bugs. Please report any unexpected behavior.

I might convert this to C++ code at some point, to improve performance.

### Intended use case

This node is very useful for static text that you want to display in your application. It's not recommended to use this for text which is dynamically modified at run time.

My initial use case that lead me to do this was to directly include text from files in my game, such as credits and patch notes, in a format that is easier to mantain for me. This has the added benefit of being able to use the same Markdown files that are displayed in a github repository, instead of having to make two versions of the same text in two different formats.

## Installation

**From Github:**

1. Download the `addons` folder of this repository
2. Place it in your project's root folder (merge it if you already have an 'addons' folder)
3. Go to `Project > Project Settings... > Plugins` and enable the MarkdownLabel plugin

**From Godot:**

1. Go to the AssetLib tab and search for MarkdownLabel
2. Click "Download" and then "Install"
3. Go to `Project > Project Settings... > Plugins` and enable the MarkdownLabel plugin

You might need to reload the project.

## Usage

Simply add a MarkdownLabel to the scene and write its `markdown_text` field in Markdown format. Alternatively, you can use the `display_file` method to automatically import the contents of a Markdown file.

In the RichTextLabel properties:
- Do not touch the `bbcode_enabled` property, since it's automatically set to `true` by MarkdownLabel to properly format its text. It's hidden from the editor to prevent misusage.
- Editing the `text` property has the same effect as editing `markdown_text`. It's hidden from the editor to prevent misusage.
- You can use the rest of its properties as normal.

You can still use BBCode tags that don't have a Markdown equivalent, such as `[color=green]underlined text[/color]`, allowing you to have the full functionality of RichTextLabel with the simplicity and readibility of Markdown.

You can access the in-editor documentation in Godot by going to "Help" > "Search Help" and search for MarkdownLabel.

![An example of the node being used to display this Markdown file](addons/markdownlabel/assets/screenshot.png "An example of the node being used to display this Markdown file")
*An example of the node being used to display this Markdown file.*

### Basic syntax

The basic Markdown syntax works in the standard way:
```
Markdown text ................ -> BBCode equivalent
-------------------------------||------------------
**Bold** or __bold__ ......... -> [b]Bold[/b] or [b]bold[/b]
*Italics* or _italics_ ....... -> [i]Italics[/i] or [i]italics[/i]
***Nested*** *__emphasis__* .. -> [b][i]Nested[/i][b] [i][b]emphasis[/b][/i]
~~Strike-through~~ ........... -> [s]Strike-through[/s]
```

### Code

You can display code in-line by surrounding text with any number of backticks (\`), and you can display code in multiple lines (also called a fenced code block) by placing a line containing just three or more backticks (\`\`\`) or tildes (\~\~\~) above and below your code block.

Examples:
```
Markdown text ................. -> BBCode equivalent
--------------------------------||------------------
The following is `in-line code` -> The following is [code]in-line code[/code]
This is also ``in-line code``   -> The is also [code]in-line code[/code]

~~~                  .......... -> [code]
This is a            .......... -> This is a
multiline codeblock  .......... -> multiline codeblock
~~~                  .......... -> [/code]

```

**Important**: note that in-line code and code blocks **won't do anything with Godot's default font**, since it doesn't have a monospace variant. As described in [Godot's BBCode reference](https://docs.godotengine.org/en/stable/tutorials/ui/bbcode_in_richtextlabel.html#reference): *"The monospaced (`[code]`) tag only works if a custom font is set up in the RichTextLabel node's theme overrides. Otherwise, monospaced text will use the regular font"*.

### Headers

MarkdownLabel supports headers, although RichTextLabel doesn't. By default, a line defined as a header will have its font size scaled by a pre-defined amount.

To define a line as a header, begin it with any number of consecutive hash symbols (#) and follow it with the title of your header. The number of hash symbols defines the level of the header. The maximum supported level is six.

Example:
```
Markdown text:
## This is a second-level header

BBCode equivalent:
[font_size=27]This is a second-level header[/font_size]
```
where the `27` in `[font_size=27]` comes from multiplying the set `h2.font_size` (`1.714` by default) by the current `normal_font_size` (`16` by default).

You can optionally set custom sizes and formatting (bold, italics, underline, and color) for each header level individually. To do so:

- In the inspector, open the "Header formats" category, click on the resource associated with the desired header level, and customize the properties there.
- In script, access those properties through the `h1`, `h2`, etc. properties. Example: `$YourMarkdownLabel.h3.is_italic = true` will set all level-3 headers within `$YourMarkdownLabel` to be displayed as italics.

Note: to change a header level's font color, it's not enough with changing the ``font_color`` property: you also have to set its ``override_font_color`` property to ``true``.

Of course, you can also use basic formatting within the headers (e.g. `### Header with **bold** and *italic* words`).

### Links

Links follow the standard Markdown syntax of `[text to display](https://example.com)`. Additionally, you can add tooltips to your links with `[text to display](https://example.com "Some tooltip")`.

"Autolinks" are also supported with their standard syntax: `<https://example.com>`, and `<mail@example.com>` for mail autolinks.

Links created this way will be automatically handled by MarkdownLabel, implementing their expected behaviour:

- Valid header anchors (such as the ones in [Contents](#contents)) will make MarkdownLabel scroll to their header's position.
- Valid URLs and emails will be opened according to the user's settings (usually, using their default browser).
- Links that do not match any of the above conditions will be interpreted as a URL by prefixing them with "https://". E.g. `[link](example.com)` will link to "https://example.com". This can be disabled using the ``assume_https_links`` property (enabled by default), in which case the ``unhandled_link_clicked`` signal will be emitted.

This behavior can be disabled using the `automatic_links` property (enabled by default), in which case all links will be left unhandled and the ``unhandled_link_clicked`` signal will be emitted for all of them.

The ``unhandled_link_clicked`` signal can be used to implement custom behavior when clicking a link. It passes the clicked link metadata (which usually would be the URL) as an argument.

```
Markdown text ...................................... -> BBCode equivalent
-----------------------------------------------------||------------------
[this is a link](https://example.com) .............. -> [url=https://example.com]this is a link[/url]
[this is a link](https://example.com "Example page") -> [hint=Example url][url=https://example.com]this is a link[/url][/hint]
<https://example.com> .............................. -> [url]https://example.com[/url]
<mail@example.com> ................................. -> [url=mailto:mail@example.com]mail@example.com[/url]
```

### Horizontal rules

_**Note:** this feature is exclusive for **Godot 4.5+.**_

Write a line with just three (or more) matching ``-``, ``_``, or ``*`` to form a horizontal rule (also called a thematic break).

You can customize the height, width, alignment, and color of all horizontal rules in the label by modifying its ``hr_height``, ``hr_width``, ``hr_alignment``, and ``hr_color`` properties (by default they are 2 px high, 90% wide, center-aligned, and white). If you need to customize these properties individually for each rule, then you can just use the ``[hr]`` BBCode tag.

```
Markdown text  -> BBCode equivalent
---------------||------------------
---            -> [hr height=2 width=90% align=center color=ffffffff]
 ____          -> [hr height=2 width=90% align=center color=ffffffff]
***            -> [hr height=2 width=90% align=center color=ffffffff]
```

### Images

Images use the same syntax as links but preceded by an exclamation mark (!):

```
Markdown text .............................................. -> BBCode equivalent
-------------------------------------------------------------||------------------
![This is an image](res://some/path.png) ................... -> [img alt="This is an image"]res://some/path.png[/img]
![This is an image](res://some/path.png "This is a tooltip") -> [img alt="This is an image" tooltip="This is a tooltip"]res://some/path.png[/img]
![](res://some/path.png) ................................... -> [img]res://some/path.png[/img]
```
What you put inside the square brackets will be the image's alt text.

For advanced usage (setting width, height, and other options), use the BBCode `[img]` tag instead, as described in [Godot's BBCode reference](https://docs.godotengine.org/en/stable/tutorials/ui/bbcode_in_richtextlabel.html#reference).

### Lists

Unordered list elements begin with a dash (-), asterisk (*), or plus sign (+) followed by a space.

Ordered list elements begin with a number from 1 to 9 followed by a single dot and a space.

To begin a list, you must write the first element without indentation and, in the case of ordered lists, the first element must begin with the number 1.

From there, you add elements in consecutive lines (do not leave blank lines between elements), and you can open nested lists by indenting new elements any number of spaces or tabs.

Examples:

Markdown text:
```
1. First element of an unordered list
2. Second element
  1. Nested element
1. Third element. The number at the beginning doesn't need to match the actual order. It's only relevant for the first element.
  - You can also nest unordered lists inside ordered lists, and viceversa
	1. This is a nested list inside another nested list.
```
BBCode equivalent:
```
[ol]First element of an unordered list
Second element
[ol]Nested element[/ol]
Third element. The number at the beginning doesn't need to match the actual order. It's only relevant for the first element.
[ul]You can also nest unordered lists inside ordered lists, and viceversa
[ol]This is a nested list inside another nested list.[/ol]
[/ul][/ol]
```

#### Task list items

A task list item is an unordered list item which begins with ``[ ]`` or ``[x]`` followed by a space. These characters are, by default, replaced with a checkbox icon when converting the text (☐ and ☑, respectively). These checkbox characters depend on the used font and may not display properly, so they can be customized using the ``unchecked_item_character`` and ``checked_item_character`` properties, where you can even insert an image using BBCode or Markdown syntax.

When clicking on a checkbox, it automatically checks/unchecks itself and emits the ``task_checkbox_clicked`` signal. This behavior can be disabled with the ``enable_checkbox_clicks`` property.

The arguments of the ``task_checkbox_clicked`` signal are:

- The id of the checkbox (used internally)
- The line number it is on (within the original Markdown text)
- A boolean representing whether the checkbox is now checked (true) or unchecked (false)
- A string containing the text after the checkbox (within the same line).

Example (run the ``example.tscn`` scene to test it):

- [ ] This is an unchecked item
  - [x] This is a nested task
- [x] This is a checked item
  1. This is a nested regular list
  2. Here goes another nested task list:
    - [ ] Task 1
    - [ ] Task 2

### Tables

Tables are constructed by separating columns with pipes (`|`).

Example:

Markdown text:
````
| cell1 | cell2 |
| cell3 | cell4 |
````

BBCode equivalent:
````
[table=2]
[cell]cell1[/cell][cell]cell2[/cell]
[cell]cell3[/cell][cell]cell4[/cell]
[/table]
````

Note that [delimiter rows](https://github.github.com/gfm/#delimiter-row) are optional and will be ignored, since Godot's BBCode doesn't support cell alignment.

Example:
````
| cell1 | cell2 |
| ----: | :---- |
| cell3 | cell4 |
````
The above Markdown table will produce the same BBCode output as the previous example.

For advanced usage (setting ratio, border, background, etc.), use the BBCode `[table]` tag instead, as described in [Godot's BBCode reference](https://docs.godotengine.org/en/stable/tutorials/ui/bbcode_in_richtextlabel.html#reference).

### Escaping characters

You can escape characters using a backlash if you don't want them to form a Markdown syntax element. You can escape backlashes if you don't want them to escape the following character. You can't escape characters inside in-line or fenced code, since the string will be displayed as-is. You also don't need to escape characters inside a link or image url.

Examples:
```
Markdown text ............................ -> BBCode equivalent
-------------------------------------------||------------------
These \**outer asterisks*\* are escaped .. -> These *[i]outer asterisks[/i]* are escaped
This \\*asterisk* is not escaped ......... -> \[i]This asterisk[/i] is not escaped
`This \\*asterisk* is inside in-line code` -> [code]This \\*asterisk* is inside in-line code[/code]
[Link](url_with_backlashes.net) .......... -> [url=url_with_backlashes.net]Link[/url]
```

Note: to escape an ordered list, you must escape the dot that follows the number, e.g. `1\. Not a list`.

Keep in mind that, if you are writing text inside a script, you will have to "double escape" backlashes, since you are writing in a string. Some other characters, such as double-quotes, also need in-script escaping:

- In-script: `\\*`, `\\\"`
- In-editor: `\*`, `\"`
- Result: `*`, `"`

### Internationalization

By default, if ``markdown_text`` is set to a translation key, the label will automatically show the corresponding localized text. This is the same behavior as in other control nodes such as ``Label`` and ``RichTextLabel``. This can be disabled by modifying the node's [``auto_translate_mode``](https://docs.godotengine.org/en/stable/classes/class_node.html#class-node-property-auto-translate-mode) property (Godot 4.3+) or the ``auto_translate`` property (Godot 4.2). Read [the official docs on game internationalization](https://docs.godotengine.org/en/stable/tutorials/i18n/internationalizing_games.html) for more information.

### Advanced usage

#### Front-matter

By default, the ``display_file()`` method separates YAML/TOML front-matter (if present) and actual Markdown content, only displaying the Markdown content. The front-matter is stored internally as a string and can be retrieved using the ``get_frontmatter()`` method afterwards.

#### Custom syntax

MarkdownLabel can be customized to handle custom syntax if desired. There are two methods which are meant to support this use case: ``_preprocess_line()`` and ``_process_custom_syntax()``. These are called line by line and do nothing by default. ``_preprocess_line()`` is called before any syntax in the line is processed by the node, while ``_process_custom_syntax()`` is called after all syntax has been processed. These methods take a line as argument and return a processed line. This way, you can create a node that inherits from MarkdownLabel and override these methods in order to implement your custom syntax.

For even more advanced customization, you can override other built-in methods, like ``_process_text_formatting_syntax()`` or ``_process_link_syntax()``. Check the source code for more information.

## Limitations

Keep in mind that this is not supposed to be a full Markdown implementation, it just provides a Markdown interface to Godot's BBCode support and, as such, is limited by it.

If encountering any unreported bug or unexpected bahaviour, please ensure that your Markdown is written as clean as possible, following best practices (I wrote this primarily taking [Commonmark](https://commonmark.org/) and [Github-flavoured Markdown](https://github.github.com/gfm/) as reference, but it has its own peculiarities due to the use of Godot's BBCode).

### Unsupported syntax elements

The following Markdown syntax elements are not supported because Godot's BBCode does not support them:
- Quotes
- Reference links

### Performance

This node basically parses the whole text, converting it from Markdown to BBCode at runtime, so it may produce performance issues with some extreme usages, such as very large and heavily-formatted texts or updating a heavily-formatted text very frequently. That already can happen with BBCode, though, so in that case, you are probably better off [using RichTextLabel's functions](https://docs.godotengine.org/en/stable/tutorials/ui/bbcode_in_richtextlabel.html#using-push-tag-and-pop-functions-instead-of-bbcode) instead of writing the formatting directly in-text.

### Acknowledgements

The syntax and implementation of MarkdownLabel is largely based on [Github-flavored Markdown](https://github.github.com/gfm/) and [CommonMark](https://commonmark.org/), with its own quirks to accomodate it within [Godot's RichTextLabel BBCode](https://docs.godotengine.org/en/stable/tutorials/ui/bbcode_in_richtextlabel.html).
