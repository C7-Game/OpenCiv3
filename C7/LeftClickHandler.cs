using C7Engine.GameAction;
using Godot;
using System;
using System.Collections.Generic;

namespace C7;

/// A class for performing actions based on a sequence of mouse inputs, beginning with a left button input.
///
/// See MouseAction.cs for the sequences that are handled.
///
/// The class is set up by registering handlers to perform events, e.g. handler.On(MouseAction.Event.SomeEvent, HandleSomeEvent)
/// and filters to determine if an event is allowed, e.g. handler.Can(MouseAction.Event.SomeEvent, CanDoSomeEvent)
///
/// HandleLeftMouseButton is called when a left button input is received.
///
/// NotfiyMouseMotion is called when a mouse motion input is received, the motion is handled elsewhere in the code.
///
/// NotifyProcessFrame is called when a process frame event occurs. This is used as a timer to measure elapesd time.
/// These events occur about 60 times a second, which provides enough accuracy for handling mouse actions.

public class LeftClickHandler {

	public LeftClickHandler() {
		mouse = new MouseAction(mouseEvent => Filter(mouseEvent, lastButtonEvent));
	}

	public LeftClickHandler On(MouseAction.Event mouseEvent, Action<InputEventMouseButton> handler) {
		handlers[mouseEvent] = handler;
		return this;
	}

	public LeftClickHandler Can(MouseAction.Event mouseEvent, Func<InputEventMouseButton, bool> filter) {
		filters[mouseEvent] = filter;
		return this;
	}

	public void HandleLeftMouseButton(InputEventMouseButton input) {
		lastButtonEvent = input;
		Dispatch(mouse.LeftButtonEvent(input.IsPressed()), input);
	}

	public void NotifyMouseMotion(InputEventMouseMotion input) {
		Dispatch(mouse.MotionEvent(), lastButtonEvent);
	}

	public void NotifyProcessFrame(double delta) {
		Dispatch(mouse.TimeElapsedEvent(delta), lastButtonEvent);
	}

	void Dispatch(MouseAction.Event handlerEvent, InputEventMouseButton input) {
		if (handlers.TryGetValue(handlerEvent, out Action<InputEventMouseButton> handler)) {
			if (Filter(handlerEvent, input)) {
				handler(input);
			}
		}
	}

	bool Filter(MouseAction.Event filterEvent, InputEventMouseButton input) {
		if (filters.TryGetValue(filterEvent, out Func<InputEventMouseButton, bool> filter)) {
			return filter(input);
		}
		return true;
	}

	InputEventMouseButton lastButtonEvent;

	readonly MouseAction mouse;
	readonly Dictionary<MouseAction.Event, Action<InputEventMouseButton>> handlers = [];
	readonly Dictionary<MouseAction.Event, Func<InputEventMouseButton, bool>> filters = [];
}
