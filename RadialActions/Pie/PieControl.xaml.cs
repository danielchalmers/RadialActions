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
    private const int SelectedSliceZIndex = 12;
    private const int HoveredSliceZIndex = 14;
    private const int SliceContentZIndex = 15;
    private const int DraggedSliceZIndex = 16;
    private const int DraggedSliceContentZIndex = 17;
    private const double MouseMoveThreshold = 0.25;

    /// <summary>
    /// The number keys 1-9 trigger slices, so hints are only shown for the first nine.
    /// </summary>
    private const int MaxDigitHints = 9;

    /// <summary>
    /// How far from the center the digit hints sit, as a fraction of the outer radius.
    /// Keeps them near the rim, clear of the icon and label at the slice midpoint.
    /// </summary>
    private const double DigitHintRadiusRatio = 0.9;

    /// <summary>
    /// How far the pointer must travel with the button held before a press becomes a drag.
    /// Deliberately much larger than the system drag threshold so held-but-jittering clicks
    /// during normal use never start reordering.
    /// </summary>
    private const double DragStartThreshold = 20;

    private static readonly Duration ReorderDuration = new(TimeSpan.FromMilliseconds(150));

    private enum InteractionMode
    {
        Mouse,
        Keyboard,
    }

    private bool _renderRefreshPending;
    private bool _renderRefreshQueued;
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
    private int _layoutSlotCount;
    private bool _slicesHandlersAttached;
    private PieSliceVisual _dragCandidate;
    private Point _dragPressPosition;
    private DragReorderState _drag;
    private bool _isReleasingDragCapture;
    private Path _ghostPath;

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
        AttachSlicesHandlers();
        RequestRenderRefresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

        // Detach so a closed or hidden host (like the settings window) doesn't stay alive through handlers on the app-lifetime actions collection.
        DetachSlicesHandlers(Slices);
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not bool isVisible)
        {
            return;
        }

        if (!isVisible)
        {
            // Hiding doesn't invalidate anything on its own. Real changes while hidden (theme, slices, size) set the pending flag
            // themselves through RequestRenderRefresh, whose queued callback returns without clearing it while the menu isn't visible.
            return;
        }

        if (_renderRefreshPending)
        {
            Log.Debug("PieControl visible again; applying deferred render refresh");
            RequestRenderRefresh();
        }
    }

    public void ResetInputState()
    {
        // The visuals survive across opens, so press and drag tracking from the previous open must be discarded
        // here or a held button in the next open can resume a drag that was never started there.
        _drag = null;
        _dragCandidate = null;

        EnterMouseInteractionMode(refreshVisualState: true, animate: false);
    }

    /// <summary>
    /// Triggers the active slice: the keyboard-selected slice in keyboard mode, otherwise the slice under the mouse.
    /// </summary>
    /// <returns>True if a slice was triggered.</returns>
    public bool TriggerActiveSlice()
    {
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

        SliceClicked?.Invoke(this, new SliceClickEventArgs(targetSlice.Action));
        return true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!IsEditMode || e.Handled)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        e.Handled = HandleEditModeKey(key, Keyboard.Modifiers);
    }

    /// <summary>
    /// Keyboard support for the editor pie: arrows move the selection around the ring, Ctrl+arrows reorder the selected action, Enter or Insert adds one, and Delete removes it.
    /// </summary>
    private bool HandleEditModeKey(Key key, ModifierKeys modifiers)
    {
        switch (key)
        {
            case Key.Right:
            case Key.Down:
                return modifiers.HasFlag(ModifierKeys.Control) ? MoveSelectedSlice(1) : MoveEditSelection(1);
            case Key.Left:
            case Key.Up:
                return modifiers.HasFlag(ModifierKeys.Control) ? MoveSelectedSlice(-1) : MoveEditSelection(-1);
            case Key.Return:
            case Key.Insert:
                RequestAddSlice();
                return true;
            case Key.Delete:
                if (SelectedSlice == null)
                {
                    return false;
                }

                RemoveSliceRequested?.Invoke(this, new SliceClickEventArgs(SelectedSlice));
                return true;
            default:
                return false;
        }
    }

    private bool MoveEditSelection(int delta)
    {
        if (_sliceVisuals.Count == 0)
        {
            return false;
        }

        var count = _sliceVisuals.Count;
        var index = _sliceVisuals.FindIndex(visual => visual.Action == SelectedSlice);
        index = index < 0
            ? (delta > 0 ? 0 : count - 1)
            : (((index + delta) % count) + count) % count;
        SelectedSlice = _sliceVisuals[index].Action;
        return true;
    }

    private bool MoveSelectedSlice(int delta)
    {
        if (SelectedSlice == null || Slices == null)
        {
            return false;
        }

        var fromIndex = Slices.IndexOf(SelectedSlice);
        if (fromIndex < 0 || Slices.Count < 2)
        {
            return false;
        }

        // Wrap like the ring does, so moving past either end carries the slice around.
        var count = Slices.Count;
        var toIndex = (((fromIndex + delta) % count) + count) % count;
        Log.Information("Reordered slice by keyboard: {SliceName} ({FromIndex} -> {ToIndex})", SelectedSlice.Name, fromIndex, toIndex);
        Slices.Move(fromIndex, toIndex);
        SlicesReordered?.Invoke(this, EventArgs.Empty);
        return true;
    }

    internal void RequestAddSlice()
    {
        AddSliceRequested?.Invoke(this, EventArgs.Empty);
    }

    internal IReadOnlyList<PieSliceVisual> SliceVisuals => _sliceVisuals;

    internal Path GhostPath => _ghostPath;

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new PieControlAutomationPeer(this);
    }

    public bool HandleMenuKey(Key key, ModifierKeys modifiers)
    {
        switch (key)
        {
            case Key.Up:
            case Key.Down:
            case Key.Left:
            case Key.Right:
                HandleArrowKey(key);
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

    private bool HandleDigitKey(int digit)
    {
        if (!_selectionController.TrySelectDigit(digit, GetSelectionItems()))
        {
            return false;
        }

        Log.Debug("Digit key {Digit} pressed; activating selected slice", digit);
        _interactionMode = InteractionMode.Keyboard;
        _keyboardModeMousePosition = Mouse.GetPosition(PieCanvas);
        _hasKeyboardModeMousePosition = true;
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

    private void HandleArrowKey(Key key)
    {
        _selectionController.HandleArrowKey(key, GetSelectionItems());
        if (_selectionController.SelectedIndex == PieSelectionController.NoSelection)
        {
            return;
        }

        _interactionMode = InteractionMode.Keyboard;
        _keyboardModeMousePosition = Mouse.GetPosition(PieCanvas);
        _hasKeyboardModeMousePosition = true;
        RefreshVisualState(animate: true);
    }

    private void ActivateSelectedSlice()
    {
        var selectedSlice = GetSelectedSliceVisual();
        if (selectedSlice != null)
        {
            SliceClicked?.Invoke(this, new SliceClickEventArgs(selectedSlice.Action));
        }
    }

    private bool OpenSelectedSliceContextMenu()
    {
        var selectedSlice = GetSelectedSliceVisual();
        if (selectedSlice?.ContextMenu == null)
        {
            return false;
        }

        selectedSlice.ContextMenu.PlacementTarget = selectedSlice.Path;
        selectedSlice.ContextMenu.Placement = PlacementMode.Center;
        selectedSlice.ContextMenu.IsOpen = true;
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

    public static readonly DependencyProperty IsEditModeProperty =
        DependencyProperty.Register(
            nameof(IsEditMode),
            typeof(bool),
            typeof(PieControl),
            new PropertyMetadata(false, OnIsEditModePropertyChanged));

    /// <summary>
    /// When true the pie renders as a live editor surface: every action is shown (disabled ones dimmed), a ghost slice at the end adds new actions, clicking selects instead of executing, and the selected slice is highlighted with the accent color.
    /// </summary>
    public bool IsEditMode
    {
        get => (bool)GetValue(IsEditModeProperty);
        set => SetValue(IsEditModeProperty, value);
    }

    private static void OnIsEditModePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PieControl control)
        {
            return;
        }

        // The editor is a keyboard-navigable control; the live menu keeps window-level key handling and stays unfocusable.
        control.Focusable = e.NewValue is true;
        control.RequestRenderRefresh();
    }

    public static readonly DependencyProperty SelectedSliceProperty =
        DependencyProperty.Register(
            nameof(SelectedSlice),
            typeof(PieAction),
            typeof(PieControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedSlicePropertyChanged));

    /// <summary>
    /// The action highlighted as selected while <see cref="IsEditMode"/> is on.
    /// </summary>
    public PieAction SelectedSlice
    {
        get => (PieAction)GetValue(SelectedSliceProperty);
        set => SetValue(SelectedSliceProperty, value);
    }

    private static void OnSelectedSlicePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PieControl control)
        {
            return;
        }

        control.RefreshVisualState(animate: true);

        if (e.NewValue is PieAction selected
            && AutomationPeer.ListenerExists(AutomationEvents.SelectionItemPatternOnElementSelected)
            && UIElementAutomationPeer.FromElement(control) is PieControlAutomationPeer peer)
        {
            peer.RaiseSelectionEvent(selected);
        }
    }

    private static void OnSlicesPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PieControl control)
        {
            return;
        }

        control.DetachSlicesHandlers(e.OldValue as ObservableCollection<PieAction>);

        // Wait for Loaded when the binding resolves before the control enters the tree, so unloaded controls never hold handlers on a long-lived collection.
        if (control.IsLoaded)
        {
            control.AttachSlicesHandlers();
        }

        control.RequestRenderRefresh();
    }

    private void AttachSlicesHandlers()
    {
        if (_slicesHandlersAttached || Slices is not ObservableCollection<PieAction> collection)
        {
            return;
        }

        collection.CollectionChanged += OnSlicesCollectionChanged;
        foreach (var item in collection)
        {
            item.PropertyChanged += OnSlicePropertyChanged;
        }

        _slicesHandlersAttached = true;
    }

    private void DetachSlicesHandlers(ObservableCollection<PieAction> collection)
    {
        if (!_slicesHandlersAttached || collection == null)
        {
            return;
        }

        collection.CollectionChanged -= OnSlicesCollectionChanged;
        foreach (var item in collection)
        {
            item.PropertyChanged -= OnSlicePropertyChanged;
        }

        _slicesHandlersAttached = false;
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
        const int centerZIndex = 20;

        PieCanvas.Children.Clear();
        _sliceVisuals.Clear();
        _centerVisual = null;
        _renderRefreshPending = false;
        _drag = null;
        _dragCandidate = null;
        _ghostPath = null;

        // Edit mode shows every action so hidden ones can still be selected and edited; the live menu only shows enabled ones.
        var visibleSlices = Slices?
            .Where(slice => slice != null && (IsEditMode || slice.IsEnabled))
            .ToList();

        // The ghost add slice takes up one extra slot in edit mode, so an empty editor still renders a full-circle ghost.
        var slotCount = (visibleSlices?.Count ?? 0) + (IsEditMode ? 1 : 0);

        if (visibleSlices == null || slotCount == 0 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            Log.Debug(
                "Skipping pie render (VisibleSlices={VisibleSliceCount}, TotalSlices={TotalSliceCount}, Width={Width}, Height={Height})",
                visibleSlices?.Count ?? 0,
                Slices?.Count ?? 0,
                ActualWidth,
                ActualHeight);
            _selectionController.Reset();

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
                slotCount,
                DefaultCenterHoleRatio,
                theme.SliceStrokeThickness,
                SnapToDevicePixel,
                out var layout))
        {
            Log.Warning(
                "Failed to create pie layout (VisibleSlices={VisibleSliceCount}, TotalSlices={TotalSliceCount}, Width={Width}, Height={Height})",
                visibleSlices.Count,
                Slices?.Count ?? 0,
                ActualWidth,
                ActualHeight);

            // Nothing was rendered; keep the refresh pending so the next opportunity (like the next open) retries.
            _renderRefreshPending = true;
            return;
        }

        var canvasSize = layout.CanvasSize;
        var center = layout.Center;
        var innerRadius = layout.InnerRadius;
        var outerRadius = layout.OuterRadius;

        PieCanvas.Width = canvasSize;
        PieCanvas.Height = canvasSize;
        var pressDuration = _renderState.PressDuration;

        PieVisualBuilder.AddSurfaceRing(
            PieCanvas,
            center,
            outerRadius,
            innerRadius,
            theme.SurfaceColor,
            theme.SurfaceBorderColor,
            theme.SliceStrokeThickness,
            theme.IsHighContrast,
            theme.AmbientShadowEffect);

        var angleStep = layout.AngleStep;
        _layoutCenter = center;
        _layoutAngleStep = angleStep;
        _layoutSlotCount = slotCount;

        if (innerRadius > 0)
        {
            var centerElements = PieVisualBuilder.CreateCenterElements(
                innerRadius,
                theme.HubStrokeThickness,
                theme.HubColor,
                theme.HubBorderColor,
                theme.IconTextColor,
                theme.HubEllipseStyle,
                theme.HubContainerStyle,
                theme.IconTextStyle);

            var centerVisual = new PieCenterVisual
            {
                Target = centerElements.Target,
                FillBrush = centerElements.FillBrush,
                StrokeBrush = centerElements.StrokeBrush,
                Icon = centerElements.Icon,
            };
            _centerVisual = centerVisual;

            if (IsEditMode)
            {
                // The hub is inert in edit mode; there is no menu to close.
                centerElements.Target.Cursor = Cursors.Arrow;
                foreach (UIElement child in centerElements.Target.Children)
                {
                    if (child is FrameworkElement childElement)
                    {
                        childElement.Cursor = Cursors.Arrow;
                    }
                }
            }
            else
            {
                WireCenterInteractions(centerElements, pressDuration);
            }

            Canvas.SetLeft(centerElements.Target, SnapToDevicePixel(center.X - innerRadius, isXAxis: true));
            Canvas.SetTop(centerElements.Target, SnapToDevicePixel(center.Y - innerRadius, isXAxis: false));
            Panel.SetZIndex(centerElements.Target, centerZIndex);
            PieCanvas.Children.Add(centerElements.Target);
        }

        for (var i = 0; i < visibleSlices.Count; i++)
        {
            var sliceAction = visibleSlices[i];
            var startAngle = (i * angleStep) - 90;
            var endAngle = startAngle + angleStep;

            var slice = PieLayoutCalculator.CreateSlice(
                center,
                outerRadius,
                innerRadius,
                startAngle,
                endAngle,
                (centerPoint, radius, angle) => PieLayoutCalculator.GetPointOnCircle(centerPoint, radius, angle, SnapPoint));
            if (theme.SlicePathStyle != null)
            {
                slice.Style = theme.SlicePathStyle;
            }

            var fillBrush = new SolidColorBrush(theme.SliceColor);
            var strokeBrush = new SolidColorBrush(theme.BorderColor);

            slice.Fill = fillBrush;
            slice.Stroke = strokeBrush;
            slice.StrokeThickness = theme.SliceStrokeThickness;
            slice.Cursor = Cursors.Hand;
            slice.SnapsToDevicePixels = true;

            // Press scale and reorder rotation live in one group so both effects can apply at once.
            var pathBounds = slice.Data.Bounds;
            var pressScale = new ScaleTransform(1, 1)
            {
                CenterX = pathBounds.X + (pathBounds.Width / 2),
                CenterY = pathBounds.Y + (pathBounds.Height / 2),
            };
            var reorderRotation = new RotateTransform(0, center.X, center.Y);
            slice.RenderTransform = new TransformGroup { Children = { pressScale, reorderRotation } };

            // No "Edit..." context menu in edit mode; the slice is already being edited in place.
            var contextMenu = IsEditMode ? null : CreateSliceContextMenu(sliceAction);
            slice.ContextMenu = contextMenu;

            var sliceVisual = new PieSliceVisual
            {
                Index = i,
                MidAngle = (startAngle + endAngle) / 2,
                Action = sliceAction,
                Path = slice,
                FillBrush = fillBrush,
                StrokeBrush = strokeBrush,
                ContextMenu = contextMenu,
                PathRotation = reorderRotation,
                CurrentSlot = i,
            };
            _sliceVisuals.Add(sliceVisual);

            var isMouseDown = false;

            slice.MouseLeftButtonDown += (_, e) =>
            {
                EnterMouseInteractionMode(refreshVisualState: false, animate: false);
                if (IsEditMode)
                {
                    Focus();
                }

                isMouseDown = true;
                _animationService.AnimateBrushColor(fillBrush, theme.PressedColor, pressDuration, _renderState.StandardEasing);
                _animationService.AnimateBrushColor(strokeBrush, _renderState.BorderHoverColor, pressDuration, _renderState.StandardEasing);
                _animationService.AnimateClickDown(slice, pressDuration, _renderState.StandardEasing);
                BeginDragTracking(sliceVisual, e.GetPosition(PieCanvas));
                e.Handled = true;
            };

            slice.MouseMove += (_, e) => HandleSliceMouseMove(sliceVisual, e);

            slice.LostMouseCapture += (_, _) => OnSliceLostMouseCapture(sliceVisual);

            slice.MouseLeftButtonUp += (_, e) =>
            {
                if (EndDragTracking(sliceVisual))
                {
                    isMouseDown = false;
                    e.Handled = true;
                    return;
                }

                if (!isMouseDown)
                {
                    return;
                }

                isMouseDown = false;
                _animationService.AnimateClickUp(slice, pressDuration, _renderState.StandardEasing);

                if (IsEditMode)
                {
                    SelectedSlice = sliceAction;
                }

                if (_interactionMode == InteractionMode.Mouse)
                {
                    if (slice.IsMouseOver)
                    {
                        ApplySliceHoverVisual(sliceVisual, animate: true);
                    }
                    else
                    {
                        ApplySliceRestingVisual(sliceVisual, animate: true);
                    }
                }
                else
                {
                    RefreshVisualState(animate: true);
                }

                SliceClicked?.Invoke(this, new SliceClickEventArgs(sliceAction));
                e.Handled = true;
            };

            slice.MouseEnter += (_, _) =>
            {
                if (_interactionMode != InteractionMode.Mouse || _drag != null)
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

                if (_interactionMode == InteractionMode.Mouse && _drag == null)
                {
                    ApplySliceRestingVisual(sliceVisual, animate: true);
                }

                if (!isMouseDown)
                {
                    return;
                }

                isMouseDown = false;
                _animationService.AnimateClickUp(slice, pressDuration, _renderState.StandardEasing);
            };
            // Hidden actions render dimmed in edit mode so they can still be selected and edited.
            if (IsEditMode && !sliceAction.IsEnabled)
            {
                slice.Opacity = 0.45;
            }

            Panel.SetZIndex(slice, SliceZIndex);
            PieCanvas.Children.Add(slice);

            // Digit hints only matter for triggering slices from the live menu.
            if (!IsEditMode && i < MaxDigitHints)
            {
                var digitHint = PieVisualBuilder.CreateSliceDigitHint(
                    i + 1,
                    theme.LabelTextStyle,
                    theme.LabelTextColor,
                    theme.IsHighContrast);
                digitHint.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var hintPosition = PieLayoutCalculator.GetTextPosition(center, outerRadius * DigitHintRadiusRatio, startAngle, endAngle, SnapPoint);
                Canvas.SetLeft(digitHint, SnapToDevicePixel(hintPosition.X - (digitHint.DesiredSize.Width / 2), isXAxis: true));
                Canvas.SetTop(digitHint, SnapToDevicePixel(hintPosition.Y - (digitHint.DesiredSize.Height / 2), isXAxis: false));
                Panel.SetZIndex(digitHint, SliceContentZIndex);
                PieCanvas.Children.Add(digitHint);
            }

            var textRadius = innerRadius > 0 ? (outerRadius + innerRadius) / 2 : outerRadius * 0.6;
            var textPosition = PieLayoutCalculator.GetTextPosition(center, textRadius, startAngle, endAngle, SnapPoint);

            var contentPanel = PieVisualBuilder.CreateSliceContentPanel(
                sliceAction,
                theme.IconTextStyle,
                theme.LabelTextStyle,
                theme.IconTextColor,
                theme.LabelTextColor,
                theme.IconToLabelSpacing,
                outerRadius,
                theme.ContentMaxWidthRatio,
                theme.ContentPadding);
            if (contentPanel == null)
            {
                continue;
            }

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

            if (IsEditMode && !sliceAction.IsEnabled)
            {
                contentPanel.Opacity = 0.45;
            }

            Panel.SetZIndex(contentPanel, SliceContentZIndex);
            PieCanvas.Children.Add(contentPanel);
        }

        if (IsEditMode)
        {
            AddGhostSlice(theme, center, outerRadius, innerRadius, visibleSlices.Count, angleStep);
        }

        _selectionController.EnsureSelectionIsValid(GetSelectionItems());

        // The rendered slices back the automation tree in edit mode, so rebuilds must invalidate the cached child peers.
        if (IsEditMode && UIElementAutomationPeer.FromElement(this) is PieControlAutomationPeer peer)
        {
            peer.ResetChildrenCache();
        }

        Log.Debug(
            "Pie menu rendered with {SliceCount} slices at {CanvasSize}px",
            _sliceVisuals.Count,
            canvasSize);
        RefreshVisualState(animate: false);
    }

    /// <summary>
    /// Adds the dashed "+" slice that follows the real slices in edit mode and adds a new action when clicked.
    /// </summary>
    private void AddGhostSlice(PieThemeSnapshot theme, Point center, double outerRadius, double innerRadius, int slotIndex, double angleStep)
    {
        var startAngle = (slotIndex * angleStep) - 90;
        var endAngle = startAngle + angleStep;

        // A full-circle arc degenerates (start and end coincide), so an empty editor gets a ring instead of a slice.
        var ghost = angleStep >= 360
            ? new Path
            {
                Data = new CombinedGeometry(
                    GeometryCombineMode.Exclude,
                    new EllipseGeometry(center, outerRadius, outerRadius),
                    new EllipseGeometry(center, innerRadius, innerRadius)),
            }
            : PieLayoutCalculator.CreateSlice(
                center,
                outerRadius,
                innerRadius,
                startAngle,
                endAngle,
                (centerPoint, radius, angle) => PieLayoutCalculator.GetPointOnCircle(centerPoint, radius, angle, SnapPoint));
        if (theme.SlicePathStyle != null)
        {
            ghost.Style = theme.SlicePathStyle;
        }

        var transparentFill = Color.FromArgb(0, theme.HoverColor.R, theme.HoverColor.G, theme.HoverColor.B);
        var fillBrush = new SolidColorBrush(transparentFill);
        var strokeBrush = new SolidColorBrush(theme.BorderColor);

        ghost.Fill = fillBrush;
        ghost.Stroke = strokeBrush;
        ghost.StrokeThickness = theme.SliceStrokeThickness;
        ghost.StrokeDashArray = [4, 3];
        ghost.Cursor = Cursors.Hand;
        ghost.SnapsToDevicePixels = true;
        ghost.ToolTip = "Add an action";

        var pathBounds = ghost.Data.Bounds;
        ghost.RenderTransform = new ScaleTransform(1, 1)
        {
            CenterX = pathBounds.X + (pathBounds.Width / 2),
            CenterY = pathBounds.Y + (pathBounds.Height / 2),
        };

        var pressDuration = _renderState.PressDuration;
        var isMouseDown = false;

        ghost.MouseEnter += (_, _) =>
        {
            _animationService.AnimateBrushColor(fillBrush, theme.HoverColor, _renderState.HoverDuration, _renderState.StandardEasing);
            _animationService.AnimateBrushColor(strokeBrush, theme.AccentColor, _renderState.HoverDuration, _renderState.StandardEasing);
        };

        ghost.MouseLeave += (_, _) =>
        {
            if (isMouseDown)
            {
                isMouseDown = false;
                _animationService.AnimateClickUp(ghost, pressDuration, _renderState.StandardEasing);
            }

            _animationService.AnimateBrushColor(fillBrush, transparentFill, _renderState.HoverDuration, _renderState.StandardEasing);
            _animationService.AnimateBrushColor(strokeBrush, theme.BorderColor, _renderState.HoverDuration, _renderState.StandardEasing);
        };

        ghost.MouseLeftButtonDown += (_, e) =>
        {
            Focus();
            isMouseDown = true;
            _animationService.AnimateClickDown(ghost, pressDuration, _renderState.StandardEasing);
            e.Handled = true;
        };

        ghost.MouseLeftButtonUp += (_, e) =>
        {
            if (!isMouseDown)
            {
                return;
            }

            isMouseDown = false;
            _animationService.AnimateClickUp(ghost, pressDuration, _renderState.StandardEasing);
            AddSliceRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        };

        _ghostPath = ghost;
        Panel.SetZIndex(ghost, SliceZIndex);
        PieCanvas.Children.Add(ghost);

        var textRadius = innerRadius > 0 ? (outerRadius + innerRadius) / 2 : outerRadius * 0.6;
        var glyphPosition = PieLayoutCalculator.GetTextPosition(center, textRadius, startAngle, endAngle, SnapPoint);

        var addGlyph = new TextBlock
        {
            Style = theme.IconTextStyle,
            Text = "\uE710",
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            Foreground = new SolidColorBrush(theme.AccentColor),
            FontSize = Math.Max(16, outerRadius * 0.1),
            IsHitTestVisible = false,
            SnapsToDevicePixels = true,
        };

        addGlyph.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(addGlyph, SnapToDevicePixel(glyphPosition.X - (addGlyph.DesiredSize.Width / 2), isXAxis: true));
        Canvas.SetTop(addGlyph, SnapToDevicePixel(glyphPosition.Y - (addGlyph.DesiredSize.Height / 2), isXAxis: false));
        Panel.SetZIndex(addGlyph, SliceContentZIndex);
        PieCanvas.Children.Add(addGlyph);
    }

    private void WireCenterInteractions(PieVisualBuilder.CenterElements centerElements, Duration pressDuration)
    {
        var isCenterMouseDown = false;

        centerElements.Target.MouseEnter += (_, _) =>
        {
            if (_interactionMode != InteractionMode.Mouse)
            {
                return;
            }

            ApplyCenterHoverVisual(animate: true);
        };

        centerElements.Target.MouseLeave += (_, _) =>
        {
            if (_interactionMode == InteractionMode.Mouse)
            {
                ApplyCenterNormalVisual(animate: true);
            }

            if (!isCenterMouseDown)
            {
                return;
            }

            isCenterMouseDown = false;
            _animationService.AnimateClickUp(centerElements.Target, pressDuration, _renderState.StandardEasing);
        };

        centerElements.Target.MouseLeftButtonDown += (_, e) =>
        {
            EnterMouseInteractionMode(refreshVisualState: false, animate: false);
            isCenterMouseDown = true;
            _animationService.AnimateClickDown(centerElements.Target, pressDuration, _renderState.StandardEasing);
            e.Handled = true;
        };

        centerElements.Target.MouseLeftButtonUp += (_, e) =>
        {
            if (!isCenterMouseDown)
            {
                return;
            }

            isCenterMouseDown = false;
            _animationService.AnimateClickUp(centerElements.Target, pressDuration, _renderState.StandardEasing);
            if (_interactionMode == InteractionMode.Mouse)
            {
                if (centerElements.Target.IsMouseOver)
                {
                    ApplyCenterHoverVisual(animate: true);
                }
                else
                {
                    ApplyCenterNormalVisual(animate: true);
                }
            }

            CenterClicked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        };

        centerElements.Target.MouseRightButtonUp += (_, e) =>
        {
            EnterMouseInteractionMode(refreshVisualState: false, animate: false);
            CenterContextMenuRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        };
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

        // Wrap over every layout slot (including the edit-mode ghost) so the angle math matches the geometry, then land ghost-slot drops on the last real slot.
        var targetSlot = Math.Min(
            PieReorderCalculator.GetTargetSlot(
                drag.Slice.Index,
                drag.RotationOffset,
                _layoutAngleStep,
                _layoutSlotCount),
            _sliceVisuals.Count - 1);
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

        Log.Debug("Slice drag canceled because mouse capture was lost: {SliceName}", sliceVisual.Action.Name);
        _drag = null;
        _dragCandidate = null;
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
        _animationService.AnimateRotationAngle(sliceVisual.PathRotation, toAngle, ReorderDuration, _renderState.StandardEasing, onCompleted);
        if (sliceVisual.ContentOrbit != null)
        {
            _animationService.AnimateRotationAngle(sliceVisual.ContentOrbit, toAngle, ReorderDuration, _renderState.StandardEasing);
            _animationService.AnimateRotationAngle(sliceVisual.ContentCounter, -toAngle, ReorderDuration, _renderState.StandardEasing);
        }
    }

    private ContextMenu CreateSliceContextMenu(PieAction sliceAction)
    {
        var contextMenu = new ContextMenu();
        var editMenuItem = new MenuItem { Header = "Edit..." };
        editMenuItem.Click += (_, _) => SliceEditRequested?.Invoke(this, new SliceClickEventArgs(sliceAction));
        contextMenu.Items.Add(editMenuItem);
        return contextMenu;
    }

    private void RefreshVisualState(bool animate)
    {
        foreach (var sliceVisual in _sliceVisuals)
        {
            var isSelectedOrHovered = _interactionMode == InteractionMode.Keyboard
                ? _selectionController.SelectedIndex == sliceVisual.Index
                : sliceVisual.Path.IsMouseOver;

            if (isSelectedOrHovered)
            {
                ApplySliceHoverVisual(sliceVisual, animate);
            }
            else
            {
                ApplySliceRestingVisual(sliceVisual, animate);
            }
        }

        if (_centerVisual == null)
        {
            return;
        }

        var isCenterHovered = !IsEditMode && _interactionMode == InteractionMode.Mouse && _centerVisual.Target.IsMouseOver;
        if (isCenterHovered)
        {
            ApplyCenterHoverVisual(animate);
        }
        else
        {
            ApplyCenterNormalVisual(animate);
        }
    }

    /// <summary>
    /// Applies the visual a slice returns to when not hovered: the accent selection highlight in edit mode when it is the selected slice, otherwise the normal resting look.
    /// </summary>
    private void ApplySliceRestingVisual(PieSliceVisual sliceVisual, bool animate)
    {
        if (IsSelectedInEditMode(sliceVisual))
        {
            ApplySliceSelectedVisual(sliceVisual, animate);
        }
        else
        {
            ApplySliceNormalVisual(sliceVisual, animate);
        }
    }

    private void ApplySliceNormalVisual(PieSliceVisual sliceVisual, bool animate)
    {
        Panel.SetZIndex(sliceVisual.Path, SliceZIndex);
        sliceVisual.Path.StrokeThickness = _renderState.SliceStrokeThickness;
        _animationService.ApplyBrushColor(sliceVisual.FillBrush, _renderState.SliceColor, animate, _renderState.HoverDuration, _renderState.StandardEasing);
        _animationService.ApplyBrushColor(sliceVisual.StrokeBrush, _renderState.BorderColor, animate, _renderState.HoverDuration, _renderState.StandardEasing);
    }

    private void ApplySliceSelectedVisual(PieSliceVisual sliceVisual, bool animate)
    {
        // Raised above neighbors so the accent stroke is not painted over by their edges.
        Panel.SetZIndex(sliceVisual.Path, SelectedSliceZIndex);
        sliceVisual.Path.StrokeThickness = _renderState.SliceStrokeThickness + 1;
        _animationService.ApplyBrushColor(sliceVisual.FillBrush, _renderState.SelectedFillColor, animate, _renderState.HoverDuration, _renderState.StandardEasing);
        _animationService.ApplyBrushColor(sliceVisual.StrokeBrush, _renderState.AccentColor, animate, _renderState.HoverDuration, _renderState.StandardEasing);
    }

    private void ApplySliceHoverVisual(PieSliceVisual sliceVisual, bool animate)
    {
        Panel.SetZIndex(sliceVisual.Path, HoveredSliceZIndex);
        sliceVisual.Path.StrokeThickness = IsSelectedInEditMode(sliceVisual)
            ? _renderState.SliceStrokeThickness + 1
            : _renderState.SliceStrokeThickness;
        _animationService.ApplyBrushColor(sliceVisual.FillBrush, _renderState.HoverColor, animate, _renderState.HoverDuration, _renderState.StandardEasing);
        _animationService.ApplyBrushColor(sliceVisual.StrokeBrush, _renderState.BorderHoverColor, animate, _renderState.HoverDuration, _renderState.StandardEasing);
    }

    private bool IsSelectedInEditMode(PieSliceVisual sliceVisual)
    {
        return IsEditMode && SelectedSlice != null && sliceVisual.Action == SelectedSlice;
    }

    private void ApplyCenterNormalVisual(bool animate)
    {
        var centerVisual = _centerVisual;
        if (centerVisual == null)
        {
            return;
        }

        ApplyBrushColor(centerVisual.FillBrush, _renderState.HubColor, animate);
        ApplyBrushColor(centerVisual.StrokeBrush, _renderState.HubBorderColor, animate);

        if (animate)
        {
            _animationService.AnimateOpacity(centerVisual.Icon, 0, _renderState.HoverDuration, _renderState.StandardEasing, () =>
            {
                if (!centerVisual.Target.IsMouseOver || _interactionMode == InteractionMode.Keyboard)
                {
                    centerVisual.Icon.Visibility = Visibility.Collapsed;
                }
            });
        }
        else
        {
            centerVisual.Icon.BeginAnimation(UIElement.OpacityProperty, null);
            centerVisual.Icon.Opacity = 0;
            centerVisual.Icon.Visibility = Visibility.Collapsed;
        }
    }

    private void ApplyCenterHoverVisual(bool animate)
    {
        var centerVisual = _centerVisual;
        if (centerVisual == null)
        {
            return;
        }

        ApplyBrushColor(centerVisual.FillBrush, _renderState.HubHoverColor, animate);
        ApplyBrushColor(centerVisual.StrokeBrush, _renderState.CenterHoverBorderColor, animate);
        centerVisual.Icon.Visibility = Visibility.Visible;

        if (animate)
        {
            _animationService.AnimateOpacity(centerVisual.Icon, 1, _renderState.HoverDuration, _renderState.StandardEasing);
        }
        else
        {
            centerVisual.Icon.BeginAnimation(UIElement.OpacityProperty, null);
            centerVisual.Icon.Opacity = 1;
        }
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

        if (!IsLoaded || _renderRefreshQueued)
        {
            return;
        }

        _renderRefreshQueued = true;

        Dispatcher.InvokeAsync(() =>
        {
            _renderRefreshQueued = false;

            if (!IsLoaded || !IsVisible)
            {
                Log.Debug("Skipping render refresh because PieControl is not ready (IsLoaded={IsLoaded}, IsVisible={IsVisible})", IsLoaded, IsVisible);
                return;
            }

            CreatePieMenu();

            // Render priority so the rebuild lands before the next frame is presented. At Background the menu could fade in
            // while the pie was still missing, making it pop in partway through the animation.
        }, DispatcherPriority.Render);
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
        var dpiInfo = VisualTreeHelper.GetDpi(this);
        var scale = isXAxis ? dpiInfo.DpiScaleX : dpiInfo.DpiScaleY;
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

    /// <summary>
    /// Occurs when the ghost add slice is clicked in edit mode.
    /// </summary>
    public event EventHandler AddSliceRequested;

    /// <summary>
    /// Occurs when removal of the selected slice is requested by keyboard in edit mode.
    /// </summary>
    public event EventHandler<SliceClickEventArgs> RemoveSliceRequested;
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
