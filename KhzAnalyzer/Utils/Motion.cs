using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Windows.UI.ViewManagement;

namespace KhzAnalyzer.Utils;

/// <summary>
/// Motion from the design system, on the Composition layer so it runs off the UI thread:
/// entrance (slide-up + fade, staggered), masked headline reveal, tilt + lift on hover,
/// magnetic pull with a spring release, and number count-up.
/// Everything is skipped when Windows "Animation effects" is off.
/// </summary>
public static class Motion
{
    private static readonly bool WindowsAnimations = new UISettings().AnimationsEnabled;

    /// <summary>Settings › Animations: On (default), follow Windows, or Off.</summary>
    public static bool Enabled => AppSettings.Current.Motion switch
    {
        "Off" => false,
        "System" => WindowsAnimations,
        _ => true,
    };

    private static Visual V(UIElement e) => ElementCompositionPreview.GetElementVisual(e);
    private static CompositionEasingFunction ExpoOut(Compositor c) => c.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1f), new Vector2(0.3f, 1f));
    private static CompositionEasingFunction Overshoot(Compositor c) => c.CreateCubicBezierEasingFunction(new Vector2(0.34f, 1.56f), new Vector2(0.64f, 1f));

    /// <summary>volta's slide-up / lumina's fadeSlideIn: 24px rise and fade, 800ms, expo-out, staggered.</summary>
    public static void Entrance(IReadOnlyList<UIElement> elements, int staggerMs = 90, int startDelayMs = 0, float distance = 24f)
    {
        if (!Enabled) return;
        for (int i = 0; i < elements.Count; i++)
        {
            var el = elements[i];
            ElementCompositionPreview.SetIsTranslationEnabled(el, true);
            var v = V(el); var c = v.Compositor; var ease = ExpoOut(c);
            var delay = TimeSpan.FromMilliseconds(startDelayMs + i * staggerMs);
            var o = c.CreateScalarKeyFrameAnimation();
            o.InsertKeyFrame(0, 0); o.InsertKeyFrame(1, 1, ease);
            o.Duration = TimeSpan.FromMilliseconds(800); o.DelayTime = delay; o.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            var t = c.CreateVector3KeyFrameAnimation();
            t.InsertKeyFrame(0, new Vector3(0, distance, 0)); t.InsertKeyFrame(1, Vector3.Zero, ease);
            t.Duration = o.Duration; t.DelayTime = delay; t.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            var batch = c.CreateScopedBatch(CompositionBatchTypes.Animation);
            v.StartAnimation("Opacity", o);
            v.StartAnimation("Translation", t);
            batch.End();
            // Write the end values back as static values: snapshots (theme reveal) read those, not the animation.
            batch.Completed += (_, _) => { v.Opacity = 1f; v.Properties.InsertVector3("Translation", Vector3.Zero); };
        }
    }

    /// <summary>digital-architect's text-reveal: the text rises from below a clipping edge, 1200ms expo-out.</summary>
    public static void Reveal(FrameworkElement text, UIElement clipHost, int delayMs = 0)
    {
        if (!Enabled) return;
        var host = V(clipHost); host.Clip = host.Compositor.CreateInsetClip();
        ElementCompositionPreview.SetIsTranslationEnabled(text, true);
        var v = V(text); var c = v.Compositor;
        var t = c.CreateVector3KeyFrameAnimation();
        t.InsertKeyFrame(0, new Vector3(0, (float)Math.Max(60, text.ActualHeight * 1.1), 0));
        t.InsertKeyFrame(1, Vector3.Zero, ExpoOut(c));
        t.Duration = TimeSpan.FromMilliseconds(1200); t.DelayTime = TimeSpan.FromMilliseconds(delayMs);
        t.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        var batch = c.CreateScopedBatch(CompositionBatchTypes.Animation);
        v.StartAnimation("Translation", t);
        batch.End();
        batch.Completed += (_, _) => { v.Properties.InsertVector3("Translation", Vector3.Zero); host.Clip = null; };
    }

    // ───── Tilt + lift (ftcoffee hero/panel tilt) ─────
    public static readonly DependencyProperty TiltProperty =
        DependencyProperty.RegisterAttached("Tilt", typeof(double), typeof(Motion), new PropertyMetadata(0d, OnTiltChanged));
    public static double GetTilt(DependencyObject d) => (double)d.GetValue(TiltProperty);
    public static void SetTilt(DependencyObject d, double v) => d.SetValue(TiltProperty, v);

    private static void OnTiltChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (!Enabled || d is not FrameworkElement fe || (double)e.NewValue <= 0) return;
        void Setup()
        {
            ElementCompositionPreview.SetIsTranslationEnabled(fe, true);
            var v = V(fe); var c = v.Compositor;
            var implicitAnims = c.CreateImplicitAnimationCollection();
            foreach (var prop in new[] { "Translation", "Scale" })
            {
                var a = c.CreateVector3KeyFrameAnimation(); a.Target = prop;
                a.InsertExpressionKeyFrame(1, "this.FinalValue", c.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.8f), new Vector2(0.2f, 1f)));
                a.Duration = TimeSpan.FromMilliseconds(500); implicitAnims[prop] = a;
            }
            var r = c.CreateScalarKeyFrameAnimation(); r.Target = "RotationAngleInDegrees";
            r.InsertExpressionKeyFrame(1, "this.FinalValue"); r.Duration = TimeSpan.FromMilliseconds(400); implicitAnims["RotationAngleInDegrees"] = r;
            v.ImplicitAnimations = implicitAnims;
        }
        fe.Loaded += (_, _) => Setup();
        if (fe.IsLoaded) Setup();
        fe.PointerMoved += (s, args) =>
        {
            double max = GetTilt(fe); var p = args.GetCurrentPoint(fe).Position;
            float w = (float)fe.ActualWidth, h = (float)fe.ActualHeight; if (w <= 0 || h <= 0) return;
            float x = (float)(p.X / w - 0.5), y = (float)(p.Y / h - 0.5);
            var v = V(fe);
            v.CenterPoint = new Vector3(w / 2, h / 2, 0);
            var axis = new Vector3(-y, x, 0);
            if (axis.LengthSquared() < 0.0001f) return;
            v.RotationAxis = Vector3.Normalize(axis);
            v.RotationAngleInDegrees = (float)(Math.Min(1, axis.Length() * 2) * max);
            v.Properties.InsertVector3("Translation", new Vector3(0, -4, 0));
            v.Scale = new Vector3(1.015f, 1.015f, 1);
        };
        void Release(object s, PointerRoutedEventArgs a)
        {
            var v = V(fe);
            v.RotationAngleInDegrees = 0; v.Properties.InsertVector3("Translation", Vector3.Zero); v.Scale = Vector3.One;
        }
        fe.PointerExited += Release; fe.PointerCanceled += Release; fe.PointerCaptureLost += Release;
    }

    // ───── Magnetic (ftcoffee .magnetic-btn: 0.3 pull, elastic release) ─────
    public static readonly DependencyProperty MagneticProperty =
        DependencyProperty.RegisterAttached("Magnetic", typeof(bool), typeof(Motion), new PropertyMetadata(false, OnMagneticChanged));
    public static bool GetMagnetic(DependencyObject d) => (bool)d.GetValue(MagneticProperty);
    public static void SetMagnetic(DependencyObject d, bool v) => d.SetValue(MagneticProperty, v);

    private static void OnMagneticChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (!Enabled || d is not FrameworkElement fe || !(bool)e.NewValue) return;
        fe.PointerMoved += (s, args) =>
        {
            ElementCompositionPreview.SetIsTranslationEnabled(fe, true);
            var p = args.GetCurrentPoint(fe).Position; var v = V(fe); var c = v.Compositor;
            var target = new Vector3((float)((p.X - fe.ActualWidth / 2) * 0.3), (float)((p.Y - fe.ActualHeight / 2) * 0.3), 0);
            var a = c.CreateVector3KeyFrameAnimation();
            a.InsertKeyFrame(1, target, c.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.8f), new Vector2(0.2f, 1f)));
            a.Duration = TimeSpan.FromMilliseconds(400);
            v.StartAnimation("Translation", a);
        };
        void Release(object s, PointerRoutedEventArgs a)
        {
            var v = V(fe); var c = v.Compositor;
            var spring = c.CreateSpringVector3Animation();
            spring.FinalValue = Vector3.Zero; spring.DampingRatio = 0.3f; spring.Period = TimeSpan.FromMilliseconds(55);
            v.StartAnimation("Translation", spring);
        }
        fe.PointerExited += Release; fe.PointerCanceled += Release;
    }

    private sealed class Count { public int From, To; public DateTime Start; public Microsoft.UI.Dispatching.DispatcherQueueTimer? Timer; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TextBlock, Count> _counts = new();

    /// <summary>Counts a number up or down in the text over 600ms. A running count is retargeted, never doubled.</summary>
    public static void CountTo(TextBlock tb, int value)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        int shown = int.TryParse(tb.Text.Replace(",", ""), out var f) ? f : 0;
        var c = _counts.GetValue(tb, _ => new Count());
        bool running = c.Timer?.IsRunning == true;
        if (!running && (!Enabled || Math.Abs(value - shown) <= 5)) { tb.Text = value.ToString("N0", inv); return; }
        if (running && c.To == value) return;
        c.From = shown; c.To = value; c.Start = DateTime.UtcNow;
        if (c.Timer is null)
        {
            c.Timer = tb.DispatcherQueue.CreateTimer();
            c.Timer.Interval = TimeSpan.FromMilliseconds(16);
            c.Timer.Tick += (s, _) =>
            {
                double k = Math.Min(1, (DateTime.UtcNow - c.Start).TotalMilliseconds / 600.0);
                double eased = 1 - Math.Pow(1 - k, 4);
                tb.Text = ((int)Math.Round(c.From + (c.To - c.From) * eased)).ToString("N0", inv);
                if (k >= 1) s.Stop();
            };
        }
        if (!running) c.Timer.Start();
    }
}
