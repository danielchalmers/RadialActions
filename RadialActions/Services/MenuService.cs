using System.Windows;
using System.Windows.Input;
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
        BeginFadeIn();
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
        _isFadingOut = false;
        StopFadeAnimations();
        _window.IsHitTestVisible = false;
        _window.Opacity = 0;
        _window.Hide();
    }
}
