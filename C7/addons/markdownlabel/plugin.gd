@tool
extends EditorPlugin

func _enter_tree() -> void:
	# Initialization of the plugin goes here.
	# Add the new type with a name, a parent type, a script and an icon.
	add_custom_type("MarkdownLabel", "RichTextLabel", preload("markdownlabel.gd"), preload("icon.svg"))


func _exit_tree() -> void:
	remove_custom_type("MarkdownLabel")
