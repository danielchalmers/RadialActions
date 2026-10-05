using System.Runtime.InteropServices;

namespace RadialActions;

/// <summary>
/// Keeps one copy of the app per sign-in session. A later launch asks the first copy to show itself instead of adding a second tray icon that can't register the hotkey.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const int ASFW_ANY = -1;

    private readonly EventWaitHandle _showRequested;
    private RegisteredWaitHandle _showRequestRegistration;

    /// <param name="name">A session-wide name such as Local\RadialActions, shared by every copy wherever it's installed.</param>
    public SingleInstance(string name)
    {
        try
        {
            _showRequested = new EventWaitHandle(false, EventResetMode.AutoReset, name, out var createdNew);
            IsFirst = createdNew;
        }
        catch (UnauthorizedAccessException ex)
        {
            // A copy running as administrator created the event, so this copy can't open it or ask that copy to show itself.
            Log.Warning(ex, "Radial Actions is already running as administrator");
        }
    }

    /// <summary>
    /// True when no other copy is running, so this one should start.
    /// </summary>
    public bool IsFirst { get; }

    /// <summary>
    /// Asks the first copy to show itself, passing it the right to come to the foreground that this newly launched copy holds.
    /// </summary>
    public void RequestShow()
    {
        if (_showRequested == null)
            return;

        AllowSetForegroundWindow(ASFW_ANY);
        _showRequested.Set();
    }

    /// <summary>
    /// Calls <paramref name="onShowRequested"/> on a thread pool thread each time a later launch calls <see cref="RequestShow"/>.
    /// </summary>
    public void ListenForShowRequests(Action onShowRequested)
    {
        ArgumentNullException.ThrowIfNull(onShowRequested);

        _showRequestRegistration = ThreadPool.RegisterWaitForSingleObject(
            _showRequested,
            (_, _) => onShowRequested(),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _showRequestRegistration?.Unregister(null);
        _showRequested?.Dispose();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);
}
