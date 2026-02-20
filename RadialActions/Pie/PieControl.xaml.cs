using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
    private const int NoSelectedSliceIndex = -1;
    private const int SliceZIndex = 10;
    private const int HoveredSliceZIndex = 14;
    private const double MouseMoveThreshold = 0.25;

    private enum InteractionMode
    {
        Mouse,
        Keyboard,
    }

    private sealed class SliceVisual
    {
        public required int Index { get; init; }
        public required double MidAngle { get; init; }
        public required PieAction Action { get; init; }
        public required Path Path { get; init; }
        public required SolidColorBrush FillBrush { get; init; }
        public required SolidColorBrush StrokeBrush { get; init; }
        public required ContextMenu ContextMenu { get; init; }
    }

    private sealed class CenterVisual
    {
        public required Grid Target { get; init; }
        public required SolidColorBrush FillBrush { get; init; }
        public required SolidColorBrush StrokeBrush { get; init; }
        public required TextBlock Icon { get; init; }
    }

    private bool _renderRefreshPending;
    private bool _renderRefreshQueued;
    private readonly List<SliceVisual> _sliceVisuals = [];
    private CenterVisual _centerVisual;
    private readonly PieRenderState _renderState = new();
    private InteractionMode _interactionMode = InteractionMode.Mouse;
    private int _selectedSliceIndex = NoSelectedSliceIndex;
    private Point _keyboardModeMousePosition;
    private bool _hasKeyboardModeMousePosition;

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
            default:
                return false;
        }
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
        if (_sliceVisuals.Count == 0)
        {
            return;
        }

        if (_selectedSliceIndex == NoSelectedSliceIndex)
        {
            _selectedSliceIndex = key switch
            {
                Key.Up => GetSliceIndexClosestToAngle(-90),
                Key.Right => GetSliceIndexClosestToAngle(0),
                Key.Down => GetSliceIndexClosestToAngle(90),
                Key.Left => GetSliceIndexClosestToAngle(180),
                _ => NoSelectedSliceIndex
            };
        }
        else if (key is Key.Right or Key.Down)
        {
            _selectedSliceIndex = (_selectedSliceIndex + 1) % _sliceVisuals.Count;
        }
        else if (key is Key.Left or Key.Up)
        {
            _selectedSliceIndex = (_selectedSliceIndex - 1 + _sliceVisuals.Count) % _sliceVisuals.Count;
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

    private SliceVisual GetSelectedSliceVisual()
    {
        if (_selectedSliceIndex == NoSelectedSliceIndex)
        {
            return null;
        }

        return _sliceVisuals.FirstOrDefault(slice => slice.Index == _selectedSliceIndex);
    }

    private int GetSliceIndexClosestToAngle(double targetAngle)
    {
        if (_sliceVisuals.Count == 0)
        {
            return 0;
        }

        var bestIndex = 0;
        var bestDistance = double.MaxValue;

        foreach (var sliceVisual in _sliceVisuals)
        {
            var distance = Math.Abs(PieLayoutCalculator.NormalizeSignedAngle(sliceVisual.MidAngle - targetAngle));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = sliceVisual.Index;
            }
        }

        return bestIndex;
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
        const int contentZIndex = 15;
        const int centerZIndex = 20;

        PieCanvas.Children.Clear();
        _sliceVisuals.Clear();
        _centerVisual = null;
        _renderRefreshPending = false;

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
            _selectedSliceIndex = NoSelectedSliceIndex;
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

            var centerVisual = new CenterVisual
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
                AnimateSliceClickUp(centerElements.Target, pressDuration, _renderState.StandardEasing);
            };

            centerElements.Target.MouseLeftButtonDown += (_, e) =>
            {
                EnterMouseInteractionMode(refreshVisualState: false, animate: false);
                isCenterMouseDown = true;
                AnimateSliceClickDown(centerElements.Target, pressDuration, _renderState.StandardEasing);
                e.Handled = true;
            };

            centerElements.Target.MouseLeftButtonUp += (_, e) =>
            {
                if (!isCenterMouseDown)
                {
                    return;
                }

                isCenterMouseDown = false;
                AnimateSliceClickUp(centerElements.Target, pressDuration, _renderState.StandardEasing);
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

            var contextMenu = CreateSliceContextMenu(sliceAction);
            slice.ContextMenu = contextMenu;

            var sliceVisual = new SliceVisual
            {
                Index = i,
                MidAngle = (startAngle + endAngle) / 2,
                Action = sliceAction,
                Path = slice,
                FillBrush = fillBrush,
                StrokeBrush = strokeBrush,
                ContextMenu = contextMenu,
            };
            _sliceVisuals.Add(sliceVisual);

            var isMouseDown = false;

            slice.MouseLeftButtonDown += (_, e) =>
            {
                EnterMouseInteractionMode(refreshVisualState: false, animate: false);
                isMouseDown = true;
                AnimateSliceColor(fillBrush, theme.PressedColor, pressDuration, _renderState.StandardEasing);
                AnimateSliceColor(strokeBrush, _renderState.BorderHoverColor, pressDuration, _renderState.StandardEasing);
                AnimateSliceClickDown(slice, pressDuration, _renderState.StandardEasing);
                e.Handled = true;
            };

            slice.MouseLeftButtonUp += (_, e) =>
            {
                if (!isMouseDown)
                {
                    return;
                }

                isMouseDown = false;
                AnimateSliceClickUp(slice, pressDuration, _renderState.StandardEasing);
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
                if (_interactionMode != InteractionMode.Mouse)
                {
                    return;
                }

                ApplySliceHoverVisual(sliceVisual, animate: true);
            };

            slice.MouseLeave += (_, _) =>
            {
                if (_interactionMode == InteractionMode.Mouse)
                {
                    ApplySliceNormalVisual(sliceVisual, animate: true);
                }

                if (!isMouseDown)
                {
                    return;
                }

                isMouseDown = false;
                AnimateSliceClickUp(slice, pressDuration, _renderState.StandardEasing);
            };
            Panel.SetZIndex(slice, SliceZIndex);
            PieCanvas.Children.Add(slice);

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

            Canvas.SetLeft(contentPanel, SnapToDevicePixel(textPosition.X - (contentSize.Width / 2), isXAxis: true));
            Canvas.SetTop(contentPanel, SnapToDevicePixel(textPosition.Y - (contentSize.Height / 2), isXAxis: false));

            Panel.SetZIndex(contentPanel, contentZIndex);
            PieCanvas.Children.Add(contentPanel);
        }

        if (_selectedSliceIndex != NoSelectedSliceIndex
            && _sliceVisuals.All(sliceVisual => sliceVisual.Index != _selectedSliceIndex))
        {
            _selectedSliceIndex = NoSelectedSliceIndex;
        }

        Log.Debug(
            "Pie menu rendered with {SliceCount} slices at {CanvasSize}px",
            _sliceVisuals.Count,
            canvasSize);
        RefreshVisualState(animate: false);
    }

    private void EnterMouseInteractionMode(bool refreshVisualState, bool animate)
    {
        _interactionMode = InteractionMode.Mouse;
        _selectedSliceIndex = NoSelectedSliceIndex;
        _hasKeyboardModeMousePosition = false;

        if (refreshVisualState)
        {
            RefreshVisualState(animate);
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
                ? _selectedSliceIndex == sliceVisual.Index
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

    private void ApplySliceNormalVisual(SliceVisual sliceVisual, bool animate)
    {
        Panel.SetZIndex(sliceVisual.Path, SliceZIndex);
        ApplyBrushColor(sliceVisual.FillBrush, _renderState.SliceColor, animate);
        ApplyBrushColor(sliceVisual.StrokeBrush, _renderState.BorderColor, animate);
    }

    private void ApplySliceHoverVisual(SliceVisual sliceVisual, bool animate)
    {
        Panel.SetZIndex(sliceVisual.Path, HoveredSliceZIndex);
        ApplyBrushColor(sliceVisual.FillBrush, _renderState.HoverColor, animate);
        ApplyBrushColor(sliceVisual.StrokeBrush, _renderState.BorderHoverColor, animate);
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
            AnimateOpacity(centerVisual.Icon, 0, _renderState.HoverDuration, _renderState.StandardEasing, () =>
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
            AnimateOpacity(centerVisual.Icon, 1, _renderState.HoverDuration, _renderState.StandardEasing);
        }
        else
        {
            centerVisual.Icon.BeginAnimation(UIElement.OpacityProperty, null);
            centerVisual.Icon.Opacity = 1;
        }
    }

    private void ApplyBrushColor(SolidColorBrush brush, Color color, bool animate)
    {
        if (animate)
        {
            AnimateSliceColor(brush, color, _renderState.HoverDuration, _renderState.StandardEasing);
            return;
        }

        brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
        brush.Color = color;
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

    private void AnimateSliceColor(
        SolidColorBrush brush,
        Color toColor,
        Duration duration,
        IEasingFunction easingFunction)
    {
        if (IsReducedMotionEnabled())
        {
            brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
            brush.Color = toColor;
            return;
        }

        var colorAnimation = new ColorAnimation
        {
            To = toColor,
            Duration = duration,
            EasingFunction = easingFunction,
        };

        brush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    private void AnimateOpacity(
        UIElement element,
        double toOpacity,
        Duration duration,
        IEasingFunction easingFunction,
        Action onCompleted = null)
    {
        if (IsReducedMotionEnabled())
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = toOpacity;
            onCompleted?.Invoke();
            return;
        }

        var opacityAnimation = new DoubleAnimation
        {
            To = toOpacity,
            Duration = duration,
            EasingFunction = easingFunction,
        };

        if (onCompleted != null)
        {
            opacityAnimation.Completed += (_, _) => onCompleted();
        }

        element.BeginAnimation(UIElement.OpacityProperty, opacityAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    private void AnimateSliceClickDown(UIElement target, Duration duration, IEasingFunction easingFunction)
    {
        if (target.RenderTransform is not ScaleTransform scaleTransform)
        {
            scaleTransform = new ScaleTransform(1, 1);
            target.RenderTransform = scaleTransform;
            target.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        if (IsReducedMotionEnabled())
        {
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            scaleTransform.ScaleX = 0.95;
            scaleTransform.ScaleY = 0.95;
            return;
        }

        var scaleAnimation = new DoubleAnimation
        {
            To = 0.95,
            Duration = duration,
            EasingFunction = easingFunction,
            FillBehavior = FillBehavior.HoldEnd,
        };

        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation, HandoffBehavior.SnapshotAndReplace);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    private void AnimateSliceClickUp(UIElement target, Duration duration, IEasingFunction easingFunction)
    {
        if (target.RenderTransform is not ScaleTransform scaleTransform)
        {
            return;
        }

        if (IsReducedMotionEnabled())
        {
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            scaleTransform.ScaleX = 1;
            scaleTransform.ScaleY = 1;
            return;
        }

        var scaleAnimation = new DoubleAnimation
        {
            To = 1,
            Duration = duration,
            EasingFunction = easingFunction,
        };

        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation, HandoffBehavior.SnapshotAndReplace);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    private static bool IsReducedMotionEnabled()
    {
        return !SystemParameters.ClientAreaAnimation;
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
