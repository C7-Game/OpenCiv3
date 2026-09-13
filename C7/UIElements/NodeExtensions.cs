using Godot;

public static class NodeExtensions {
	/// <summary>
	/// Recursively set MouseFilter on the node and all of its descendants.
	/// </summary>
	/// <param name="node">Root of the subtree to update.</param>
	/// <param name="filter">MouseFilterEnum to apply to every Control in the subtree.</param>
	public static void SetMouseFilterRecursive(this Node node, Control.MouseFilterEnum filter) {
		foreach (var child in node.GetChildren()) {
			child.SetMouseFilterRecursive(filter);
		}
		if (node is Control control) {
			control.MouseFilter = filter;
		}
	}
}
