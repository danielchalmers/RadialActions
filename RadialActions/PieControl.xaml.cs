using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
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

    private bool _themeRefreshPending;
    private bool _themeRefreshQueued;
    private readonly List<SliceVisual> _sliceVisuals = [];
    private CenterVisual? _centerVisual;
    private InteractionMode _interactionMode = InteractionMode.Mouse;
    private int? _selectedSliceIndex;
    private Point? _keyboardModeMousePosition;
    private Duration _hoverDuration = new(TimeSpan.FromMilliseconds(100));
    private IEasingFunction _standardEasing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
    private Color _sliceColor = SystemColors.ControlColor;
    private Color _hoverColor = SystemColors.ControlLightColor;
    private Color _borderColor = SystemColors.ControlDarkColor;
    private Color _borderHoverColor = SystemColors.ControlDarkColor;
    private Color _hubColor = SystemColors.ControlColor;
    private Color _hubHoverColor = SystemColors.ControlLightColor;
    private Color _hubBorderColor = SystemColors.ControlDarkColor;
    private Color _centerHoverBorderColor = SystemColors.ControlDarkColor;

    public PieControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
        SizeChanged += (_, _) => CreatePieMenu();
        PieCanvas.MouseMove += OnPieCanvasMouseMove;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        QueueThemeRefresh();
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
            _themeRefreshPending = true;
            return;
        }

        if (_themeRefreshPending)
        {
            CreatePieMenu();
        }
    }

    public void ResetInputState()
    {
        _interactionMode = InteractionMode.Mouse;
        _selectedSliceIndex = null;
        _keyboardModeMousePosition = null;
        RefreshVisualState(animate: false);
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
                OpenSelectedSliceContextMenu();
                return true;
            case Key.F10 when modifiers.HasFlag(ModifierKeys.Shift):
                OpenSelectedSliceContextMenu();
                return true;
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
        if (_keyboardModeMousePosition is Point keyboardPosition
            && Math.Abs(position.X - keyboardPosition.X) <= MouseMoveThreshold
            && Math.Abs(position.Y - keyboardPosition.Y) <= MouseMoveThreshold)
        {
            return;
        }

        _interactionMode = InteractionMode.Mouse;
        _selectedSliceIndex = null;
        _keyboardModeMousePosition = null;
        RefreshVisualState(animate: true);
    }

    private void HandleArrowKey(Key key)
    {
        if (_sliceVisuals.Count == 0)
        {
            return;
        }

        if (_selectedSliceIndex is null)
        {
            _selectedSliceIndex = key switch
            {
                Key.Up => GetSliceIndexClosestToAngle(-90),
                Key.Right => GetSliceIndexClosestToAngle(0),
                Key.Down => GetSliceIndexClosestToAngle(90),
                Key.Left => GetSliceIndexClosestToAngle(180),
                _ => _selectedSliceIndex
            };
        }
        else if (key is Key.Right or Key.Down)
        {
            _selectedSliceIndex = (_selectedSliceIndex.Value + 1) % _sliceVisuals.Count;
        }
        else if (key is Key.Left or Key.Up)
        {
            _selectedSliceIndex = ((_selectedSliceIndex.Value - 1) + _sliceVisuals.Count) % _sliceVisuals.Count;
        }

        _interactionMode = InteractionMode.Keyboard;
        _keyboardModeMousePosition = Mouse.GetPosition(PieCanvas);
        RefreshVisualState(animate: true);
    }

    private void ActivateSelectedSlice()
    {
        if (TryGetSelectedSliceVisual(out var selectedSlice))
        {
            SliceClicked?.Invoke(this, new SliceClickEventArgs(selectedSlice.Action));
        }
    }

    private void OpenSelectedSliceContextMenu()
    {
        if (!TryGetSelectedSliceVisual(out var selectedSlice))
        {
            return;
        }

        selectedSlice.ContextMenu.PlacementTarget = selectedSlice.Path;
        selectedSlice.ContextMenu.Placement = PlacementMode.Center;
        selectedSlice.ContextMenu.IsOpen = true;
    }

    private bool TryGetSelectedSliceVisual(out SliceVisual selectedSlice)
    {
        selectedSlice = default!;

        if (_selectedSliceIndex is null)
        {
            return false;
        }

        selectedSlice = _sliceVisuals.FirstOrDefault(slice => slice.Index == _selectedSliceIndex.Value);
        return selectedSlice != null;
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
            var distance = Math.Abs(NormalizeSignedAngle(sliceVisual.MidAngle - targetAngle));
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

        control.CreatePieMenu();
    }

    private void OnSlicesCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (PieAction item in e.OldItems)
            {
                item.PropertyChanged -= OnSlicePropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (PieAction item in e.NewItems)
            {
                item.PropertyChanged += OnSlicePropertyChanged;
            }
        }

        CreatePieMenu();
    }

    private void OnSlicePropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        CreatePieMenu();
    }

    private void CreatePieMenu()
    {
        const int contentZIndex = 15;
        const int centerZIndex = 20;

        PieCanvas.Children.Clear();
        _sliceVisuals.Clear();
        _centerVisual = null;
        _themeRefreshPending = false;

        if (Slices == null || Slices.Count == 0 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            _selectedSliceIndex = null;
            return;
        }

        var canvasSize = SnapToDevicePixel(Math.Min(ActualWidth, ActualHeight), isXAxis: true);
        if (canvasSize <= 0)
        {
            return;
        }

        var canvasRadius = canvasSize / 2;
        var center = new Point(canvasRadius, canvasRadius);

        PieCanvas.Width = canvasSize;
        PieCanvas.Height = canvasSize;

        var isHighContrast = SystemParameters.HighContrast;

        var sliceStrokeThickness = Math.Max(1, GetDoubleResource("RadialMenuSliceStrokeThickness", 1.5));
        var hubStrokeThickness = Math.Max(1, GetDoubleResource("RadialMenuHubStrokeThickness", 1.5));
        var iconToLabelSpacing = Math.Max(0, GetDoubleResource("RadialMenuIconToLabelSpacing", 3));
        var contentMaxWidthRatio = Math.Clamp(GetDoubleResource("RadialMenuSliceContentMaxWidthRatio", 0.38), 0.2, 0.8);

        _hoverDuration = GetDurationResource("RadialMenuHoverDuration", new Duration(TimeSpan.FromMilliseconds(100)));
        var pressDuration = GetDurationResource("RadialMenuPressDuration", new Duration(TimeSpan.FromMilliseconds(75)));
        _standardEasing = GetEasingResource(
            "RadialMenuEaseStandard",
            new QuadraticEase { EasingMode = EasingMode.EaseOut });
        var slicePathStyle = TryFindResource("RadialMenuSlicePathStyle") as Style;
        var hubEllipseStyle = TryFindResource("RadialMenuHubEllipseStyle") as Style;
        var hubContainerStyle = TryFindResource("RadialMenuHubContainerStyle") as Style;
        var iconTextStyle = TryFindResource("RadialMenuIconTextStyle") as Style;
        var labelTextStyle = TryFindResource("RadialMenuLabelTextStyle") as Style;

        var accentColor = GetSystemAccentColor();
        var surfaceColor = GetBrushResource("RadialMenuSurfaceBrush", SystemColors.ControlColor).Color;
        var surfaceBorderColor = GetBrushResource("RadialMenuSurfaceBorderBrush", SystemColors.ControlDarkColor).Color;
        var sliceColor = GetBrushResource("RadialMenuSliceFillBrush", SystemColors.ControlColor).Color;
        var hoverColor = GetBrushResource("RadialMenuSliceHoverBrush", SystemColors.ControlLightColor).Color;
        var pressedColor = GetBrushResource("RadialMenuSlicePressedBrush", SystemColors.ControlDarkColor).Color;
        var borderColor = GetBrushResource("RadialMenuSliceBorderBrush", SystemColors.ControlDarkColor).Color;
        var hubColor = GetBrushResource("RadialMenuHubFillBrush", SystemColors.ControlColor).Color;
        var hubHoverColor = GetBrushResource("RadialMenuHubHoverBrush", SystemColors.ControlLightColor).Color;
        var hubBorderColor = GetBrushResource("RadialMenuHubBorderBrush", SystemColors.ControlDarkColor).Color;
        var iconTextColor = accentColor;
        var labelTextColor = GetBrushResource("RadialMenuTextBrush", SystemColors.WindowTextColor).Color;

        if (isHighContrast)
        {
            surfaceColor = SystemColors.WindowColor;
            surfaceBorderColor = SystemColors.WindowTextColor;
            sliceColor = SystemColors.WindowColor;
            hoverColor = SystemColors.HighlightColor;
            pressedColor = BlendColor(SystemColors.HighlightColor, SystemColors.WindowColor, 0.35);
            borderColor = SystemColors.WindowTextColor;
            hubColor = SystemColors.ControlColor;
            hubHoverColor = SystemColors.HighlightColor;
            hubBorderColor = SystemColors.WindowTextColor;
            iconTextColor = SystemColors.WindowTextColor;
            labelTextColor = SystemColors.WindowTextColor;
            accentColor = SystemColors.HighlightColor;
        }
        else
        {
            accentColor = GetAccessibleAccentColor(accentColor, sliceColor);
            iconTextColor = GetAccessibleAccentColor(iconTextColor, sliceColor);
            labelTextColor = GetAccessibleAccentColor(labelTextColor, sliceColor);
        }

        _sliceColor = sliceColor;
        _hoverColor = hoverColor;
        _borderColor = borderColor;
        _borderHoverColor = BlendColor(borderColor, accentColor, 0.40);
        _hubColor = hubColor;
        _hubHoverColor = hubHoverColor;
        _hubBorderColor = hubBorderColor;
        _centerHoverBorderColor = BlendColor(hubBorderColor, accentColor, 0.45);

        var innerRadius = canvasRadius * DefaultCenterHoleRatio;
        var outerRadius = Math.Max(0, canvasRadius - (sliceStrokeThickness / 2));

        AddSurfaceRing(center, outerRadius, innerRadius, surfaceColor, surfaceBorderColor, sliceStrokeThickness, isHighContrast);

        var angleStep = 360.0 / Slices.Count;

        if (innerRadius > 0)
        {
            var centerFillBrush = new SolidColorBrush(hubColor);
            var centerStrokeBrush = new SolidColorBrush(hubBorderColor);

            var centerHole = new Ellipse
            {
                Style = hubEllipseStyle,
                Width = innerRadius * 2,
                Height = innerRadius * 2,
                Fill = centerFillBrush,
                Stroke = centerStrokeBrush,
                StrokeThickness = hubStrokeThickness,
                Cursor = System.Windows.Input.Cursors.Hand,
                SnapsToDevicePixels = true,
            };

            var centerCloseIcon = new TextBlock
            {
                Style = iconTextStyle,
                Text = "\uE8BB",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Foreground = new SolidColorBrush(iconTextColor),
                FontSize = Math.Max(innerRadius * 0.40, 11),
                Margin = new Thickness(0, -1, 0, 0),
                Opacity = 0,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
            };

            var centerCloseTarget = new Grid
            {
                Style = hubContainerStyle,
                Width = innerRadius * 2,
                Height = innerRadius * 2,
                Cursor = System.Windows.Input.Cursors.Hand,
            };

            centerCloseTarget.Children.Add(centerHole);
            centerCloseTarget.Children.Add(centerCloseIcon);

            var centerVisual = new CenterVisual
            {
                Target = centerCloseTarget,
                FillBrush = centerFillBrush,
                StrokeBrush = centerStrokeBrush,
                Icon = centerCloseIcon,
            };
            _centerVisual = centerVisual;

            var isCenterMouseDown = false;

            centerCloseTarget.MouseEnter += (_, _) =>
            {
                if (_interactionMode != InteractionMode.Mouse)
                {
                    return;
                }

                ApplyCenterHoverVisual(animate: true);
            };

            centerCloseTarget.MouseLeave += (_, _) =>
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
                AnimateSliceClickUp(centerCloseTarget, pressDuration, _standardEasing);
            };

            centerCloseTarget.MouseLeftButtonDown += (_, e) =>
            {
                _interactionMode = InteractionMode.Mouse;
                _selectedSliceIndex = null;
                _keyboardModeMousePosition = null;
                isCenterMouseDown = true;
                AnimateSliceClickDown(centerCloseTarget, pressDuration, _standardEasing);
                e.Handled = true;
            };

            centerCloseTarget.MouseLeftButtonUp += (_, e) =>
            {
                if (!isCenterMouseDown)
                {
                    return;
                }

                isCenterMouseDown = false;
                AnimateSliceClickUp(centerCloseTarget, pressDuration, _standardEasing);
                if (_interactionMode == InteractionMode.Mouse)
                {
                    if (centerCloseTarget.IsMouseOver)
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

            Canvas.SetLeft(centerCloseTarget, SnapToDevicePixel(center.X - innerRadius, isXAxis: true));
            Canvas.SetTop(centerCloseTarget, SnapToDevicePixel(center.Y - innerRadius, isXAxis: false));
            Panel.SetZIndex(centerCloseTarget, centerZIndex);
            PieCanvas.Children.Add(centerCloseTarget);
        }

        for (var i = 0; i < Slices.Count; i++)
        {
            var sliceAction = Slices[i];
            var startAngle = (i * angleStep) - 90;
            var endAngle = startAngle + angleStep;

            var slice = CreateSlice(center, outerRadius, innerRadius, startAngle, endAngle);
            if (slicePathStyle != null)
            {
                slice.Style = slicePathStyle;
            }

            var fillBrush = new SolidColorBrush(sliceColor);
            var strokeBrush = new SolidColorBrush(borderColor);

            slice.Fill = fillBrush;
            slice.Stroke = strokeBrush;
            slice.StrokeThickness = sliceStrokeThickness;
            slice.Cursor = System.Windows.Input.Cursors.Hand;
            slice.SnapsToDevicePixels = true;

            var contextMenu = new ContextMenu();
            var editMenuItem = new MenuItem { Header = "Edit..." };
            editMenuItem.Click += (_, _) => SliceEditRequested?.Invoke(this, new SliceClickEventArgs(sliceAction));
            contextMenu.Items.Add(editMenuItem);
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
                _interactionMode = InteractionMode.Mouse;
                _selectedSliceIndex = null;
                _keyboardModeMousePosition = null;
                isMouseDown = true;
                AnimateSliceColor(fillBrush, pressedColor, pressDuration, _standardEasing);
                AnimateSliceColor(strokeBrush, _borderHoverColor, pressDuration, _standardEasing);
                AnimateSliceClickDown(slice, pressDuration, _standardEasing);
                e.Handled = true;
            };

            slice.MouseLeftButtonUp += (_, e) =>
            {
                if (!isMouseDown)
                {
                    return;
                }

                isMouseDown = false;
                AnimateSliceClickUp(slice, pressDuration, _standardEasing);
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
                AnimateSliceClickUp(slice, pressDuration, _standardEasing);
            };
            Panel.SetZIndex(slice, SliceZIndex);
            PieCanvas.Children.Add(slice);

            var textRadius = innerRadius > 0 ? (outerRadius + innerRadius) / 2 : outerRadius * 0.6;
            var textPosition = GetTextPosition(center, textRadius, startAngle, endAngle);

            var showIcon = !string.IsNullOrEmpty(sliceAction.Icon);
            if (!showIcon && string.IsNullOrWhiteSpace(sliceAction.Name))
            {
                continue;
            }

            var contentPadding = TryFindResource("RadialMenuSliceContentPadding") is Thickness padding
                ? padding
                : new Thickness(2);

            var contentPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = contentPadding,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true,
            };

            if (showIcon)
            {
                var iconText = new TextBlock
                {
                    Style = iconTextStyle,
                    Text = sliceAction.Icon,
                    Foreground = new SolidColorBrush(iconTextColor),
                    FontSize = 20,
                    Margin = new Thickness(0, 0, 0, iconToLabelSpacing),
                };

                contentPanel.Children.Add(iconText);
            }

            if (!string.IsNullOrWhiteSpace(sliceAction.Name))
            {
                var text = new TextBlock
                {
                    Style = labelTextStyle,
                    Text = sliceAction.Name,
                    Foreground = new SolidColorBrush(labelTextColor),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = Math.Max(56, outerRadius * contentMaxWidthRatio),
                };

                contentPanel.Children.Add(text);
            }

            contentPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var contentSize = contentPanel.DesiredSize;

            Canvas.SetLeft(contentPanel, SnapToDevicePixel(textPosition.X - (contentSize.Width / 2), isXAxis: true));
            Canvas.SetTop(contentPanel, SnapToDevicePixel(textPosition.Y - (contentSize.Height / 2), isXAxis: false));

            Panel.SetZIndex(contentPanel, contentZIndex);
            PieCanvas.Children.Add(contentPanel);
        }

        if (_selectedSliceIndex is int selectedIndex
            && _sliceVisuals.All(sliceVisual => sliceVisual.Index != selectedIndex))
        {
            _selectedSliceIndex = null;
        }

        RefreshVisualState(animate: false);
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
        ApplyBrushColor(sliceVisual.FillBrush, _sliceColor, animate);
        ApplyBrushColor(sliceVisual.StrokeBrush, _borderColor, animate);
    }

    private void ApplySliceHoverVisual(SliceVisual sliceVisual, bool animate)
    {
        Panel.SetZIndex(sliceVisual.Path, HoveredSliceZIndex);
        ApplyBrushColor(sliceVisual.FillBrush, _hoverColor, animate);
        ApplyBrushColor(sliceVisual.StrokeBrush, _borderHoverColor, animate);
    }

    private void ApplyCenterNormalVisual(bool animate)
    {
        var centerVisual = _centerVisual;
        if (centerVisual == null)
        {
            return;
        }

        ApplyBrushColor(centerVisual.FillBrush, _hubColor, animate);
        ApplyBrushColor(centerVisual.StrokeBrush, _hubBorderColor, animate);

        if (animate)
        {
            AnimateOpacity(centerVisual.Icon, 0, _hoverDuration, _standardEasing, () =>
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

        ApplyBrushColor(centerVisual.FillBrush, _hubHoverColor, animate);
        ApplyBrushColor(centerVisual.StrokeBrush, _centerHoverBorderColor, animate);
        centerVisual.Icon.Visibility = Visibility.Visible;

        if (animate)
        {
            AnimateOpacity(centerVisual.Icon, 1, _hoverDuration, _standardEasing);
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
            AnimateSliceColor(brush, color, _hoverDuration, _standardEasing);
            return;
        }

        brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
        brush.Color = color;
    }

    private static double NormalizeSignedAngle(double angle)
    {
        while (angle <= -180)
        {
            angle += 360;
        }

        while (angle > 180)
        {
            angle -= 360;
        }

        return angle;
    }

    private void AddSurfaceRing(
        Point center,
        double outerRadius,
        double innerRadius,
        Color fillColor,
        Color borderColor,
        double strokeThickness,
        bool isHighContrast)
    {
        if (outerRadius <= 0)
        {
            return;
        }

        Geometry ringGeometry;
        if (innerRadius > 0)
        {
            ringGeometry = new CombinedGeometry(
                GeometryCombineMode.Exclude,
                new EllipseGeometry(center, outerRadius, outerRadius),
                new EllipseGeometry(center, innerRadius, innerRadius));
        }
        else
        {
            ringGeometry = new EllipseGeometry(center, outerRadius, outerRadius);
        }

        var surfacePath = new Path
        {
            Data = ringGeometry,
            Fill = new SolidColorBrush(fillColor),
            Stroke = new SolidColorBrush(borderColor),
            StrokeThickness = strokeThickness,
            IsHitTestVisible = false,
            SnapsToDevicePixels = true,
        };

        if (!isHighContrast && GetThemedResource("RadialMenuAmbientShadowEffect") is Effect effect)
        {
            surfacePath.Effect = effect.CloneCurrentValue();
        }

        Panel.SetZIndex(surfacePath, 0);
        PieCanvas.Children.Add(surfacePath);
    }

    private object GetThemedResource(string resourceKey)
    {
        if (SystemParameters.HighContrast)
        {
            var highContrastKey = $"{resourceKey}.HighContrast";
            if (TryFindResource(highContrastKey) is { } highContrastValue)
            {
                return highContrastValue;
            }
        }

        var themedKey = IsAppDarkModeEnabled() ? $"{resourceKey}.Dark" : $"{resourceKey}.Light";
        if (TryFindResource(themedKey) is { } themedValue)
        {
            return themedValue;
        }

        return TryFindResource(resourceKey);
    }

    private SolidColorBrush GetBrushResource(string resourceKey, Color fallbackColor)
    {
        var value = GetThemedResource(resourceKey);

        if (value is SolidColorBrush solidColorBrush)
        {
            return solidColorBrush;
        }

        if (value is Color color)
        {
            return new SolidColorBrush(color);
        }

        return new SolidColorBrush(fallbackColor);
    }

    private double GetDoubleResource(string resourceKey, double fallbackValue)
    {
        var value = GetThemedResource(resourceKey);

        if (value is double doubleValue)
        {
            return doubleValue;
        }

        if (value is int intValue)
        {
            return intValue;
        }

        return fallbackValue;
    }

    private Duration GetDurationResource(string resourceKey, Duration fallbackValue)
    {
        var value = GetThemedResource(resourceKey);

        if (value is Duration duration)
        {
            return duration;
        }

        if (value is TimeSpan timeSpan)
        {
            return new Duration(timeSpan);
        }

        return fallbackValue;
    }

    private IEasingFunction GetEasingResource(string resourceKey, IEasingFunction fallbackValue)
    {
        var value = GetThemedResource(resourceKey);
        return value as IEasingFunction ?? fallbackValue;
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
            QueueThemeRefresh();
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        QueueThemeRefresh();
    }

    private void QueueThemeRefresh()
    {
        _themeRefreshPending = true;

        if (!IsLoaded || _themeRefreshQueued)
        {
            return;
        }

        _themeRefreshQueued = true;

        Dispatcher.InvokeAsync(() =>
        {
            _themeRefreshQueued = false;

            if (!IsLoaded || !IsVisible)
            {
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

    private Point GetTextPosition(Point center, double radius, double startAngle, double endAngle)
    {
        var midAngle = (startAngle + endAngle) / 2;

        var x = center.X + (radius * Math.Cos(midAngle * Math.PI / 180));
        var y = center.Y + (radius * Math.Sin(midAngle * Math.PI / 180));

        return SnapPoint(new Point(x, y));
    }

    private Path CreateSlice(Point center, double outerRadius, double innerRadius, double startAngle, double endAngle)
    {
        var startPointOuter = GetPointOnCircle(center, outerRadius, startAngle);
        var endPointOuter = GetPointOnCircle(center, outerRadius, endAngle);
        var startPointInner = GetPointOnCircle(center, innerRadius, startAngle);
        var endPointInner = GetPointOnCircle(center, innerRadius, endAngle);

        var figure = new PathFigure { StartPoint = startPointInner };

        figure.Segments.Add(new LineSegment(startPointOuter, true));

        figure.Segments.Add(new ArcSegment(
            endPointOuter,
            new Size(outerRadius, outerRadius),
            0,
            endAngle - startAngle > 180,
            SweepDirection.Clockwise,
            true));

        figure.Segments.Add(new LineSegment(endPointInner, true));

        if (innerRadius > 0)
        {
            figure.Segments.Add(new ArcSegment(
                startPointInner,
                new Size(innerRadius, innerRadius),
                0,
                endAngle - startAngle > 180,
                SweepDirection.Counterclockwise,
                true));
        }
        else
        {
            figure.Segments.Add(new LineSegment(center, true));
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        return new Path { Data = geometry };
    }

    private Point GetPointOnCircle(Point center, double radius, double angleInDegrees)
    {
        var angleInRadians = angleInDegrees * Math.PI / 180;
        var x = center.X + (radius * Math.Cos(angleInRadians));
        var y = center.Y + (radius * Math.Sin(angleInRadians));
        return SnapPoint(new Point(x, y));
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

    private static Color BlendColor(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        var r = (byte)Math.Round((from.R * (1 - amount)) + (to.R * amount));
        var g = (byte)Math.Round((from.G * (1 - amount)) + (to.G * amount));
        var b = (byte)Math.Round((from.B * (1 - amount)) + (to.B * amount));
        return Color.FromRgb(r, g, b);
    }

    private static Color GetSystemAccentColor()
    {
        if (SystemParameters.WindowGlassBrush is SolidColorBrush accentBrush)
        {
            return accentBrush.Color;
        }

        return SystemParameters.WindowGlassColor;
    }

    private static bool IsAppDarkModeEnabled()
    {
        const string personalizePath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        const string appsUseLightTheme = "AppsUseLightTheme";

        try
        {
            using var personalizeKey = Registry.CurrentUser.OpenSubKey(personalizePath);
            if (personalizeKey?.GetValue(appsUseLightTheme) is int lightThemeFlag)
            {
                return lightThemeFlag == 0;
            }
        }
        catch
        {
            // Ignore registry access failures and fallback to system colors.
        }

        return GetRelativeLuminance(SystemColors.WindowColor) < 0.5;
    }

    private static Color GetAccessibleAccentColor(Color accentColor, Color backgroundColor)
    {
        var contrast = GetContrastRatio(accentColor, backgroundColor);
        if (contrast >= 3.0)
        {
            return accentColor;
        }

        var isDarkBackground = GetRelativeLuminance(backgroundColor) < 0.5;
        var target = isDarkBackground ? Colors.White : Colors.Black;
        return BlendColor(accentColor, target, 0.35);
    }

    private static double GetContrastRatio(Color foreground, Color background)
    {
        var foregroundLuminance = GetRelativeLuminance(foreground);
        var backgroundLuminance = GetRelativeLuminance(background);
        var brighter = Math.Max(foregroundLuminance, backgroundLuminance);
        var darker = Math.Min(foregroundLuminance, backgroundLuminance);
        return (brighter + 0.05) / (darker + 0.05);
    }

    private static double GetRelativeLuminance(Color color)
    {
        static double ChannelToLinear(byte channel)
        {
            var srgb = channel / 255.0;
            return srgb <= 0.03928
                ? srgb / 12.92
                : Math.Pow((srgb + 0.055) / 1.055, 2.4);
        }

        var r = ChannelToLinear(color.R);
        var g = ChannelToLinear(color.G);
        var b = ChannelToLinear(color.B);
        return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
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
