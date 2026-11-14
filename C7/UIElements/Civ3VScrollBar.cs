using Godot;

namespace C7.UIElements;

[GlobalClass]
[Tool]
public partial class Civ3VScrollBar : VScrollBar, ICiv3Range {
	public Civ3RangeTheme rangeTheme { get; init; }

	public Civ3VScrollBar() {
		this.rangeTheme = new Civ3RangeTheme(this);
	}

	public override void _Notification(int what) {
		this.rangeTheme.ClearAndRestoreOverrides(what);
	}
}
