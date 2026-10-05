using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace RadialActions;

internal sealed class MenuService
{
    private readonly Window _window;
    private readonly PieControl _pieMenu;
    private readonly Storyboard _enterStoryboard;
    private readonly Storyboard _exitStoryboard;
    private readonly double _enterStartScale;
    private readonly Dispatcher _dispatcher;
    private readonly MenuTransitionState _state = new();

    /// <param name="pieMenu">The pie, whose ScaleTransform render transform the storyboards animate.</param>
    /// <param name="enterStartScale">The scale the entrance grows from (MotionMenuEnterScale).</param>
    public MenuService(
        Window window,
        PieControl pieMenu,
        Storyboard enterStoryboard,
        Storyboard exitStoryboard,
        double enterStartScale)
    {
        _window = window;
        _pieMenu = pieMenu;
        _enterStoryboard = enterStoryboard;
        _exitStoryboard = exitStoryboard;
        _enterStartScale = enterStartScale;
        _dispatcher = window.Dispatcher;
    }

    /// <summary>
    /// True while the menu is on screen or on its way in; false while hidden or fading out.
    /// </summary>
    public bool IsOpen => _state.IsOpen;

    /// <summary>
    /// True when at least one action would appear in the menu.
    /// </summary>
    public static bool HasEnabledActions(IEnumerable<PieAction> actions)
    {
        return actions?.Any(action => action?.IsEnabled == true) == true;
    }

    public void ShowMenu(bool atCursor)
    {
        if (atCursor)
        {
            Log.Information("Opening at the cursor");
            _window.CenterOnCursor();
        }
        else
        {
            Log.Information("Opening at the center of the screen");
            _window.CenterOnScreen();
        }

        var step = _state.Open();
        if (step == MenuOpenStep.ShowThenEnter)
        {
            _window.Opacity = 0;
            SetPieScale(_enterStartScale);

            if (!_window.IsVisible)
            {
                _window.Show();
            }
        }

        _window.Activate();
        _ = FocusMenuForKeyboardInputAsync();
        _pieMenu.ResetInputState();
        _window.IsHitTestVisible = true;

        switch (step)
        {
            case MenuOpenStep.ShowThenEnter:
            case MenuOpenStep.EnterWhenSurfaceReady:
                BeginEnterWhenSurfaceReady();
                break;
            case MenuOpenStep.EnterNow:
                Log.Debug("Reopening during the exit animation");
                BeginEnter();
                break;
        }
    }

    /// <summary>
    /// Starts the entrance only after the layered window has rendered and submitted a frame.
    /// </summary>
    /// <remarks>
    /// The OS hit-tests a layered window against its last submitted surface, and the first submission after
    /// <see cref="Window.Show"/> lags by several frames. Starting the animation immediately made the fade run
    /// against a still-invisible, click-through window: the menu appeared mid-animation at high opacity, and
    /// clicks in that gap fell through to the window beneath, which dismissed the menu via deactivation.
    /// Waiting two composition ticks (one to render the shown surface, one so it is submitted) keeps the menu
    /// hittable from the first visible pixel and lets the full fade actually be seen.
    /// </remarks>
    private void BeginEnterWhenSurfaceReady()
    {
        var request = _state.EntranceRequest;
        var renderedTicks = 0;

        void OnRendering(object sender, EventArgs e)
        {
            if (!_state.IsEntranceWanted(request))
            {
                CompositionTarget.Rendering -= OnRendering;
                return;
            }

            renderedTicks++;
            if (renderedTicks < 2)
            {
                return;
            }

            CompositionTarget.Rendering -= OnRendering;
            BeginEnter();
        }

        CompositionTarget.Rendering += OnRendering;
    }

    public void HideMenu(bool animate = true)
    {
        if (!_window.IsVisible)
        {
            return;
        }

        var step = _state.Close(animate && !PieAnimationService.IsReducedMotionEnabled());
        if (step == MenuCloseStep.None)
        {
            return;
        }

        Log.Information("Dismissing menu");

        // Turning hit testing off sends MouseLeave to the slice under the pointer, so the pie freezes its visual state first and the slice that fired stays highlighted through the exit.
        _pieMenu.BeginDismiss();
        _window.IsHitTestVisible = false;

        if (step == MenuCloseStep.HideNow)
        {
            HideWindow();
            return;
        }

        StopAnimations();
        _exitStoryboard.Begin(_window, HandoffBehavior.SnapshotAndReplace, true);
    }

    public void OnEnterCompleted()
    {
        if (_state.Phase == MenuPhase.Opening)
        {
            SettleOpen();
        }
    }

    public void OnExitCompleted()
    {
        if (_state.CompleteExit())
        {
            HideWindow();
        }
    }

    private async Task FocusMenuForKeyboardInputAsync()
    {
        if (!_window.IsVisible)
        {
            return;
        }

        _window.Focus();
        Keyboard.Focus(_pieMenu);

        await _dispatcher.InvokeAsync(() =>
        {
            if (!_window.IsVisible)
            {
                return;
            }

            _window.Activate();
            _window.Focus();
            Keyboard.Focus(_pieMenu);
        }, DispatcherPriority.Input);
    }

    private void BeginEnter()
    {
        _state.MarkEntranceStarted();
        StopAnimations();

        if (PieAnimationService.IsReducedMotionEnabled())
        {
            SettleOpen();
            return;
        }

        // SnapshotAndReplace continues from the current values, so a reopen during the exit reverses it smoothly.
        _enterStoryboard.Begin(_window, HandoffBehavior.SnapshotAndReplace, true);
    }

    private void SettleOpen()
    {
        _state.CompleteEntrance();

        // Hands the final values to the properties and drops the animation so the pie renders at exactly scale 1, with crisp snapped geometry.
        _window.Opacity = 1;
        SetPieScale(1);
        StopAnimations();
    }

    private void HideWindow()
    {
        _window.Opacity = 0;
        SetPieScale(1);
        StopAnimations();
        _window.Hide();
    }

    private void StopAnimations()
    {
        _enterStoryboard.Remove(_window);
        _exitStoryboard.Remove(_window);
    }

    private void SetPieScale(double scale)
    {
        if (_pieMenu.RenderTransform is ScaleTransform pieScale)
        {
            pieScale.ScaleX = scale;
            pieScale.ScaleY = scale;
        }
    }
}
