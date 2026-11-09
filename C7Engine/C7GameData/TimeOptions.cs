using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Serialization;
using C7Engine;
using Serilog;

namespace C7GameData;

public enum TimeUnit {
	Years,
	Months,
	Weeks,
	Days,
	Hours,
}

public sealed class TimeOptions {
	public TimeUnit baseUnit { get; init; } = TimeUnit.Years;
	public string negativeLabel { get; init; } = "BC";
	public string positiveLabel { get; init; } = "AD";
	public int startYear { get; init; } = -4000;
	public int startMonth { get; init; } = 1;

	public int startWeek { get; init; } = 1;
	// custom
	public int startDay { get; init; } = 1;
	// custom
	public int startHour { get; init; } = 12;
	public int[,] timeScale { get; set; } = new int[,] { { 25, 25, 40, 50, 100, 100, 100, 50000 }, { 50, 40, 25, 20, 10, 5, 2, 1 } };

	public int turnLimit { get; init; } = 540;

	// the sum of all the turns included in the timeScale array
	// e.x. in a standard game of 540 turns, the totalTurns amounts to 440 turns
	// after that we default to 1 timeUnit/per turn
	private int totalTurns = -1;

	[JsonIgnore]
	public int currentYear { get; private set; } = -1;
	[JsonIgnore]
	public int currentMonth { get; private set; } = -1;
	[JsonIgnore]
	public int currentWeek { get; private set; } = -1;
	[JsonIgnore]
	// custom
	public int currentDay { get; private set; } = -1;
	[JsonIgnore]
	// custom
	public int currentHour { get; private set; } = -1;

	// used to cache the turn number to display text value
	private Dictionary<int, string> DisplayTime = [];


	/// <summary>
	/// Get the corresponding NON-normalized time unit number based on the turn number,
	/// taking into account the various time intervals from the timeSpan array.<br/><br/>
	/// In a standard game, for years as a base time unit, turn 0 will return -4000, turn 1 will return -3950, etc<br/><br/>
	/// For months as a base time unit (and 1 unit intervals) turn 2 will be month 3, turn 14 will be month 15, etc<br/><br/>
	/// For weeks as a base time unit (and 1 unit intervals) turn 2 will be week 3, turn 54 will be week 55, etc<br/>
	/// </summary>
	/// <param name="current"></param>
	/// <returns></returns>
	/// <exception cref="Exception"></exception>
	public int GetRawNumber(int current) {
		int i = 0;
		int acc = timeScale[0, i];
		var currentNormalized = current;
		while (acc <= GetTotalTurns()) {
			int extra = 0;
			if (current <= acc) {
				for (int j = 0; j < i; j++) {
					// sum all the previous turns in years/months/weeks
					extra += timeScale[0, j] * timeScale[1, j];
					// forces the currentNormalized to have a value that we can use
					// e.x. if the current turn is 26, we are on the "second" iteration of the timeScale array
					// so we remove 25 that is the previous iteration, and now currentNormalized is 1,
					// and we can calculate the timeUnit for this iteration (1*40 in a std game)
					// think of it as a modulo operation, but for uneven divisor numbers
					currentNormalized -= timeScale[0, j];
				}

				var value = GetStartingPoint() + (currentNormalized * timeScale[1, i]) + extra;
				SetTimeUnitCurrent(value);
				return value;
			}

			++i;
			acc += timeScale[0, i];
		}

		throw new Exception($"The current turn {current} is beyond our powers");
	}

	/// <summary>
	/// Get the turn number from the current unit time.<br/>
	/// So, assuming a std game<br/>
	/// -3950 will return 1, etc<br/>
	/// month 5, Year 1, will return 4, month 3 year 2 will return 14, etc<br/>
	/// week 3 will return 3, and week 53 will  return 1, etc<br/>
	/// </summary>
	/// <param name="time"></param>
	/// <returns></returns>
	/// <exception cref="Exception"></exception>
	public int GetTurnFromRaw(int time) {
		int current = GetStartingPoint();
		int turn = 0;
		var extra = 0;
		for (int i = 0; i < timeScale.GetLength(1); i++) {
			var j = 1;
			if (time == current) return turn;
			while (current + extra < time && j <= timeScale[0, i]) {
				extra += timeScale[1, i];
				++turn;
				if (current + extra == time)
					return turn;
				++j;
			}
		}

		throw new Exception($"The current time {time} is unknown to us");
	}

	/// <summary>
	/// The total number of accumulated turns included in the game data
	/// </summary>
	/// <returns></returns>
	private int GetTotalTurns() {
		if (totalTurns != -1) return totalTurns;
		totalTurns = 0;
		var len = timeScale.GetLength(1);
		for (int i = 0; i < len; i++) {
			totalTurns += timeScale[0, i];
		}
		return totalTurns;
	}

	public string GetDisplayTime(int current) {
		if (DisplayTime.TryGetValue(current, out var value)) return value;
		var time = GetRawNumber(current);
		var displayText = GetDisplayTimeFromRaw(time);
		DisplayTime[current] = displayText;
		return displayText;
	}

	public string GetDisplayTimeFromRaw(int time) {
		string label = "PLACEHOLDER_TIME";
		var displayTimeLabelFunction = EngineStorage.gameData.luaBehaviorEngine.ImportFunc<Func<int, string>>("gameplay.time.display_text");
		label = displayTimeLabelFunction.Invoke(time);
		return label;
	}

	private int GetStartingPoint() {
		return baseUnit switch {
			TimeUnit.Years => currentYear = startYear,
			TimeUnit.Months => currentMonth = startMonth,
			TimeUnit.Weeks => currentWeek = startWeek,
			TimeUnit.Days => currentDay = startDay,
			TimeUnit.Hours => currentHour = startHour,
			_ => throw new InvalidEnumArgumentException($"{baseUnit} is not a valid TimeUnit")
		};
	}

	private void SetTimeUnitCurrent(int value) {
		switch (baseUnit) {
			case TimeUnit.Years:
				currentYear = value;
				break;
			case TimeUnit.Months:
				currentMonth = value;
				break;
			case TimeUnit.Weeks:
				currentWeek = value;
				break;
			case TimeUnit.Days:
				currentDay = value;
				break;
			case TimeUnit.Hours:
				currentHour = value;
				break;
			default:
				throw new InvalidEnumArgumentException($"{baseUnit} is not a valid TimeUnit");
		}
	}

	[LuaMethod]
	public void SetTimeUnitCurrent(TimeUnit timeUnit, int value) {
		Log.Information($"Setting unit {timeUnit} to {value}");
		switch (timeUnit) {
			case TimeUnit.Years:
				currentYear = value;
				break;
			case TimeUnit.Months:
				currentMonth = value;
				break;
			case TimeUnit.Weeks:
				currentWeek = value;
				break;
			case TimeUnit.Days:
				currentDay = value;
				break;
			case TimeUnit.Hours:
				currentHour = value;
				break;
			default:
				throw new InvalidEnumArgumentException($"{baseUnit} is not a valid TimeUnit");
		}
	}

	[LuaMethod]
	public string GetAbbrMonthNameByIndex(int index) {
		return CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(index);
	}
	[LuaMethod]
	public string GetMonthNameByIndex(int index) {
		return CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(index);
	}
}
