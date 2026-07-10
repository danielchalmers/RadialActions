using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
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
    private PieSliceVisual _dragCandidate;
    private Point _dragPressPosition;
    private DragReorderState _drag;
    private bool _isReleasingDragCapture;

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
            Log.Debug("PieControl hidden; deferring render refresh");
            _renderRefreshPending = true;
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
        EnterMouseInteractionMode(refreshVisualState: true, animate: false);
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
        if (selectedSlice == null)
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
        const int centerZIndex = 20;

        PieCanvas.Children.Clear();
        _sliceVisuals.Clear();
        _centerVisual = null;
        _renderRefreshPending = false;
        _drag = null;
        _dragCandidate = null;

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

            Canvas.SetLeft(centerElements.Target, SnapToDevicePixel(center.X - innerRadius, isXAxis: true));
            Canvas.SetTop(centerElements.Target, SnapToDevicePixel(center.Y - innerRadius, isXAxis: false));
            Panel.SetZIndex(centerElements.Target, centerZIndex);
            PieCanvas.Children.Add(centerElements.Target);
        }

        for (var i = 0; i < enabledSlices.Count; i++)
        {
            var sliceAction = enabledSlices[i];
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

            var contextMenu = CreateSliceContextMenu(sliceAction);
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
                    ApplySliceNormalVisual(sliceVisual, animate: true);
                }

                if (!isMouseDown)
                {
                    return;
                }

                isMouseDown = false;
                _animationService.AnimateClickUp(slice, pressDuration, _renderState.StandardEasing);
            };
            Panel.SetZIndex(slice, SliceZIndex);
            PieCanvas.Children.Add(slice);

            if (i < MaxDigitHints)
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

            Panel.SetZIndex(contentPanel, SliceContentZIndex);
            PieCanvas.Children.Add(contentPanel);
        }

        _selectionController.EnsureSelectionIsValid(GetSelectionItems());

        Log.Debug(
            "Pie menu rendered with {SliceCount} slices at {CanvasSize}px",
            _sliceVisuals.Count,
            canvasSize);
        RefreshVisualState(animate: false);
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
            if (sliceVisual.ContentPanel != null)
            {
                Panel.SetZIndex(sliceVisual.ContentPanel, SliceContentZIndex);
            }

            RefreshVisualState(animate: true);
            return;
        }

        // Move the dragged action to the position of the action that was built at the target
        // slot; disabled actions keep their relative placement in the collection.
        var targetAction = _sliceVisuals.First(visual => visual.Index == targetSlot).Action;
        var fromIndex = Slices.IndexOf(sliceVisual.Action);
        var toIndex = Slices.IndexOf(targetAction);
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
                ApplySliceNormalVisual(sliceVisual, animate);
            }
        }

        if (_centerVisual == null)
        {
            return;
        }

        var isCenterHovered = _interactionMode == InteractionMode.Mouse && _centerVisual.Target.IsMouseOver;
        if (isCenterHovered)
        {
            ApplyCenterHoverVisual(animate);
        }
        else
        {
            ApplyCenterNormalVisual(animate);
        }
    }

    private void ApplySliceNormalVisual(PieSliceVisual sliceVisual, bool animate)
    {
        Panel.SetZIndex(sliceVisual.Path, SliceZIndex);
        _animationService.ApplyBrushColor(sliceVisual.FillBrush, _renderState.SliceColor, animate, _renderState.HoverDuration, _renderState.StandardEasing);
        _animationService.ApplyBrushColor(sliceVisual.StrokeBrush, _renderState.BorderColor, animate, _renderState.HoverDuration, _renderState.StandardEasing);
    }

    private void ApplySliceHoverVisual(PieSliceVisual sliceVisual, bool animate)
    {
        Panel.SetZIndex(sliceVisual.Path, HoveredSliceZIndex);
        _animationService.ApplyBrushColor(sliceVisual.FillBrush, _renderState.HoverColor, animate, _renderState.HoverDuration, _renderState.StandardEasing);
        _animationService.ApplyBrushColor(sliceVisual.StrokeBrush, _renderState.BorderHoverColor, animate, _renderState.HoverDuration, _renderState.StandardEasing);
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
        }, DispatcherPriority.Background);
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
