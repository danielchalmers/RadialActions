namespace RadialActions.Tests;

public class MenuTransitionStateTests
{
    [Fact]
    public void Open_WhenHidden_ShowsThenEnters()
    {
        var state = new MenuTransitionState();

        var step = state.Open();

        Assert.Equal(MenuOpenStep.ShowThenEnter, step);
        Assert.Equal(MenuPhase.Opening, state.Phase);
        Assert.True(state.IsOpen);
    }

    [Fact]
    public void Open_WhileOpeningOrOpen_DoesNothing()
    {
        var state = new MenuTransitionState();
        state.Open();
        var request = state.EntranceRequest;

        Assert.Equal(MenuOpenStep.None, state.Open());
        Assert.True(state.IsEntranceWanted(request));

        state.MarkEntranceStarted();
        state.CompleteEntrance();

        Assert.Equal(MenuOpenStep.None, state.Open());
        Assert.Equal(MenuPhase.Open, state.Phase);
    }

    [Fact]
    public void CompleteEntrance_MovesOpeningToOpen()
    {
        var state = new MenuTransitionState();
        state.Open();
        state.MarkEntranceStarted();

        Assert.True(state.CompleteEntrance());
        Assert.Equal(MenuPhase.Open, state.Phase);
        Assert.True(state.IsOpen);
    }

    [Fact]
    public void Close_Animated_StartsExitAndIsNoLongerOpen()
    {
        var state = OpenState();

        var step = state.Close(animate: true);

        Assert.Equal(MenuCloseStep.Animate, step);
        Assert.Equal(MenuPhase.Closing, state.Phase);
        Assert.False(state.IsOpen);
    }

    [Fact]
    public void Close_WhileClosing_IsIgnored()
    {
        var state = OpenState();
        state.Close(animate: true);

        Assert.Equal(MenuCloseStep.None, state.Close(animate: true));
        Assert.Equal(MenuPhase.Closing, state.Phase);
    }

    [Fact]
    public void Close_WithoutAnimation_HidesNowEvenMidExit()
    {
        var state = OpenState();
        state.Close(animate: true);

        Assert.Equal(MenuCloseStep.HideNow, state.Close(animate: false));
        Assert.Equal(MenuPhase.Hidden, state.Phase);
    }

    [Fact]
    public void Close_WhenHidden_HidesNow()
    {
        // At startup the window is visible before the menu has ever opened.
        var state = new MenuTransitionState();

        Assert.Equal(MenuCloseStep.HideNow, state.Close(animate: true));
        Assert.Equal(MenuPhase.Hidden, state.Phase);
    }

    [Fact]
    public void CompleteExit_HidesAfterAnExit()
    {
        var state = OpenState();
        state.Close(animate: true);

        Assert.True(state.CompleteExit());
        Assert.Equal(MenuPhase.Hidden, state.Phase);
    }

    [Fact]
    public void Open_DuringExit_ReversesRightAway()
    {
        var state = OpenState();
        state.Close(animate: true);

        var step = state.Open();

        Assert.Equal(MenuOpenStep.EnterNow, step);
        Assert.Equal(MenuPhase.Opening, state.Phase);
        Assert.True(state.IsOpen);
    }

    [Fact]
    public void CompleteExit_AfterReopening_DoesNotHideTheMenu()
    {
        var state = OpenState();
        state.Close(animate: true);
        state.Open();

        Assert.False(state.CompleteExit());
        Assert.Equal(MenuPhase.Opening, state.Phase);
    }

    [Fact]
    public void CompleteEntrance_AfterClosing_IsStale()
    {
        var state = new MenuTransitionState();
        state.Open();
        state.MarkEntranceStarted();
        state.Close(animate: true);

        Assert.False(state.CompleteEntrance());
        Assert.Equal(MenuPhase.Closing, state.Phase);
    }

    [Fact]
    public void Close_BeforeTheEntranceStarts_AbandonsThePendingEntrance()
    {
        var state = new MenuTransitionState();
        state.Open();
        var request = state.EntranceRequest;

        state.Close(animate: true);

        Assert.False(state.IsEntranceWanted(request));
    }

    [Fact]
    public void Open_DuringAnExitThatInterruptedAPendingEntrance_WaitsForTheSurface()
    {
        var state = new MenuTransitionState();
        state.Open();
        state.Close(animate: true);

        var step = state.Open();

        Assert.Equal(MenuOpenStep.EnterWhenSurfaceReady, step);
        Assert.True(state.IsEntranceWanted(state.EntranceRequest));
    }

    [Fact]
    public void Open_AfterHiding_StartsFreshAndWaitsForTheSurfaceAgain()
    {
        var state = OpenState();
        state.Close(animate: true);
        state.CompleteExit();

        Assert.Equal(MenuOpenStep.ShowThenEnter, state.Open());

        state.Close(animate: true);

        // The new show hasn't started its entrance, so a reopen must wait for the first frame again.
        Assert.Equal(MenuOpenStep.EnterWhenSurfaceReady, state.Open());
    }

    [Fact]
    public void RepeatedToggling_EndsInTheLastRequestedState()
    {
        var state = OpenState();

        state.Close(animate: true);
        state.Open();
        state.MarkEntranceStarted();
        state.Close(animate: true);
        state.Open();
        state.MarkEntranceStarted();

        Assert.False(state.CompleteExit());
        Assert.True(state.CompleteEntrance());
        Assert.Equal(MenuPhase.Open, state.Phase);
    }

    private static MenuTransitionState OpenState()
    {
        var state = new MenuTransitionState();
        state.Open();
        state.MarkEntranceStarted();
        state.CompleteEntrance();
        return state;
    }
}
