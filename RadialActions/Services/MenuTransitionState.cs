namespace RadialActions;

internal enum MenuPhase
{
    Hidden,
    Opening,
    Open,
    Closing,
}

/// <summary>
/// What <see cref="MenuService"/> must do to open the menu.
/// </summary>
internal enum MenuOpenStep
{
    /// <summary>
    /// The menu is already open or opening.
    /// </summary>
    None,

    /// <summary>
    /// Show the hidden window, then start the entrance once its first frame is on screen.
    /// </summary>
    ShowThenEnter,

    /// <summary>
    /// The window is visible mid-exit but its first frame may not be on screen yet, so wait for it before the entrance.
    /// </summary>
    EnterWhenSurfaceReady,

    /// <summary>
    /// Reverse the exit right away, continuing from the current opacity and scale.
    /// </summary>
    EnterNow,
}

/// <summary>
/// What <see cref="MenuService"/> must do to close the menu.
/// </summary>
internal enum MenuCloseStep
{
    /// <summary>
    /// The exit animation is already running.
    /// </summary>
    None,

    /// <summary>
    /// Start the exit animation; the window hides when it completes.
    /// </summary>
    Animate,

    /// <summary>
    /// Hide the window now.
    /// </summary>
    HideNow,
}

/// <summary>
/// Tracks the menu window through its entrance and exit so input during a transition reverses it instead of being dropped.
/// </summary>
internal sealed class MenuTransitionState
{
    private bool _hasEntranceStarted;

    public MenuPhase Phase { get; private set; } = MenuPhase.Hidden;

    /// <summary>
    /// True while the menu is on screen or on its way in; false while hidden or fading out.
    /// </summary>
    public bool IsOpen => Phase is MenuPhase.Opening or MenuPhase.Open;

    /// <summary>
    /// Changes whenever an entrance waiting for the window's first frame must be abandoned.
    /// </summary>
    public int EntranceRequest { get; private set; }

    public MenuOpenStep Open()
    {
        switch (Phase)
        {
            case MenuPhase.Hidden:
                Phase = MenuPhase.Opening;
                EntranceRequest++;
                _hasEntranceStarted = false;
                return MenuOpenStep.ShowThenEnter;

            case MenuPhase.Closing:
                Phase = MenuPhase.Opening;
                EntranceRequest++;

                // Once an entrance has started, the layered window has been submitting a frame on every tick, so it's already hittable.
                return _hasEntranceStarted ? MenuOpenStep.EnterNow : MenuOpenStep.EnterWhenSurfaceReady;

            default:
                return MenuOpenStep.None;
        }
    }

    /// <summary>
    /// True when the entrance for <paramref name="request"/> should still start once the window's first frame is on screen.
    /// </summary>
    public bool IsEntranceWanted(int request) => Phase == MenuPhase.Opening && request == EntranceRequest;

    public void MarkEntranceStarted()
    {
        _hasEntranceStarted = true;
    }

    /// <summary>
    /// Records that the entrance finished.
    /// </summary>
    /// <returns>False when the completion is stale because the menu has started closing since.</returns>
    public bool CompleteEntrance()
    {
        if (Phase != MenuPhase.Opening)
        {
            return false;
        }

        Phase = MenuPhase.Open;
        return true;
    }

    /// <param name="animate">False to hide right away, for example when the user turned animations off.</param>
    public MenuCloseStep Close(bool animate)
    {
        if (Phase == MenuPhase.Closing && animate)
        {
            return MenuCloseStep.None;
        }

        // Abandons an entrance still waiting for the window's first frame.
        EntranceRequest++;

        // Hidden here means the window is visible before the first open (at startup), so there's nothing to animate.
        if (!animate || Phase == MenuPhase.Hidden)
        {
            Phase = MenuPhase.Hidden;
            return MenuCloseStep.HideNow;
        }

        Phase = MenuPhase.Closing;
        return MenuCloseStep.Animate;
    }

    /// <summary>
    /// Records that the exit animation finished.
    /// </summary>
    /// <returns>True when the window should hide now; false when the menu was reopened during the exit.</returns>
    public bool CompleteExit()
    {
        if (Phase != MenuPhase.Closing)
        {
            return false;
        }

        Phase = MenuPhase.Hidden;
        return true;
    }
}
