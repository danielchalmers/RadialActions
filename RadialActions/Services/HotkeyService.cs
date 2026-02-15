using H.NotifyIcon;

namespace RadialActions;

internal sealed class HotkeyService : IDisposable
{
    private HotkeyManager _hotkeys;
    private EventHandler _hotkeyHandler;
    private bool _disposed;

    public void Initialize(IntPtr handle, EventHandler hotkeyHandler)
    {
        ArgumentNullException.ThrowIfNull(hotkeyHandler);
        Log.Debug("Initializing hotkey service for handle {Handle}", handle);
        DisposeHotkeys();

        _hotkeyHandler = hotkeyHandler;
        _hotkeys = new HotkeyManager(handle);
        _hotkeys.HotkeyPressed += _hotkeyHandler;
    }

    public void ApplyHotkey(string hotkey)
    {
        if (_hotkeys is null)
        {
            Log.Debug("Skipping hotkey apply because service is not initialized");
            return;
        }

        _hotkeys.UnregisterAll();
        if (!string.IsNullOrWhiteSpace(hotkey))
        {
            Log.Debug("Applying configured hotkey: {Hotkey}", hotkey);
            _hotkeys.RegisterHotkey(hotkey);
            return;
        }

        Log.Information("No activation hotkey configured; hotkeys are cleared");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DisposeHotkeys();
        GC.SuppressFinalize(this);
    }

    private void DisposeHotkeys()
    {
        if (_hotkeys is null)
        {
            return;
        }

        Log.Debug("Disposing hotkey registrations");

        if (_hotkeyHandler is not null)
        {
            _hotkeys.HotkeyPressed -= _hotkeyHandler;
        }

        _hotkeys.Dispose();
        _hotkeys = null;
    }
}
