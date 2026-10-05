extends Control


func _ready() -> void:
	%MarkdownLabelFile.display_file("res://addons/markdownlabel/README.md")
	%MarkdownLabelFile.task_checkbox_clicked.connect(
		func(id: int, line: int, checked: bool, text: String) -> void:
			print("%s task #%d on line %d: %s" % ["Checked" if checked else "Unchecked", id, line, text])
	)
