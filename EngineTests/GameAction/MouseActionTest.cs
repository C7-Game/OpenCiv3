using C7Engine.GameAction;
using System;
using Xunit;

namespace EngineTests.GameAction;

public class MouseActionTest {

	[Fact]
	public void PressForLongIntervalIsOneLongPressAction() {
		var mouse = new MouseAction(CanDo(MouseAction.Event.LongPress, true));
		Assert.Equal(MouseAction.Event.None, mouse.LeftButtonEvent(true));
		Assert.Equal(MouseAction.Event.None, mouse.TimeElapsedEvent(0.5));
		Assert.Equal(MouseAction.Event.LongPress, mouse.TimeElapsedEvent(0.51));
		Assert.Equal(MouseAction.Event.None, mouse.TimeElapsedEvent(1.01));
	}

	[Fact]
	public void CannotLongPressIsNoLongPressAction() {
		var mouse = new MouseAction(CanDo(MouseAction.Event.LongPress, false));
		Assert.Equal(MouseAction.Event.None, mouse.LeftButtonEvent(true));
		Assert.Equal(MouseAction.Event.None, mouse.TimeElapsedEvent(0.5));
		Assert.Equal(MouseAction.Event.None, mouse.TimeElapsedEvent(0.51));
	}

	[Fact]
	public void ReleaseBeforeLongPressIntervalIsLeftClick() {
		var mouse = new MouseAction(CanDo(MouseAction.Event.LongPress, true));
		Assert.Equal(MouseAction.Event.None, mouse.LeftButtonEvent(true));
		Assert.Equal(MouseAction.Event.None, mouse.TimeElapsedEvent(0.5));
		Assert.Equal(MouseAction.Event.LeftClick, mouse.LeftButtonEvent(false));
		Assert.Equal(MouseAction.Event.None, mouse.TimeElapsedEvent(0.51));
	}

	[Fact]
	public void MotionBeforeLongPressIntervalIsDragAction() {
		var mouse = new MouseAction(CanDo(MouseAction.Event.LongPress, true));
		Assert.Equal(MouseAction.Event.None, mouse.LeftButtonEvent(true));
		Assert.Equal(MouseAction.Event.None, mouse.TimeElapsedEvent(0.5));
		Assert.Equal(MouseAction.Event.Drag, mouse.MotionEvent());
		Assert.Equal(MouseAction.Event.None, mouse.TimeElapsedEvent(0.51));
	}

	[Fact]
	public void LeftClickPerformsLeftClickAction() {
		var mouse = new MouseAction(CanDo(MouseAction.Event.DoubleClick, false));
		Assert.Equal(MouseAction.Event.None, mouse.LeftButtonEvent(true));
		Assert.Equal(MouseAction.Event.LeftClick, mouse.LeftButtonEvent(false));
	}

	[Fact]
	public void IntervalBetweenLeftClicksIsNotDoubleClickAction() {
		var mouse = new MouseAction(CanDo(MouseAction.Event.DoubleClick, true));
		Assert.Equal(MouseAction.Event.None, mouse.LeftButtonEvent(false));
		Assert.Equal(MouseAction.Event.None, mouse.TimeElapsedEvent(0.15));
		Assert.Equal(MouseAction.Event.LeftClick, mouse.TimeElapsedEvent(0.151));
		Assert.Equal(MouseAction.Event.None, mouse.LeftButtonEvent(false));
		Assert.Equal(MouseAction.Event.LeftClick, mouse.TimeElapsedEvent(0.301));
	}

	[Fact]
	public void TwoLeftClicksPerformDoubleClickAction() {
		var mouse = new MouseAction(CanDo(MouseAction.Event.DoubleClick, true));
		Assert.Equal(MouseAction.Event.None, mouse.LeftButtonEvent(false));
		Assert.Equal(MouseAction.Event.DoubleClick, mouse.LeftButtonEvent(false));
	}

	[Fact]
	public void TwoLeftClicksWhenCannotDoDoubleClickPerformLeftClickActions() {
		var mouse = new MouseAction(CanDo(MouseAction.Event.DoubleClick, false));
		Assert.Equal(MouseAction.Event.LeftClick, mouse.LeftButtonEvent(false));
		Assert.Equal(MouseAction.Event.LeftClick, mouse.LeftButtonEvent(false));
	}

	static Func<MouseAction.Event, bool> CanDo(MouseAction.Event eventType, bool canDo) {
		return type => { return type == eventType && canDo; };
	}
}
