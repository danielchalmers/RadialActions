using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace RadialActions;

/// <summary>
/// A radial pie menu control that displays clickable slices.
/// </summary>
public partial class PieControl : UserControl
{
    private const double DefaultCenterHoleRatio = 0.25;
    private const int SliceZIndex = 10;
    private const int HoveredSliceZIndex = 14;
    private const int SliceContentZIndex = 15;
    private const int DraggedSliceZIndex = 16;
    private const int DraggedSliceContentZIndex = 17;
    private const double MouseMoveThreshold = 0.25;

    // A pressed slice settles inward about the pie center; the hub scales about its own center.
    private const double SlicePressedScale = 0.97;
    private const double HubPressedScale = 0.95;

    // Gap between the selection arc's round caps and the slice separators.
    private const double SelectionArcSeparatorGap = 2;

    // Label fitting: clearance from the slice edges beyond the stroke, clearance from the rim hints, the widest a label may get relative to the radius, and the narrowest label worth showing before the slice falls back to its icon alone.
    private const double LabelEdgePadding = 2;
    private const double LabelHintGap = 2;
    private const double LabelMaxWidthRatio = 0.7;
    private const double MinLabelWidth = 32;

    private const string EditGlyph = "\uE70F";

    /// <summary>
    /// The number keys 1-9 trigger slices, so hints are only shown for the first nine.
    /// </summary>
    private const int MaxDigitHints = PieAccessibilityText.MaxAccessKeys;

    /// <summary>
    /// How far the pointer must travel with the button held before a press becomes a drag.
    /// Deliberately much larger than the system drag threshold so held-but-jittering clicks
    /// during normal use never start reordering.
    /// </summary>
    private const double DragStartThreshold = 20;

    private enum InteractionMode
    {
        Mouse,
        Keyboard,
    }

    private bool _renderRefreshPending;
    private DispatcherOperation _renderRefreshOperation;
    private DpiScale _buildDpi;
    private readonly List<PieSliceVisual> _sliceVisuals = [];
    private PieCenterVisual _centerVisual;
    private readonly PieAnimationService _animationService = new();
    private readonly PieSelectionController _selectionController = new();
    private readonly PieRenderState _renderState = new();
    private InteractionMode _interactionMode = InteractionMode.Mouse;
    private Point _keyboardModeMousePosition;
    private bool _hasKeyboardModeMousePosition;
    private Point _layoutCenter;
    private double _layoutAngleStep;
    private double _layoutInnerRadius;
    private double _layoutOuterRadius;
    private PieSliceVisual _dragCandidate;
    private Point _dragPressPosition;
    private DragReorderState _drag;
    private bool _isReleasingDragCapture;
    private bool _isReleaseTriggerArmed;
    private bool _isDismissing;
    private PieSliceVisual _releaseTriggeredSlice;

    private sealed class DragReorderState
    {
        public required PieSliceVisual Slice { get; init; }
        public required double LastPointerAngle { get; set; }
        public required double RotationOffset { get; set; }
        public required int TargetSlot { get; set; }
    }

    public PieControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
        SizeChanged += OnSizeChanged;
        PieCanvas.MouseMove += OnPieCanvasMouseMove;
    }

    internal IReadOnlyList<PieSliceVisual> SliceVisuals => _sliceVisuals;

    internal PieCenterVisual CenterVisual => _centerVisual;

    /// <summary>
    /// Assistive technology can invoke slices only while the menu is open and not already fading out.
    /// </summary>
    internal bool CanInvokeFromAutomation => IsVisible && !_isDismissing;

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new PieControlAutomationPeer(this);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RequestRenderRefresh();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);

        // Snapped geometry and the surface shadow's BitmapCache scale are baked at build-time DPI, and the menu's
        // size in DIPs doesn't change when it opens on a different-DPI monitor, so nothing else triggers a rebuild.
        RequestRenderRefresh();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        RequestRenderRefresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not bool isVisible)
        {
            return;
        }

        if (!isVisible)
        {
            // Changes while hidden (theme, slices, size) go through RequestRenderRefresh, which builds them at idle so the next open normally finds the pie ready.
            // A drag cancelled by the dismiss deferred its rebuild until now, so queue it to land at idle too instead of at the next open.
            if (_renderRefreshPending)
            {
                RequestRenderRefresh();
            }

            return;
        }

        if (_renderRefreshPending)
        {
            // An idle build is still outstanding (or produced nothing); promote it so it lands before the first frame.
            Log.Debug("PieControl visible with a pending render refresh; building before the first frame");
            RequestRenderRefresh();
        }
    }

    /// <summary>
    /// True while the activation hotkey that opened the menu is still held, so releasing it over a slice triggers it.
    /// The targeted slice shows a release hint while this is set.
    /// </summary>
    public bool IsReleaseTriggerArmed
    {
        get => _isReleaseTriggerArmed;
        set
        {
            if (_isReleaseTriggerArmed == value)
            {
                return;
            }

            _isReleaseTriggerArmed = value;
            RefreshVisualState(animate: true);
        }
    }

    /// <summary>
    /// Freezes the visual state for the dismiss fade so the slice that just fired stays highlighted while the menu fades out.
    /// Call when a dismiss starts (fade-out or immediate hide); <see cref="ResetInputState"/> clears it on the next open.
    /// </summary>
    public void BeginDismiss()
    {
        if (_isDismissing)
        {
            return;
        }

        _isDismissing = true;

        // Releasing the hotkey disarms the trigger, which starts fading the release hint, just before the targeted slice fires; restore it so the fired slice keeps its hint through the fade.
        if (_releaseTriggeredSlice != null)
        {
            SetReleaseHintVisible(_releaseTriggeredSlice, isVisible: true, animate: false);
        }
    }

    public void ResetInputState()
    {
        _isDismissing = false;
        _isReleaseTriggerArmed = false;

        // The visuals survive across opens, so press and drag tracking from the previous open must be discarded
        // here or a held button in the next open can resume a drag that was never started there.
        var wasDragging = _drag != null;
        _drag = null;
        _dragCandidate = null;

        // Reopening during the exit never hides the window, so nothing else rebuilds slices left displaced by a drag the dismiss cancelled (or is about to cancel), and the hints would sit over the wrong wedges.
        if ((wasDragging || _renderRefreshPending) && IsVisible)
        {
            RequestRenderRefresh();
        }

        // A press interrupted by the dismiss must not leave a slice or the hub shrunken when the menu opens again.
        foreach (var sliceVisual in _sliceVisuals)
        {
            sliceVisual.IsPressed = false;
            _animationService.ResetClickScale(sliceVisual.Path);
        }

        if (_centerVisual != null)
        {
            _centerVisual.IsPressed = false;
            _animationService.ResetClickScale(_centerVisual.Target);
        }

        EnterMouseInteractionMode(refreshVisualState: true, animate: false);
    }

    /// <summary>
    /// Triggers the active slice: the keyboard-selected slice in keyboard mode, otherwise the slice under the mouse.
    /// </summary>
    /// <returns>True if a slice was triggered.</returns>
    public bool TriggerActiveSlice()
    {
        if (_isDismissing)
        {
            return false;
        }

        var hoveredIndex = _sliceVisuals.FirstOrDefault(slice => slice.Path.IsMouseOver)?.Index ?? PieSelectionController.NoSelection;
        var targetIndex = PieSelectionController.GetReleaseTriggerIndex(
            _drag != null,
            _interactionMode == InteractionMode.Keyboard,
            _selectionController.SelectedIndex,
            hoveredIndex);

        var targetSlice = _sliceVisuals.FirstOrDefault(slice => slice.Index == targetIndex);
        if (targetSlice == null)
        {
            return false;
        }

        _releaseTriggeredSlice = targetSlice;
        try
        {
            RaiseSliceClicked(targetSlice);
        }
        finally
        {
            _releaseTriggeredSlice = null;
        }

        return true;
    }

    public bool HandleMenuKey(Key key, ModifierKeys modifiers)
    {
        switch (key)
        {
            case Key.Up:
            case Key.Down:
            case Key.Left:
            case Key.Right:
            case Key.Home:
            case Key.End:
            case Key.Tab when (modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == ModifierKeys.None:
                MoveKeyboardSelection(key, modifiers.HasFlag(ModifierKeys.Shift));
                return true;
            case Key.Return:
            case Key.Space:
                ActivateSelectedSlice();
                return true;
            case Key.Apps:
                return OpenSelectedSliceContextMenu();
            case Key.F10 when modifiers.HasFlag(ModifierKeys.Shift):
                return OpenSelectedSliceContextMenu();
            case >= Key.D1 and <= Key.D9:
                return HandleDigitKey(key - Key.D1 + 1);
            case >= Key.NumPad1 and <= Key.NumPad9:
                return HandleDigitKey(key - Key.NumPad1 + 1);
            default:
                return false;
        }
    }

    /// <summary>
    /// Moves the keyboard selection to <paramref name="sliceVisual"/> without running it, for assistive technology moving focus.
    /// </summary>
    internal void SelectSliceForAutomation(PieSliceVisual sliceVisual)
    {
        if (_isDismissing || !_sliceVisuals.Contains(sliceVisual) || !_selectionController.TrySelect(sliceVisual.Index, GetSelectionItems()))
        {
            return;
        }

        EnterKeyboardInteractionMode();
        RefreshVisualState(animate: true);
        Focus();
        (UIElementAutomationPeer.FromElement(this) as PieControlAutomationPeer)?.RaiseSliceFocusChanged(sliceVisual);
    }

    /// <summary>
    /// Runs <paramref name="sliceVisual"/> exactly like Enter on a keyboard selection. Only an explicit Invoke from assistive technology calls this.
    /// </summary>
    internal void InvokeSliceFromAutomation(PieSliceVisual sliceVisual)
    {
        if (!CanInvokeFromAutomation || !_sliceVisuals.Contains(sliceVisual) || !_selectionController.TrySelect(sliceVisual.Index, GetSelectionItems()))
        {
            return;
        }

        Log.Debug("Slice invoked through UI Automation: {SliceName}", sliceVisual.Action.Name);
        EnterKeyboardInteractionMode();
        RefreshVisualState(animate: false);
        RaiseSliceClicked(sliceVisual);
    }

    internal void InvokeCenterFromAutomation()
    {
        if (!CanInvokeFromAutomation)
        {
            return;
        }

        Log.Debug("Center close target invoked through UI Automation");
        CenterClicked?.Invoke(this, EventArgs.Empty);
    }

    internal bool IsSliceKeyboardFocused(PieSliceVisual sliceVisual)
    {
        return IsKeyboardFocused
            && _interactionMode == InteractionMode.Keyboard
            && _selectionController.SelectedIndex == sliceVisual.Index;
    }

    private bool HandleDigitKey(int digit)
    {
        if (_isDismissing)
        {
            // The menu is already fading out after a slice fired; a second press must not run another action.
            return true;
        }

        if (!_selectionController.TrySelectDigit(digit, GetSelectionItems()))
        {
            return false;
        }

        Log.Debug("Digit key {Digit} pressed; activating selected slice", digit);
        EnterKeyboardInteractionMode();
        RefreshVisualState(animate: false);
        ActivateSelectedSlice();
        return true;
    }

    private void OnPieCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (_interactionMode != InteractionMode.Keyboard)
        {
            return;
        }

        var position = e.GetPosition(PieCanvas);
        if (_hasKeyboardModeMousePosition
            && Math.Abs(position.X - _keyboardModeMousePosition.X) <= MouseMoveThreshold
            && Math.Abs(position.Y - _keyboardModeMousePosition.Y) <= MouseMoveThreshold)
        {
            return;
        }

        EnterMouseInteractionMode(refreshVisualState: true, animate: true);
    }

    private void MoveKeyboardSelection(Key key, bool isShiftPressed)
    {
        if (_isDismissing)
        {
            return;
        }

        var items = GetSelectionItems();
        switch (key)
        {
            case Key.Tab when isShiftPressed:
                _selectionController.SelectPrevious(items);
                break;
            case Key.Tab:
                _selectionController.SelectNext(items);
                break;
            case Key.Home:
                _selectionController.SelectFirst(items);
                break;
            case Key.End:
                _selectionController.SelectLast(items);
                break;
            default:
                _selectionController.HandleArrowKey(key, items);
                break;
        }

        if (_selectionController.SelectedIndex == PieSelectionController.NoSelection)
        {
            return;
        }

        EnterKeyboardInteractionMode();
        RefreshVisualState(animate: true);
        AnnounceKeyboardSelection();
    }

    private void EnterKeyboardInteractionMode()
    {
        _interactionMode = InteractionMode.Keyboard;
        _keyboardModeMousePosition = Mouse.GetPosition(PieCanvas);
        _hasKeyboardModeMousePosition = true;
    }

    private void AnnounceKeyboardSelection()
    {
        // The peer exists only once assistive technology has asked for it, so this costs nothing when none is running.
        if (UIElementAutomationPeer.FromElement(this) is not PieControlAutomationPeer peer)
        {
            return;
        }

        var selectedSlice = GetSelectedSliceVisual();
        if (selectedSlice != null)
        {
            peer.AnnounceSelection(selectedSlice);
        }
    }

    private void ActivateSelectedSlice()
    {
        if (_isDismissing)
        {
            return;
        }

        var selectedSlice = GetSelectedSliceVisual();
        if (selectedSlice != null)
        {
            RaiseSliceClicked(selectedSlice);
        }
    }

    private void RaiseSliceClicked(PieSliceVisual sliceVisual)
    {
        SliceClicked?.Invoke(this, new SliceClickEventArgs(sliceVisual.Action));
    }

    private bool OpenSelectedSliceContextMenu()
    {
        if (_isDismissing)
        {
            return true;
        }

        var selectedSlice = GetSelectedSliceVisual();
        if (selectedSlice == null)
        {
            return false;
        }

        // Opened from the keyboard, so attach the menu below the slice's icon and label. A Path's layout box starts at the canvas origin, so placing it relative to the Path alone would land over the hub.
        var contextMenu = selectedSlice.ContextMenu;
        if (selectedSlice.ContentPanel != null)
        {
            contextMenu.PlacementTarget = selectedSlice.ContentPanel;
        }
        else
        {
            contextMenu.PlacementTarget = selectedSlice.Path;
            contextMenu.PlacementRectangle = selectedSlice.Path.Data.Bounds;
        }

        contextMenu.Placement = PlacementMode.Bottom;
        contextMenu.IsOpen = true;
        return true;
    }

    private PieSliceVisual GetSelectedSliceVisual()
    {
        return _sliceVisuals.FirstOrDefault(slice => slice.Index == _selectionController.SelectedIndex);
    }

    public static readonly DependencyProperty SlicesProperty =
        DependencyProperty.Register(
            nameof(Slices),
            typeof(ObservableCollection<PieAction>),
            typeof(PieControl),
            new PropertyMetadata(null, OnSlicesPropertyChanged));

    public ObservableCollection<PieAction> Slices
    {
        get => (ObservableCollection<PieAction>)GetValue(SlicesProperty);
        set => SetValue(SlicesProperty, value);
    }

    private static void OnSlicesPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PieControl control)
        {
            return;
        }

        if (e.OldValue is ObservableCollection<PieAction> oldCollection)
        {
            oldCollection.CollectionChanged -= control.OnSlicesCollectionChanged;
            foreach (var item in oldCollection)
            {
                item.PropertyChanged -= control.OnSlicePropertyChanged;
            }
        }

        if (e.NewValue is ObservableCollection<PieAction> newCollection)
        {
            newCollection.CollectionChanged += control.OnSlicesCollectionChanged;
            foreach (var item in newCollection)
            {
                item.PropertyChanged += control.OnSlicePropertyChanged;
            }
        }

        control.RequestRenderRefresh();
    }

    private void OnSlicesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (PieAction item in e.OldItems)
            {
                item.PropertyChanged -= OnSlicePropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (PieAction item in e.NewItems)
            {
                item.PropertyChanged += OnSlicePropertyChanged;
            }
        }

        RequestRenderRefresh();
    }

    private void OnSlicePropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        RequestRenderRefresh();
    }

    private void CreatePieMenu()
    {
        PieCanvas.Children.Clear();
        _sliceVisuals.Clear();
        _centerVisual = null;
        _renderRefreshPending = false;
        _drag = null;
        _dragCandidate = null;
        _buildDpi = VisualTreeHelper.GetDpi(this);

        var enabledSlices = Slices?
            .Where(slice => slice?.IsEnabled == true)
            .ToList();

        if (enabledSlices == null || enabledSlices.Count == 0 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            Log.Debug(
                "Skipping pie render (EnabledSlices={EnabledSliceCount}, TotalSlices={TotalSliceCount}, Width={Width}, Height={Height})",
                enabledSlices?.Count ?? 0,
                Slices?.Count ?? 0,
                ActualWidth,
                ActualHeight);
            _selectionController.Reset();
            ResetAutomationChildren();

            // Nothing was rendered; keep the refresh pending so the next opportunity (like the next open) retries.
            _renderRefreshPending = true;
            return;
        }

        var theme = PieThemeSnapshot.Capture(
            TryFindResource,
            PieThemeSnapshot.IsAppDarkModeEnabled());
        _renderState.ApplyTheme(theme);

        if (!PieLayoutCalculator.TryCreateLayout(
                ActualWidth,
                ActualHeight,
                enabledSlices.Count,
                DefaultCenterHoleRatio,
                theme.SliceStrokeThickness,
                SnapToDevicePixel,
                out var layout))
        {
            Log.Warning(
                "Failed to create pie layout (EnabledSlices={EnabledSliceCount}, TotalSlices={TotalSliceCount}, Width={Width}, Height={Height})",
                enabledSlices.Count,
                Slices?.Count ?? 0,
                ActualWidth,
                ActualHeight);
            ResetAutomationChildren();

            // Nothing was rendered; keep the refresh pending so the next opportunity (like the next open) retries.
            _renderRefreshPending = true;
            return;
        }

        var canvasSize = layout.CanvasSize;
        var center = layout.Center;
        var innerRadius = layout.InnerRadius;
        var outerRadius = layout.OuterRadius;
        var angleStep = layout.AngleStep;

        PieCanvas.Width = canvasSize;
        PieCanvas.Height = canvasSize;
        _layoutCenter = center;
        _layoutAngleStep = angleStep;
        _layoutInnerRadius = innerRadius;
        _layoutOuterRadius = outerRadius;

        var contentScale = PieLayoutCalculator.GetContentScale(outerRadius);
        var hintFontSize = new TextBlock { Style = theme.HintTextStyle }.FontSize * contentScale;
        var releaseHintFontSize = new TextBlock { Style = theme.GlyphTextStyle }.FontSize * contentScale;
        var hintRadius = PieLayoutCalculator.GetRimHintRadius(outerRadius, theme.SliceStrokeThickness, theme.SelectionArcThickness, Math.Max(hintFontSize, releaseHintFontSize));

        // Outside high contrast, foregrounds don't change with selection, so every slice shares one frozen brush per role.
        var strokeBrush = CreateFrozenBrush(theme.StrokeColor);
        var iconBrush = CreateFrozenBrush(theme.IconTextColor);
        var labelBrush = CreateFrozenBrush(theme.LabelTextColor);
        var hintBrush = CreateFrozenBrush(theme.HintTextColor);
        var accentBrush = CreateFrozenBrush(theme.AccentColor);

        PieVisualBuilder.AddSurfaceRing(
            PieCanvas,
            center,
            outerRadius,
            innerRadius,
            theme.SliceColor,
            theme.StrokeColor,
            theme.SliceStrokeThickness,
            theme.IsHighContrast,
            theme.AmbientShadowEffect);

        if (innerRadius > 0)
        {
            AddCenterVisual(theme, center, innerRadius, contentScale);
        }

        for (var i = 0; i < enabledSlices.Count; i++)
        {
            var sliceAction = enabledSlices[i];
            var startAngle = (i * angleStep) - 90;
            var endAngle = startAngle + angleStep;

            var slice = new Path
            {
                Data = PieLayoutCalculator.CreateSliceGeometry(
                    center,
                    outerRadius,
                    innerRadius,
                    startAngle,
                    endAngle,
                    (centerPoint, radius, angle) => PieLayoutCalculator.GetPointOnCircle(centerPoint, radius, angle, SnapPoint)),
            };
            if (theme.SlicePathStyle != null)
            {
                slice.Style = theme.SlicePathStyle;
            }

            var fillBrush = new SolidColorBrush(theme.SliceColor);
            slice.Fill = fillBrush;
            slice.Stroke = strokeBrush;
            slice.StrokeThickness = theme.SliceStrokeThickness;
            slice.Cursor = Cursors.Hand;
            slice.SnapsToDevicePixels = true;

            // Press scale and reorder rotation live in one group so both effects can apply at once.
            // Scaling about the pie center keeps the separators on their lines, so a pressed slice settles inward instead of tearing off the ring.
            var pressScale = new ScaleTransform(1, 1, center.X, center.Y);
            var reorderRotation = new RotateTransform(0, center.X, center.Y);
            var sliceTransform = new TransformGroup { Children = { pressScale, reorderRotation } };
            slice.RenderTransform = sliceTransform;

            var contextMenu = CreateSliceContextMenu(sliceAction, theme);
            slice.ContextMenu = contextMenu;

            var textRadius = innerRadius > 0 ? (outerRadius + innerRadius) / 2 : outerRadius * 0.6;
            var textPosition = PieLayoutCalculator.GetTextPosition(center, textRadius, startAngle, endAngle, SnapPoint);

            var sliceVisual = new PieSliceVisual
            {
                Index = i,
                MidAngle = (startAngle + endAngle) / 2,
                Action = sliceAction,
                Path = slice,
                FillBrush = fillBrush,
                ContextMenu = contextMenu,
                ContentCenter = textPosition,
                PathRotation = reorderRotation,
                CurrentSlot = i,
                ForegroundBrush = theme.IsHighContrast ? new SolidColorBrush(theme.LabelTextColor) : null,
            };
            _sliceVisuals.Add(sliceVisual);
            AttachSliceInputHandlers(sliceVisual);

            Panel.SetZIndex(slice, SliceZIndex);
            PieCanvas.Children.Add(slice);

            if (PieLayoutCalculator.TryGetSelectionArc(
                    outerRadius,
                    theme.SliceStrokeThickness,
                    theme.SelectionArcThickness,
                    SelectionArcSeparatorGap,
                    startAngle,
                    endAngle,
                    out var arcSpan))
            {
                var selectionArc = PieVisualBuilder.CreateSelectionArc(
                    PieLayoutCalculator.CreateArcGeometry(center, arcSpan.Radius, arcSpan.StartAngle, arcSpan.EndAngle),
                    theme.SelectionArcThickness,
                    sliceVisual.ForegroundBrush ?? accentBrush,
                    theme.SelectionArcStyle,
                    sliceTransform);
                Panel.SetZIndex(selectionArc, SliceContentZIndex);
                PieCanvas.Children.Add(selectionArc);
                sliceVisual.SelectionArc = selectionArc;
            }

            var hintPosition = PieLayoutCalculator.GetTextPosition(center, hintRadius, startAngle, endAngle, SnapPoint);
            var hintBounds = Rect.Empty;
            if (i < MaxDigitHints)
            {
                var digitHint = PieVisualBuilder.CreateSliceDigitHint(
                    i + 1,
                    theme.HintTextStyle,
                    sliceVisual.ForegroundBrush ?? hintBrush,
                    contentScale);
                hintBounds.Union(AddRimHint(digitHint, hintPosition));
                sliceVisual.DigitHint = digitHint;
            }

            // Takes the digit hint's spot on the rim while the held hotkey targets this slice; the digit fades out to make room.
            var releaseHint = PieVisualBuilder.CreateSliceReleaseHint(
                theme.GlyphTextStyle,
                theme.SymbolFontFamily,
                sliceVisual.ForegroundBrush ?? accentBrush,
                contentScale);
            hintBounds.Union(AddRimHint(releaseHint, hintPosition));
            sliceVisual.ReleaseHint = releaseHint;

            var content = PieVisualBuilder.CreateSliceContent(
                sliceAction,
                theme.IconTextStyle,
                theme.LabelTextStyle,
                sliceVisual.ForegroundBrush ?? iconBrush,
                sliceVisual.ForegroundBrush ?? labelBrush,
                theme.IconToLabelSpacing,
                theme.ContentPadding,
                contentScale);
            if (content.Panel == null)
            {
                continue;
            }

            var hintKeepOut = hintBounds;
            hintKeepOut.Offset(-center.X, -center.Y);
            hintKeepOut.Inflate(LabelHintGap, LabelHintGap);
            if (FitSliceLabel(content, textPosition - center, startAngle, endAngle, hintKeepOut, theme))
            {
                // Trimmed names stay discoverable: the slice's automation name always carries the full name, and sighted users get it on hover.
                slice.ToolTip = sliceAction.Name;
            }

            var contentPanel = content.Panel;
            contentPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var contentSize = contentPanel.DesiredSize;

            var contentLeft = SnapToDevicePixel(textPosition.X - (contentSize.Width / 2), isXAxis: true);
            var contentTop = SnapToDevicePixel(textPosition.Y - (contentSize.Height / 2), isXAxis: false);
            Canvas.SetLeft(contentPanel, contentLeft);
            Canvas.SetTop(contentPanel, contentTop);

            // Counter-rotate about the content's own center first, then orbit around the pie
            // center, so the content follows its slice during reordering but stays upright.
            var contentMargin = contentPanel.Margin;
            var contentCounter = new RotateTransform(
                0,
                (contentSize.Width - contentMargin.Left - contentMargin.Right) / 2,
                (contentSize.Height - contentMargin.Top - contentMargin.Bottom) / 2);
            var contentOrbit = new RotateTransform(
                0,
                center.X - contentLeft - contentMargin.Left,
                center.Y - contentTop - contentMargin.Top);
            contentPanel.RenderTransform = new TransformGroup { Children = { contentCounter, contentOrbit } };
            sliceVisual.ContentPanel = contentPanel;
            sliceVisual.ContentCounter = contentCounter;
            sliceVisual.ContentOrbit = contentOrbit;

            Panel.SetZIndex(contentPanel, SliceContentZIndex);
            PieCanvas.Children.Add(contentPanel);
        }

        _selectionController.EnsureSelectionIsValid(GetSelectionItems());

        Log.Debug(
            "Pie menu rendered with {SliceCount} slices at {CanvasSize}px",
            _sliceVisuals.Count,
            canvasSize);

        // Slices built while the menu is visible (after a drag reorder) haven't been hit-tested yet, so IsMouseOver is false under the pointer; find that slice from geometry so it doesn't blink to the normal fill for a frame.
        var hoveredIndex = IsVisible && _interactionMode == InteractionMode.Mouse
            ? PieLayoutCalculator.GetSliceIndexAt(center, innerRadius, outerRadius, _sliceVisuals.Count, Mouse.GetPosition(PieCanvas))
            : PieSelectionController.NoSelection;
        RefreshVisualState(animate: false, hoveredIndex);
        ResetAutomationChildren();
    }

    private void AddCenterVisual(PieThemeSnapshot theme, Point center, double innerRadius, double contentScale)
    {
        const int centerZIndex = 20;

        var centerElements = PieVisualBuilder.CreateCenterElements(
            innerRadius,
            theme.HubStrokeThickness,
            theme.HubColor,
            theme.StrokeColor,
            theme.HubGlyphColor,
            theme.SymbolFontFamily,
            theme.HubEllipseStyle,
            theme.HubContainerStyle,
            theme.HubGlyphTextStyle,
            contentScale);

        var target = centerElements.Target;
        var centerVisual = new PieCenterVisual
        {
            Target = target,
            FillBrush = centerElements.FillBrush,
            GlyphBrush = centerElements.GlyphBrush,
        };
        _centerVisual = centerVisual;

        target.MouseEnter += (_, _) =>
        {
            if (_isDismissing || _interactionMode != InteractionMode.Mouse)
            {
                return;
            }

            ApplyCenterVisual(isHovered: true, animate: true);
        };

        target.MouseLeave += (_, _) =>
        {
            if (!_isDismissing && _interactionMode == InteractionMode.Mouse)
            {
                ApplyCenterVisual(isHovered: false, animate: true);
            }

            // Settles even while dismissing so the hub isn't left shrunken through the fade.
            ReleaseCenterPress(centerVisual);
        };

        target.MouseLeftButtonDown += (_, e) =>
        {
            EnterMouseInteractionMode(refreshVisualState: false, animate: false);
            centerVisual.IsPressed = true;
            _animationService.AnimateClickDown(target, HubPressedScale, _renderState.PressDuration, _renderState.StandardEasing);
            e.Handled = true;
        };

        target.MouseLeftButtonUp += (_, e) =>
        {
            if (!centerVisual.IsPressed)
            {
                return;
            }

            ReleaseCenterPress(centerVisual);
            if (_interactionMode == InteractionMode.Mouse)
            {
                ApplyCenterVisual(isHovered: target.IsMouseOver, animate: true);
            }

            CenterClicked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        };

        target.MouseRightButtonUp += (_, e) =>
        {
            EnterMouseInteractionMode(refreshVisualState: false, animate: false);
            CenterContextMenuRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        };

        Canvas.SetLeft(target, SnapToDevicePixel(center.X - innerRadius, isXAxis: true));
        Canvas.SetTop(target, SnapToDevicePixel(center.Y - innerRadius, isXAxis: false));
        Panel.SetZIndex(target, centerZIndex);
        PieCanvas.Children.Add(target);
    }

    private void AttachSliceInputHandlers(PieSliceVisual sliceVisual)
    {
        var slice = sliceVisual.Path;

        slice.MouseLeftButtonDown += (_, e) =>
        {
            EnterMouseInteractionMode(refreshVisualState: false, animate: false);
            sliceVisual.IsPressed = true;
            _animationService.ApplyBrushColor(sliceVisual.FillBrush, _renderState.PressedColor, !_renderState.IsHighContrast, _renderState.PressDuration, _renderState.StandardEasing);
            if (sliceVisual.ForegroundBrush != null)
            {
                // High contrast presses on the Highlight fill, so the foreground switches to HighlightText even if the slice wasn't highlighted before the press.
                ApplyBrushColor(sliceVisual.ForegroundBrush, _renderState.SelectedForegroundColor, animate: false);
            }

            _animationService.AnimateClickDown(slice, SlicePressedScale, _renderState.PressDuration, _renderState.StandardEasing);
            BeginDragTracking(sliceVisual, e.GetPosition(PieCanvas));
            e.Handled = true;
        };

        slice.MouseMove += (_, e) => HandleSliceMouseMove(sliceVisual, e);

        slice.LostMouseCapture += (_, _) => OnSliceLostMouseCapture(sliceVisual);

        slice.MouseLeftButtonUp += (_, e) =>
        {
            if (EndDragTracking(sliceVisual))
            {
                sliceVisual.IsPressed = false;
                e.Handled = true;
                return;
            }

            if (!sliceVisual.IsPressed)
            {
                return;
            }

            ReleaseSlicePress(sliceVisual);
            if (_interactionMode == InteractionMode.Mouse)
            {
                if (slice.IsMouseOver)
                {
                    ApplySliceHoverVisual(sliceVisual, animate: true);
                }
                else
                {
                    ApplySliceNormalVisual(sliceVisual, animate: true);
                }
            }
            else
            {
                RefreshVisualState(animate: true);
            }

            RaiseSliceClicked(sliceVisual);
            e.Handled = true;
        };

        slice.MouseEnter += (_, _) =>
        {
            if (_isDismissing || _interactionMode != InteractionMode.Mouse || _drag != null)
            {
                return;
            }

            ApplySliceHoverVisual(sliceVisual, animate: true);
        };

        slice.MouseLeave += (_, _) =>
        {
            if (_drag?.Slice == sliceVisual)
            {
                return;
            }

            // While dismissing, the fired slice keeps its highlight; turning off hit testing for the fade is what raised this MouseLeave.
            if (!_isDismissing && _interactionMode == InteractionMode.Mouse && _drag == null)
            {
                ApplySliceNormalVisual(sliceVisual, animate: true);
            }

            ReleaseSlicePress(sliceVisual);
        };
    }

    private void ReleaseSlicePress(PieSliceVisual sliceVisual)
    {
        if (!sliceVisual.IsPressed)
        {
            return;
        }

        sliceVisual.IsPressed = false;
        _animationService.AnimateClickUp(sliceVisual.Path, _renderState.PressDuration, _renderState.StandardEasing);
    }

    private void ReleaseCenterPress(PieCenterVisual centerVisual)
    {
        if (!centerVisual.IsPressed)
        {
            return;
        }

        centerVisual.IsPressed = false;
        _animationService.AnimateClickUp(centerVisual.Target, _renderState.PressDuration, _renderState.StandardEasing);
    }

    /// <summary>
    /// Limits the label to the room its slice actually has, or drops it for an icon-only slice when even a short trimmed name won't fit.
    /// </summary>
    /// <returns>True when the label was trimmed or dropped, so the full name needs another way to be seen.</returns>
    private bool FitSliceLabel(
        PieVisualBuilder.SliceContent content,
        Vector textOffset,
        double startAngle,
        double endAngle,
        Rect hintKeepOut,
        PieThemeSnapshot theme)
    {
        var label = content.Label;
        if (label == null)
        {
            return false;
        }

        content.Panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var labelSize = label.DesiredSize;

        // The content stack is centered on the text position, and the label is its bottom row.
        var rowCenter = textOffset + new Vector(0, (content.Panel.DesiredSize.Height / 2) - content.Panel.Margin.Bottom - (labelSize.Height / 2));
        var fittedWidth = PieLayoutCalculator.GetLabelMaxWidth(
            rowCenter,
            labelSize.Height,
            _layoutInnerRadius,
            _layoutOuterRadius - theme.SliceStrokeThickness - theme.SelectionArcThickness,
            startAngle,
            endAngle,
            theme.SliceStrokeThickness + LabelEdgePadding,
            hintKeepOut);
        var maxWidth = Math.Min(fittedWidth, _layoutOuterRadius * LabelMaxWidthRatio);

        // Changing a child only dirties the child, so the panel would otherwise report its cached unconstrained size and be positioned off-center.
        content.Panel.InvalidateMeasure();

        if (maxWidth < MinLabelWidth && content.Icon != null)
        {
            PieVisualBuilder.DropLabel(content);
            return true;
        }

        label.MaxWidth = maxWidth;
        return labelSize.Width > maxWidth;
    }

    private Rect AddRimHint(TextBlock hint, Point position)
    {
        hint.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = hint.DesiredSize;
        var left = SnapToDevicePixel(position.X - (size.Width / 2), isXAxis: true);
        var top = SnapToDevicePixel(position.Y - (size.Height / 2), isXAxis: false);
        Canvas.SetLeft(hint, left);
        Canvas.SetTop(hint, top);
        Panel.SetZIndex(hint, SliceContentZIndex);
        PieCanvas.Children.Add(hint);
        return new Rect(left, top, size.Width, size.Height);
    }

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void ResetAutomationChildren()
    {
        // FromElement only returns a peer once assistive technology has asked for one, so no peers are created here otherwise.
        UIElementAutomationPeer.FromElement(this)?.ResetChildrenCache();
    }

    private void EnterMouseInteractionMode(bool refreshVisualState, bool animate)
    {
        _interactionMode = InteractionMode.Mouse;
        _selectionController.Reset();
        _hasKeyboardModeMousePosition = false;

        if (refreshVisualState)
        {
            RefreshVisualState(animate);
        }
    }

    private void BeginDragTracking(PieSliceVisual sliceVisual, Point pressPosition)
    {
        if (_sliceVisuals.Count < 2)
        {
            _dragCandidate = null;
            return;
        }

        _dragCandidate = sliceVisual;
        _dragPressPosition = pressPosition;
    }

    private void HandleSliceMouseMove(PieSliceVisual sliceVisual, MouseEventArgs e)
    {
        if (_drag != null)
        {
            if (_drag.Slice == sliceVisual)
            {
                UpdateDrag(e.GetPosition(PieCanvas));
            }

            return;
        }

        if (_dragCandidate != sliceVisual || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(PieCanvas);
        var distance = (position - _dragPressPosition).Length;
        if (distance < DragStartThreshold)
        {
            return;
        }

        StartDrag(sliceVisual, position);
    }

    private void StartDrag(PieSliceVisual sliceVisual, Point position)
    {
        Log.Debug("Started dragging slice to reorder: {SliceName}", sliceVisual.Action.Name);

        _drag = new DragReorderState
        {
            Slice = sliceVisual,
            LastPointerAngle = PieReorderCalculator.GetPointerAngle(_layoutCenter, position),
            RotationOffset = sliceVisual.RotationOffset,
            TargetSlot = sliceVisual.CurrentSlot,
        };

        // Release any running reorder animations so the angle can be driven directly.
        sliceVisual.PathRotation?.BeginAnimation(RotateTransform.AngleProperty, null);
        sliceVisual.ContentOrbit?.BeginAnimation(RotateTransform.AngleProperty, null);
        sliceVisual.ContentCounter?.BeginAnimation(RotateTransform.AngleProperty, null);

        _animationService.AnimateClickUp(sliceVisual.Path, _renderState.PressDuration, _renderState.StandardEasing);
        ApplySliceHoverVisual(sliceVisual, animate: true);
        Panel.SetZIndex(sliceVisual.Path, DraggedSliceZIndex);
        if (sliceVisual.SelectionArc != null)
        {
            Panel.SetZIndex(sliceVisual.SelectionArc, DraggedSliceContentZIndex);
        }

        if (sliceVisual.ContentPanel != null)
        {
            Panel.SetZIndex(sliceVisual.ContentPanel, DraggedSliceContentZIndex);
        }

        sliceVisual.Path.CaptureMouse();
    }

    private void UpdateDrag(Point position)
    {
        var drag = _drag;
        var pointerAngle = PieReorderCalculator.GetPointerAngle(_layoutCenter, position);
        drag.RotationOffset += PieLayoutCalculator.NormalizeSignedAngle(pointerAngle - drag.LastPointerAngle);
        drag.LastPointerAngle = pointerAngle;

        SetSliceRotation(drag.Slice, drag.RotationOffset);

        var targetSlot = PieReorderCalculator.GetTargetSlot(
            drag.Slice.Index,
            drag.RotationOffset,
            _layoutAngleStep,
            _sliceVisuals.Count);
        if (targetSlot == drag.TargetSlot)
        {
            return;
        }

        drag.TargetSlot = targetSlot;
        var slots = PieReorderCalculator.GetSlotAssignments(_sliceVisuals.Count, drag.Slice.Index, targetSlot);
        foreach (var sliceVisual in _sliceVisuals)
        {
            if (sliceVisual == drag.Slice)
            {
                continue;
            }

            sliceVisual.CurrentSlot = slots[sliceVisual.Index];
            var targetRotation = PieReorderCalculator.GetNearestEquivalentAngle(
                sliceVisual.RotationOffset,
                (sliceVisual.CurrentSlot - sliceVisual.Index) * _layoutAngleStep);
            sliceVisual.RotationOffset = targetRotation;
            AnimateSliceRotation(sliceVisual, targetRotation);
        }
    }

    private bool EndDragTracking(PieSliceVisual sliceVisual)
    {
        _dragCandidate = null;

        if (_drag?.Slice != sliceVisual)
        {
            return false;
        }

        var drag = _drag;
        _drag = null;

        _isReleasingDragCapture = true;
        try
        {
            sliceVisual.Path.ReleaseMouseCapture();
        }
        finally
        {
            _isReleasingDragCapture = false;
        }

        sliceVisual.CurrentSlot = drag.TargetSlot;
        var settledRotation = PieReorderCalculator.GetNearestEquivalentAngle(
            drag.RotationOffset,
            (drag.TargetSlot - sliceVisual.Index) * _layoutAngleStep);
        sliceVisual.RotationOffset = settledRotation;
        AnimateSliceRotation(sliceVisual, settledRotation, () => CommitReorder(sliceVisual, drag.TargetSlot));
        return true;
    }

    private void CommitReorder(PieSliceVisual sliceVisual, int targetSlot)
    {
        if (targetSlot == sliceVisual.Index)
        {
            Log.Debug("Slice drag ended in its original slot: {SliceName}", sliceVisual.Action.Name);
            if (sliceVisual.SelectionArc != null)
            {
                Panel.SetZIndex(sliceVisual.SelectionArc, SliceContentZIndex);
            }

            if (sliceVisual.ContentPanel != null)
            {
                Panel.SetZIndex(sliceVisual.ContentPanel, SliceContentZIndex);
            }

            RefreshVisualState(animate: true);
            return;
        }

        // Move the dragged action to the position of the action that was built at the target
        // slot; disabled actions keep their relative placement in the collection.
        // The commit runs from an animation callback, so a rebuild (settings edit, theme change) may have
        // replaced the visuals in the meantime and the target slot may no longer exist.
        var targetAction = _sliceVisuals.FirstOrDefault(visual => visual.Index == targetSlot)?.Action;
        var fromIndex = Slices.IndexOf(sliceVisual.Action);
        var toIndex = targetAction == null ? -1 : Slices.IndexOf(targetAction);
        if (fromIndex < 0 || toIndex < 0 || fromIndex == toIndex)
        {
            Log.Warning(
                "Could not commit slice reorder (FromIndex={FromIndex}, ToIndex={ToIndex})",
                fromIndex,
                toIndex);
            RequestRenderRefresh();
            return;
        }

        Log.Information(
            "Reordered slice by dragging: {SliceName} ({FromIndex} -> {ToIndex})",
            sliceVisual.Action.Name,
            fromIndex,
            toIndex);
        Slices.Move(fromIndex, toIndex);
        SlicesReordered?.Invoke(this, EventArgs.Empty);
    }

    private void OnSliceLostMouseCapture(PieSliceVisual sliceVisual)
    {
        if (_isReleasingDragCapture || _drag?.Slice != sliceVisual)
        {
            return;
        }

        _drag = null;
        _dragCandidate = null;

        if (_isDismissing)
        {
            // The dismiss took the capture. Leave the slices where they are for the fade; the rebuild that resets them runs at idle once the menu is hidden.
            Log.Debug("Slice drag canceled because the menu is closing: {SliceName}", sliceVisual.Action.Name);
            if (IsVisible)
            {
                // Still fading out, so a build now would land at render priority mid-fade; hiding re-queues the pending rebuild at idle, and ResetInputState queues it if the menu reopens before it hides.
                _renderRefreshPending = true;
            }
            else
            {
                // An immediate hide raises IsVisibleChanged before the capture loss arrives, so queue the rebuild here; hidden, it lands at idle.
                RequestRenderRefresh();
            }

            return;
        }

        Log.Debug("Slice drag canceled because mouse capture was lost: {SliceName}", sliceVisual.Action.Name);
        RequestRenderRefresh();
    }

    private static void SetSliceRotation(PieSliceVisual sliceVisual, double angle)
    {
        sliceVisual.PathRotation.Angle = angle;
        if (sliceVisual.ContentOrbit != null)
        {
            sliceVisual.ContentOrbit.Angle = angle;
            sliceVisual.ContentCounter.Angle = -angle;
        }
    }

    private void AnimateSliceRotation(PieSliceVisual sliceVisual, double toAngle, Action onCompleted = null)
    {
        var duration = _renderState.ReorderDuration;
        var easing = _renderState.DecelerateEasing;
        _animationService.AnimateRotationAngle(sliceVisual.PathRotation, toAngle, duration, easing, onCompleted);
        if (sliceVisual.ContentOrbit != null)
        {
            _animationService.AnimateRotationAngle(sliceVisual.ContentOrbit, toAngle, duration, easing);
            _animationService.AnimateRotationAngle(sliceVisual.ContentCounter, -toAngle, duration, easing);
        }
    }

    private ContextMenu CreateSliceContextMenu(PieAction sliceAction, PieThemeSnapshot theme)
    {
        var editIcon = new ContentControl { Content = EditGlyph };
        if (theme.MenuItemIconStyle != null)
        {
            editIcon.Style = theme.MenuItemIconStyle;
        }
        else
        {
            editIcon.FontFamily = theme.SymbolFontFamily;
            editIcon.Focusable = false;
        }

        var editMenuItem = new MenuItem
        {
            Header = "_Edit…",
            Icon = editIcon,
        };
        editMenuItem.Click += (_, _) => SliceEditRequested?.Invoke(this, new SliceClickEventArgs(sliceAction));

        var contextMenu = new ContextMenu();
        contextMenu.Items.Add(editMenuItem);

        // The keyboard path places the menu below the slice; clear that afterwards so a right-click opens it at the pointer again.
        contextMenu.Closed += (_, _) =>
        {
            contextMenu.ClearValue(ContextMenu.PlacementProperty);
            contextMenu.ClearValue(ContextMenu.PlacementTargetProperty);
            contextMenu.ClearValue(ContextMenu.PlacementRectangleProperty);
        };

        return contextMenu;
    }

    // geometricHoverIndex names a slice to treat as hovered in mouse mode even though it hasn't been hit-tested yet.
    private void RefreshVisualState(bool animate, int geometricHoverIndex = PieSelectionController.NoSelection)
    {
        if (_isDismissing)
        {
            return;
        }

        foreach (var sliceVisual in _sliceVisuals)
        {
            var isSelectedOrHovered = _interactionMode == InteractionMode.Keyboard
                ? _selectionController.SelectedIndex == sliceVisual.Index
                : sliceVisual.Path.IsMouseOver || sliceVisual.Index == geometricHoverIndex;

            if (isSelectedOrHovered)
            {
                ApplySliceHoverVisual(sliceVisual, animate);
            }
            else
            {
                ApplySliceNormalVisual(sliceVisual, animate);
            }
        }

        var isCenterHovered = _centerVisual != null
            && _interactionMode == InteractionMode.Mouse
            && _centerVisual.Target.IsMouseOver;
        ApplyCenterVisual(isCenterHovered, animate);
    }

    private void ApplySliceNormalVisual(PieSliceVisual sliceVisual, bool animate)
    {
        ApplySliceVisual(sliceVisual, isHighlighted: false, animate);
    }

    private void ApplySliceHoverVisual(PieSliceVisual sliceVisual, bool animate)
    {
        ApplySliceVisual(sliceVisual, isHighlighted: true, animate);
    }

    /// <summary>
    /// The hovered, keyboard-selected or release-targeted slice gets the hover fill and its accent selection arc; the stroke stays neutral.
    /// </summary>
    private void ApplySliceVisual(PieSliceVisual sliceVisual, bool isHighlighted, bool animate)
    {
        // High contrast switches between system color pairs, which must never pass through blended in-between colors.
        animate &= !_renderState.IsHighContrast;

        Panel.SetZIndex(sliceVisual.Path, isHighlighted ? HoveredSliceZIndex : SliceZIndex);
        ApplyBrushColor(sliceVisual.FillBrush, isHighlighted ? _renderState.HoverColor : _renderState.SliceColor, animate);

        if (sliceVisual.SelectionArc != null)
        {
            _animationService.ApplyOpacity(sliceVisual.SelectionArc, isHighlighted ? 1 : 0, animate, _renderState.HoverDuration, _renderState.StandardEasing);
        }

        if (sliceVisual.ForegroundBrush != null)
        {
            ApplyBrushColor(sliceVisual.ForegroundBrush, isHighlighted ? _renderState.SelectedForegroundColor : _renderState.ForegroundColor, animate: false);
        }

        ApplyReleaseHintVisual(sliceVisual, isTargeted: isHighlighted, animate);
    }

    private void ApplyReleaseHintVisual(PieSliceVisual sliceVisual, bool isTargeted, bool animate)
    {
        // A release never triggers during a drag, so the hint must not promise one.
        SetReleaseHintVisible(sliceVisual, isTargeted && _isReleaseTriggerArmed && _drag == null, animate);
    }

    private void SetReleaseHintVisible(PieSliceVisual sliceVisual, bool isVisible, bool animate)
    {
        if (sliceVisual.ReleaseHint != null)
        {
            _animationService.ApplyOpacity(sliceVisual.ReleaseHint, isVisible ? 1 : 0, animate, _renderState.HoverDuration, _renderState.StandardEasing);
        }

        if (sliceVisual.DigitHint != null)
        {
            _animationService.ApplyOpacity(sliceVisual.DigitHint, isVisible ? 0 : 1, animate, _renderState.HoverDuration, _renderState.StandardEasing);
        }
    }

    /// <summary>
    /// The hub keeps its close glyph at rest in the hint color; hovering steps the fill to the hover color and turns the glyph accent.
    /// </summary>
    private void ApplyCenterVisual(bool isHovered, bool animate)
    {
        var centerVisual = _centerVisual;
        if (centerVisual == null)
        {
            return;
        }

        animate &= !_renderState.IsHighContrast;
        ApplyBrushColor(centerVisual.FillBrush, isHovered ? _renderState.HubHoverColor : _renderState.HubColor, animate);
        ApplyBrushColor(centerVisual.GlyphBrush, isHovered ? _renderState.HubGlyphHoverColor : _renderState.HubGlyphColor, animate);
    }

    private void ApplyBrushColor(SolidColorBrush brush, Color color, bool animate)
    {
        _animationService.ApplyBrushColor(brush, color, animate, _renderState.HoverDuration, _renderState.StandardEasing);
    }

    private void OnSystemParametersChanged(object sender, PropertyChangedEventArgs e)
    {
        // SystemEvents can deliver on a worker thread, and the refresh path reads dependency properties.
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => OnSystemParametersChanged(sender, e));
            return;
        }

        var propertyName = e.PropertyName;
        if (string.IsNullOrEmpty(propertyName)
            || propertyName.Contains("Color", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("Brush", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("Contrast", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("Theme", StringComparison.OrdinalIgnoreCase))
        {
            RequestRenderRefresh();
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        // SystemEvents can deliver on a worker thread, and the refresh path reads dependency properties.
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => OnUserPreferenceChanged(sender, e));
            return;
        }

        RequestRenderRefresh();
    }

    private void RequestRenderRefresh()
    {
        _renderRefreshPending = true;

        if (!IsLoaded)
        {
            return;
        }

        // Visible: Render priority so the rebuild lands before the next frame is presented; any lower and the menu could fade in while the pie was still missing, making it pop in partway through the animation.
        // Hidden: build at idle so the work happens while nothing is waiting on it and the next open skips the rebuild.
        // The hidden build still coalesces bursts (every keystroke while editing an action) into one pass.
        var priority = IsVisible ? DispatcherPriority.Render : DispatcherPriority.ApplicationIdle;

        if (_renderRefreshOperation != null)
        {
            // Already queued; only ever promote it (an idle build that became visible must not wait until idle).
            if (_renderRefreshOperation.Priority < priority)
            {
                _renderRefreshOperation.Priority = priority;
            }

            return;
        }

        _renderRefreshOperation = Dispatcher.InvokeAsync(() =>
        {
            _renderRefreshOperation = null;

            if (!IsLoaded)
            {
                Log.Debug("Skipping render refresh because PieControl is unloaded");
                return;
            }

            CreatePieMenu();
        }, priority);
    }

    private PieSelectionController.Item[] GetSelectionItems()
    {
        return _sliceVisuals.Select(static slice => slice.ToSelectionItem()).ToArray();
    }

    private Point SnapPoint(Point point)
    {
        return new Point(
            SnapToDevicePixel(point.X, isXAxis: true),
            SnapToDevicePixel(point.Y, isXAxis: false));
    }

    private double SnapToDevicePixel(double value, bool isXAxis)
    {
        var scale = isXAxis ? _buildDpi.DpiScaleX : _buildDpi.DpiScaleY;
        if (scale <= 0)
        {
            return value;
        }

        return Math.Round(value * scale) / scale;
    }

    /// <summary>
    /// Occurs when a slice is clicked.
    /// </summary>
    public event EventHandler<SliceClickEventArgs> SliceClicked;

    /// <summary>
    /// Occurs after the <see cref="Slices"/> collection is reordered by dragging a slice.
    /// </summary>
    public event EventHandler SlicesReordered;

    /// <summary>
    /// Occurs when the center close target is clicked.
    /// </summary>
    public event EventHandler CenterClicked;

    /// <summary>
    /// Occurs when a slice edit is requested from the context menu.
    /// </summary>
    public event EventHandler<SliceClickEventArgs> SliceEditRequested;

    /// <summary>
    /// Occurs when the center target requests the main context menu.
    /// </summary>
    public event EventHandler CenterContextMenuRequested;
}

/// <summary>
/// Event arguments for slice click events.
/// </summary>
public class SliceClickEventArgs : EventArgs
{
    public PieAction Slice { get; }

    public SliceClickEventArgs(PieAction action)
    {
        Slice = action;
    }
}
