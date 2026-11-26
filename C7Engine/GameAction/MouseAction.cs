using System;

namespace C7Engine.GameAction;

/// A class for determining the next action based on mouse inputs.
///
/// There are four possible sequences starting with a left mouse press:
///
/// - Long Press: the button is held down for a time interval (LONG_PRESS_DELAY).
/// - Drag: the button is held down and the mouse is moved.
/// - Double Click: the button is pressed and released, and before an interval elapses
///    (DOUBLE_CLICK_DELAY), the button is pressed and released again.
/// - Left Click: the button is pressed and released, and none of the previous sequences apply.
///
/// Note: Godot provides built-in double click detection, but it is based on the standard
/// sequence of a Left Click followed bya Double Click.  Civ 3 does not treat double clicks this way.
/// For example, with Godot’s built-in detection, clicking on a city that contains a
/// unit would both zoom to the city and select the unit.
///
/// Long Press and Double Click may be disabled in some game states. The class constructor
/// contains a callback function that methods can use to see if it can do these events.
///
/// Each of the class methods updates the current state and returns an event that has occured,
///  or Event.None if the determination of the next event is still pending.

public class MouseAction(Func<MouseAction.Event, bool> canDoFunction) {

	public enum Event {
		None,
		LeftClick,
		DoubleClick,
		LongPress,
		Drag
	}

	public Event LeftButtonEvent(bool isPressed) {
		var action = Event.None;
		if (isPressed) {
			pressedElapsed = 0.0;
		} else {
			if (canDo(Event.DoubleClick)) {
				if (doubleClickPending) {
					doubleClickPending = false;
					action = Event.DoubleClick;
				} else {
					clickElapsed = 0.0;
					doubleClickPending = true;
				}
			} else {
				action = Event.LeftClick;
			}
		}
		if (isPressed) {
			longPressPending = canDo(Event.LongPress);
		} else {
			longPressPending = false;
		}
		return action;
	}

	public Event MotionEvent() {
		if (longPressPending) {
			longPressPending = false;
			return Event.Drag;
		} else
			return Event.None;
	}

	public Event TimeElapsedEvent(double delta) {
		if (longPressPending) {
			pressedElapsed += delta;
			if (pressedElapsed > LONG_PRESS_DELAY) {
				longPressPending = false;
				return Event.LongPress;
			}
		}
		if (doubleClickPending) {
			clickElapsed += delta;
			if (clickElapsed > DOUBLE_CLICK_DELAY) {
				doubleClickPending = false;
				return Event.LeftClick;
			}
		}
		return Event.None;
	}

	const double LONG_PRESS_DELAY = 1.0;
	const double DOUBLE_CLICK_DELAY = 0.3;

	readonly Func<Event, bool> canDo = canDoFunction;
	bool longPressPending = false;
	double pressedElapsed = 0.0;
	bool doubleClickPending = false;
	double clickElapsed = 0.0;
}
