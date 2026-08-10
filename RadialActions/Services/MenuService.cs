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
    private readonly Storyboard _fadeInStoryboard;
    private readonly Storyboard _fadeOutStoryboard;
    private readonly Dispatcher _dispatcher;

    private bool _isFadingOut;
    private int _fadeInRequestVersion;

    public MenuService(
        Window window,
        PieControl pieMenu,
        Storyboard fadeInStoryboard,
        Storyboard fadeOutStoryboard)
    {
        _window = window;
        _pieMenu = pieMenu;
        _fadeInStoryboard = fadeInStoryboard;
        _fadeOutStoryboard = fadeOutStoryboard;
        _dispatcher = window.Dispatcher;
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

        if (!_window.IsVisible)
        {
            _window.Opacity = 0;
            _window.Show();
        }

        _window.Activate();
        _ = FocusMenuForKeyboardInputAsync();
        _pieMenu.ResetInputState();
        _window.IsHitTestVisible = true;
        BeginFadeInWhenSurfaceReady();
    }

    /// <summary>
    /// Starts the fade-in only after the layered window has rendered and submitted a frame.
    /// </summary>
    /// <remarks>
    /// The OS hit-tests a layered window against its last submitted surface, and the first submission after
    /// <see cref="Window.Show"/> lags by several frames. Starting the animation immediately made the fade run
    /// against a still-invisible, click-through window: the menu appeared mid-animation at high opacity, and
    /// clicks in that gap fell through to the window beneath, which dismissed the menu via deactivation.
    /// Waiting two composition ticks (one to render the shown surface, one so it is submitted) keeps the menu
    /// hittable from the first visible pixel and lets the full fade actually be seen.
    /// </remarks>
    private void BeginFadeInWhenSurfaceReady()
    {
        var version = ++_fadeInRequestVersion;
        var renderedTicks = 0;

        void OnRendering(object sender, EventArgs e)
        {
            if (version != _fadeInRequestVersion)
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
            BeginFadeIn();
        }

        CompositionTarget.Rendering += OnRendering;
    }

    public void HideMenu(bool animate = true)
    {
        if (!_window.IsVisible || _isFadingOut)
        {
            return;
        }

        Log.Information("Dismissing menu");

        if (!animate || IsReducedMotionEnabled())
        {
            HideMenuImmediately();
            return;
        }

        BeginFadeOut();
    }

    public void OnFadeOutCompleted()
    {
        if (!_isFadingOut)
        {
            return;
        }

        HideMenuImmediately();
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

    private void BeginFadeIn()
    {
        StopFadeAnimations();

        if (IsReducedMotionEnabled())
        {
            _isFadingOut = false;
            _window.Opacity = 1;
            return;
        }

        _isFadingOut = false;
        _fadeInStoryboard.Begin(_window, HandoffBehavior.SnapshotAndReplace, true);
    }

    private void BeginFadeOut()
    {
        // Cancels any fade-in still waiting on its first rendered frame.
        _fadeInRequestVersion++;
        StopFadeAnimations();

        if (IsReducedMotionEnabled())
        {
            HideMenuImmediately();
            return;
        }

        _isFadingOut = true;
        _window.IsHitTestVisible = false;
        _fadeOutStoryboard.Begin(_window, HandoffBehavior.SnapshotAndReplace, true);
    }

    private void StopFadeAnimations()
    {
        _fadeInStoryboard.Remove(_window);
        _fadeOutStoryboard.Remove(_window);
    }

    private static bool IsReducedMotionEnabled()
    {
        return !SystemParameters.ClientAreaAnimation;
    }

    private void HideMenuImmediately()
    {
        // Cancels any fade-in still waiting on its first rendered frame.
        _fadeInRequestVersion++;
        _isFadingOut = false;
        StopFadeAnimations();
        _window.IsHitTestVisible = false;
        _window.Opacity = 0;
        _window.Hide();
    }
}
