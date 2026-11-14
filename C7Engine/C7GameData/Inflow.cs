using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Lua;
using C7GameData.Save;

namespace C7GameData;

// TODO: ideas for other city related stuff that are not yet implemented;
// pollution + -, luxury happy face + -, some tile modifier? ,
// more/less units in  the city -> happier/sadder population

// We should keep these names in all lower case form
// to avoid any case mismatch between this code, the json and lua.
// It's not pretty but at least it will be consistent across all these.
public enum InflowYield {
	commerce,
	culture,
	science,
	happiness,
	maintenance,
	unitsupport,
	corruption,
}
public class Inflow : IProducible {
	public string name { get; set; }
	public int populationCost { get; set; }
	public Tech requiredTech { get; set; }
	public HashSet<Resource> requiredResources { get; set; }

	public int iconRowIndex;
	public List<LocalYield> localYield { get; set; }
	// TODO: Implement a globalYield where for example, 10 cities must be producing this in order for something to happen

	public int ShieldCost(Player player, float costFactor) {
		// TODO: add the option to consume shields
		return 0;
	}

	public bool CanProduce(City city, HashSet<Resource> accessibleResources) {
		// TODO: add the option to unlock after researching a tech, or have a certain building, or having a certain resource
		// as well as rendering it obsolete by a building/tech/resource. Basically make this as configurable as it can be
		return true;
	}

	public Inflow(SaveInflow saveInflow, BehaviorEngine luaRulesEngine) {
		this.name = saveInflow.name;
		this.iconRowIndex = saveInflow.iconRowIndex;
		this.localYield = saveInflow.localYield.ConvertAll(y => new LocalYield(y.yieldType, luaRulesEngine, y.yieldCalculation));
	}

	public Func<ScriptContext, int> GetInflowYieldFunc(InflowYield yieldType) {
		return this.localYield.FirstOrDefault(y => y.yieldType == yieldType).yieldCalculation;
	}

	public bool TryGetInflowYieldFunc(InflowYield yieldType, out Func<ScriptContext, int> yieldFunc) {
		Func<ScriptContext, int> yieldCalculation = GetInflowYieldFunc(yieldType);
		if (yieldCalculation != null) {
			yieldFunc = yieldCalculation;
			return true;
		}

		yieldFunc = null;
		return false;
	}
}

public struct LocalYield {
	public InflowYield yieldType { get; set; }
	public readonly Func<ScriptContext, int> yieldCalculation;

	public LocalYield() { }
	public LocalYield(InflowYield type, BehaviorEngine rulesEngine, string yieldCalculation = null) {
		this.yieldType = type;
		if (yieldCalculation != null) {
			this.yieldCalculation = rulesEngine.ImportFunc<Func<ScriptContext, int>>(yieldCalculation);
		}
	}
}
public struct SaveLocalYield {
	public InflowYield yieldType { get; set; }
	public string yieldCalculation;

	public SaveLocalYield() { }
	public SaveLocalYield(InflowYield type, string yieldCalculation) {
		this.yieldType = type;
		this.yieldCalculation = yieldCalculation;
	}
}

public struct ScriptContext(Player player, City city) {
	public Player player = player;
	public City city = city;
}
