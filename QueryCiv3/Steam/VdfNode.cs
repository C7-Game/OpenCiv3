using System;
using System.Collections.Generic;
using System.Linq;

namespace QueryCiv3.Steam;

public class VdfNode {
	public string Name { get; }
	public string Value { get; } // null for section nodes
	public List<VdfNode> Children { get; } = new List<VdfNode>();

	public VdfNode(string name, string value) {
		Name = name;
		Value = value;
	}

	public IEnumerable<VdfNode> FindAll(string name) {
		return Children.Where(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
	}

	public VdfNode Find(string name) {
		return FindAll(name).FirstOrDefault();
	}
}
