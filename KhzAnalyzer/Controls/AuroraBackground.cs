using System;
using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using KhzAnalyzer.Utils;
using Windows.UI;

namespace KhzAnalyzer.Controls;

/// <summary>
/// The design system's ground: three soft spectral discs drifting slowly (khz-aurora)
/// and three falling hairline beams (lumina's animate-beam-1/2/3). Pure Composition, no hit testing.
/// </summary>
public sealed class AuroraBackground : Grid
{
    private Compositor? _c;
    private ContainerVisual? _root;
    private readonly SpriteVisual[] _discs = new SpriteVisual[3];
    private readonly CompositionRadialGradientBrush[] _brushes = new CompositionRadialGradientBrush[3];
    private readonly Rectangle[] _beams = new Rectangle[3];
    private static readonly double[] BeamX = { 0.18, 0.52, 0.83 };
    private static readonly double[] BeamSeconds = { 6, 8, 7 };
    private static readonly double[] BeamDelay = { 0, 2, 4 };
    private static readonly double[] DriftSeconds = { 22, 26, 30 };

    public AuroraBackground()
    {
        IsHitTestVisible = false;
        Loaded += OnLoaded;
        SizeChanged += (_, _) => LayoutAndAnimate();
        ActualThemeChanged += (_, _) => ApplyColors();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_root is not null) return;
        _c = ElementCompositionPreview.GetElementVisual(this).Compositor;
        _root = _c.CreateContainerVisual();
        for (int i = 0; i < 3; i++)
        {
            _brushes[i] = _c.CreateRadialGradientBrush();
            _discs[i] = _c.CreateSpriteVisual();
            _discs[i].Brush = _brushes[i];
            _root.Children.InsertAtTop(_discs[i]);
        }
        ElementCompositionPreview.SetElementChildVisual(this, _root);

        if (Motion.Enabled)
        {
            var beamBrush = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(0.5, 0), EndPoint = new Windows.Foundation.Point(0.5, 1) };
            beamBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0, 0xFA, 0x24, 0x3C), Offset = 0 });
            beamBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0xCC, 0xFA, 0x24, 0x3C), Offset = 0.5 });
            beamBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0, 0xFA, 0x24, 0x3C), Offset = 1 });
            for (int i = 0; i < 3; i++)
            {
                _beams[i] = new Rectangle { Width = 1, Fill = beamBrush, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Opacity = 0 };
                Children.Add(_beams[i]);
            }
        }
        ApplyColors();
        LayoutAndAnimate();
    }

    private void ApplyColors()
    {
        if (_c is null) return;
        bool dark = ActualTheme == ElementTheme.Dark;
        (Color col, double a)[] set =
        {
            (Color.FromArgb(255, 0x22, 0xD3, 0xEE), dark ? 0.30 : 0.24),
            (Color.FromArgb(255, 0xFA, 0x24, 0x3C), dark ? 0.26 : 0.16),
            (Color.FromArgb(255, 0x4A, 0xDE, 0x80), dark ? 0.16 : 0.14),
        };
        for (int i = 0; i < 3; i++)
        {
            var b = _brushes[i]; b.ColorStops.Clear();
            byte A(double k) => (byte)Math.Round(255 * set[i].a * k);
            b.ColorStops.Add(_c.CreateColorGradientStop(0f, Color.FromArgb(A(1), set[i].col.R, set[i].col.G, set[i].col.B)));
            b.ColorStops.Add(_c.CreateColorGradientStop(0.5f, Color.FromArgb(A(0.45), set[i].col.R, set[i].col.G, set[i].col.B)));
            b.ColorStops.Add(_c.CreateColorGradientStop(1f, Color.FromArgb(0, set[i].col.R, set[i].col.G, set[i].col.B)));
        }
    }

    private void LayoutAndAnimate()
    {
        if (_c is null || ActualWidth <= 0 || ActualHeight <= 0) return;
        float w = (float)ActualWidth, h = (float)ActualHeight, d = Math.Max(w, h) * 0.62f;
        Vector3[] start = { new(-0.18f * w, -0.35f * h, 0), new(w - 0.55f * d, -0.25f * h, 0), new(0.28f * w, h - 0.45f * d, 0) };
        for (int i = 0; i < 3; i++)
        {
            _discs[i].Size = new Vector2(d, d);
            _discs[i].Offset = start[i];
            if (!Motion.Enabled) continue;
            var a = _c.CreateVector3KeyFrameAnimation();
            var ease = _c.CreateCubicBezierEasingFunction(new Vector2(0.45f, 0f), new Vector2(0.55f, 1f));
            a.InsertKeyFrame(0, start[i]);
            a.InsertKeyFrame(0.5f, start[i] + new Vector3(0.08f * w, 0.06f * h, 0), ease);
            a.InsertKeyFrame(1, start[i] + new Vector3(-0.06f * w, 0.10f * h, 0), ease);
            a.Duration = TimeSpan.FromSeconds(DriftSeconds[i]);
            a.IterationBehavior = AnimationIterationBehavior.Forever;
            a.Direction = AnimationDirection.Alternate;
            _discs[i].StartAnimation("Offset", a);
        }
        if (!Motion.Enabled) return;
        for (int i = 0; i < 3; i++)
        {
            var r = _beams[i]; if (r is null) continue;
            r.Height = h * 0.38; r.Margin = new Thickness(w * BeamX[i], 0, 0, 0); r.Opacity = 1;
            ElementCompositionPreview.SetIsTranslationEnabled(r, true);
            var v = ElementCompositionPreview.GetElementVisual(r);
            var t = _c.CreateVector3KeyFrameAnimation();
            t.InsertKeyFrame(0, new Vector3(0, -h * 0.38f, 0)); t.InsertKeyFrame(1, new Vector3(0, h * 1.2f, 0), _c.CreateLinearEasingFunction());
            var o = _c.CreateScalarKeyFrameAnimation();
            o.InsertKeyFrame(0, 0); o.InsertKeyFrame(0.05f, 0.5f); o.InsertKeyFrame(0.5f, 1f); o.InsertKeyFrame(0.95f, 0.5f); o.InsertKeyFrame(1, 0);
            foreach (var anim in new KeyFrameAnimation[] { t, o })
            {
                anim.Duration = TimeSpan.FromSeconds(BeamSeconds[i]); anim.DelayTime = TimeSpan.FromSeconds(BeamDelay[i]);
                anim.IterationBehavior = AnimationIterationBehavior.Forever; anim.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            }
            v.StartAnimation("Translation", t); v.StartAnimation("Opacity", o);
        }
    }
}
