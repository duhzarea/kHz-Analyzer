using System;
using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace KhzAnalyzer.Controls;

/// <summary>
/// Timeline scrubber: neutral track, Apple Music red fill for the elapsed part, and a glossy
/// water-drop thumb that follows playback. Click anywhere to jump, drag to scrub (seeks live).
/// Arrow keys step 5 s when focused (via <see cref="Seeking"/>).
/// </summary>
public sealed class Scrubber : Grid
{
    private static readonly Color Red = Color.FromArgb(255, 0xFA, 0x24, 0x3C);
    private readonly Border _track = new() { Height = 4, CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _fill = new() { Height = 4, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid _thumb = new() { Width = 16, Height = 16, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
    private double _value;
    private bool _dragging;

    /// <summary>Raised while the user clicks or drags (0..1). <c>final</c> is true on release.</summary>
    public event Action<double, bool>? Seeking;

    public bool IsDragging => _dragging;

    public double Value
    {
        get => _value;
        set { if (_dragging) return; _value = Math.Clamp(double.IsFinite(value) ? value : 0, 0, 1); Arrange(); }
    }

    public Scrubber()
    {
        Height = 22;
        Background = new SolidColorBrush(Colors.Transparent); // whole strip is clickable
        IsTabStop = true;
        UseSystemFocusVisuals = true;
        _track.Background = new SolidColorBrush(Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF));
        _fill.Background = new SolidColorBrush(Red);

        // Water drop: a soft contact shadow under a glass bead. The old thumb was a flat red ball with a
        // white dot stuck on it, lit from straight above, which matched nothing else on screen.
        var shadow = new Ellipse
        {
            Width = 14, Height = 14, Margin = new Thickness(1, 4, 1, -4),
            Fill = new RadialGradientBrush
            {
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(0x4D, 0, 0, 0), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(0x26, 0, 0, 0), Offset = 0.6 },
                    new GradientStop { Color = Color.FromArgb(0, 0, 0, 0), Offset = 1 },
                },
            },
        };
        // The bead itself, painted to the same model the glass surfaces are baked from: key light from
        // the upper left, a cool rim where that light grazes the edge, and a warm bounce coming back off
        // the red track on the lower right. The old thumb was lit from straight above, which matched
        // nothing else on screen and read as a sticker.
        var bead = new Ellipse
        {
            Width = 16, Height = 16, StrokeThickness = 1,
            Stroke = new LinearGradientBrush
            {
                StartPoint = new Point(0.2, 0), EndPoint = new Point(0.8, 1),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(0x59, 0xFF, 0xFF, 0xFF), Offset = 0.45 },
                    new GradientStop { Color = Color.FromArgb(0xCC, 0xFF, 0x8A, 0x9B), Offset = 1 },
                },
            },
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.36, 0.30), GradientOrigin = new Point(0.30, 0.24), RadiusX = 0.85, RadiusY = 0.85,
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(0x8C, 0xFF, 0xE6, 0xEA), Offset = 0.32 },
                    new GradientStop { Color = Color.FromArgb(0x73, 0xFA, 0x24, 0x3C), Offset = 0.78 },
                    new GradientStop { Color = Color.FromArgb(0xB3, 0xD3, 0x12, 0x2B), Offset = 1 },
                },
            },
        };
        _thumb.Children.Add(shadow);
        _thumb.Children.Add(bead);

        Children.Add(_track); Children.Add(_fill); Children.Add(_thumb);
        SizeChanged += (_, _) => Arrange();
        Loaded += (_, _) => { ElementCompositionPreview.SetIsTranslationEnabled(_thumb, true); Arrange(); };

        PointerEntered += (_, _) => Hover(true);
        PointerExited += (_, _) => { if (!_dragging) Hover(false); };
        PointerPressed += OnPressed;
        PointerMoved += OnMoved;
        PointerReleased += OnReleased;
        PointerCaptureLost += (_, _) => { if (_dragging) { _dragging = false; Seeking?.Invoke(_value, true); Hover(false); } };
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
    }

    private void Hover(bool on)
    {
        _track.Height = _fill.Height = on ? 6 : 4;
        var v = ElementCompositionPreview.GetElementVisual(_thumb);
        v.CenterPoint = new Vector3(8, 8, 0);
        var a = v.Compositor.CreateVector3KeyFrameAnimation();
        a.InsertKeyFrame(1, on ? new Vector3(1.3f, 1.3f, 1) : Vector3.One, v.Compositor.CreateCubicBezierEasingFunction(new Vector2(0.34f, 1.56f), new Vector2(0.64f, 1f)));
        a.Duration = TimeSpan.FromMilliseconds(220);
        v.StartAnimation("Scale", a);
    }

    private double FromX(double x) => ActualWidth <= 0 ? 0 : Math.Clamp(x / ActualWidth, 0, 1);

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        CapturePointer(e.Pointer);
        _dragging = true;
        _value = FromX(e.GetCurrentPoint(this).Position.X);
        Arrange(); Seeking?.Invoke(_value, false);
        e.Handled = true;
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        _value = FromX(e.GetCurrentPoint(this).Position.X);
        Arrange(); Seeking?.Invoke(_value, false);
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        _value = FromX(e.GetCurrentPoint(this).Position.X);
        _dragging = false;
        ReleasePointerCapture(e.Pointer);
        Arrange(); Seeking?.Invoke(_value, true);
        Hover(false);
    }

    private void Arrange()
    {
        double w = ActualWidth;
        if (w <= 0) return;
        _fill.Width = Math.Max(0, w * _value);
        _thumb.Margin = new Thickness(Math.Clamp(w * _value - 8, -8, w - 8), 0, 0, 0);
    }
}
