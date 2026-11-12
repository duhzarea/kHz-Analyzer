using System;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI;

namespace KhzAnalyzer.Utils;

/// <summary>
/// macOS-style theme switch: the window is photographed in the old theme, the new theme is applied
/// underneath, and the photo is erased by a soft-edged circle that grows from the point that was clicked
/// (650 ms, the design system's in-out-sharp curve). Same idea as the web version's view-transition reveal.
/// </summary>
public static class ThemeTransition
{
    private static bool _running;

    public static async Task RunAsync(FrameworkElement root, FrameworkElement overlayHost, Point origin, Color oldGround, Action apply)
    {
        if (_running || !Motion.Enabled || root.ActualWidth < 1) { apply(); return; }
        _running = true;
        bool applied = false;
        void Apply() { if (!applied) { applied = true; apply(); } }
        try
        {
            var rtb = new RenderTargetBitmap();
            await rtb.RenderAsync(root);
            var pixels = await rtb.GetPixelsAsync();
            float scale = (float)(root.XamlRoot?.RasterizationScale ?? 1.0);

            var stream = new InMemoryRandomAccessStream();
            var enc = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            enc.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                (uint)rtb.PixelWidth, (uint)rtb.PixelHeight, 96 * scale, 96 * scale, pixels.ToArray());
            await enc.FlushAsync();
#if DEBUG
            try
            {
                stream.Seek(0);
                var bytes = new byte[stream.Size];
                await stream.ReadAsync(bytes.AsBuffer(), (uint)stream.Size, InputStreamOptions.None);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "khz-reveal.png"), bytes);
                Logger.Log($"Reveal snapshot {rtb.PixelWidth}x{rtb.PixelHeight}, element {root.ActualWidth}x{root.ActualHeight}, scale {scale}, origin {origin}");
            }
            catch { }
#endif
            stream.Seek(0);

            var visual = ElementCompositionPreview.GetElementVisual(overlayHost);
            var c = visual.Compositor;
            var surface = LoadedImageSurface.StartLoadFromStream(stream);
            var loaded = new TaskCompletionSource<bool>();
            surface.LoadCompleted += (_, e) => loaded.TrySetResult(e.Status == LoadedImageSourceLoadStatus.Success);
            if (!await loaded.Task) { Apply(); return; }

            var size = new Vector2((float)root.ActualWidth, (float)root.ActualHeight);

            // The mask: transparent inside the circle (new theme shows), opaque outside (old photo stays).
            var mask = c.CreateRadialGradientBrush();
            mask.MappingMode = CompositionMappingMode.Absolute;
            mask.EllipseCenter = new Vector2((float)origin.X, (float)origin.Y);
            mask.EllipseRadius = Vector2.Zero;
            mask.ExtendMode = CompositionGradientExtendMode.Clamp;
            mask.ColorStops.Add(c.CreateColorGradientStop(0f, Color.FromArgb(0, 0, 0, 0)));
            mask.ColorStops.Add(c.CreateColorGradientStop(0.86f, Color.FromArgb(0, 0, 0, 0)));
            mask.ColorStops.Add(c.CreateColorGradientStop(1f, Color.FromArgb(255, 0, 0, 0)));

            CompositionMaskBrush Masked(CompositionBrush source)
            {
                var m = c.CreateMaskBrush(); m.Source = source; m.Mask = mask; return m;
            }

            // Ground colour under the photo: the acrylic backdrop is not in the photo, so without it the
            // see-through parts would show the new theme too early.
            var ground = c.CreateSpriteVisual(); ground.Size = size;
            ground.Brush = Masked(c.CreateColorBrush(oldGround));
            var photoBrush = c.CreateSurfaceBrush(surface); photoBrush.Stretch = CompositionStretch.Fill;
            var photo = c.CreateSpriteVisual(); photo.Size = size; photo.Brush = Masked(photoBrush);
            var container = c.CreateContainerVisual(); container.Size = size;
            container.Children.InsertAtTop(ground); container.Children.InsertAtTop(photo);
            ElementCompositionPreview.SetElementChildVisual(overlayHost, container);

            Apply();
            await Task.Delay(30); // let the new theme lay out under the photo

            float dx = Math.Max((float)origin.X, size.X - (float)origin.X);
            float dy = Math.Max((float)origin.Y, size.Y - (float)origin.Y);
            float r = MathF.Sqrt(dx * dx + dy * dy) / 0.86f + 40f;
            var anim = c.CreateVector2KeyFrameAnimation();
            anim.InsertKeyFrame(0f, Vector2.Zero);
            anim.InsertKeyFrame(1f, new Vector2(r, r), c.CreateCubicBezierEasingFunction(new Vector2(0.86f, 0f), new Vector2(0.07f, 1f)));
            anim.Duration = TimeSpan.FromMilliseconds(650);
            var batch = c.CreateScopedBatch(CompositionBatchTypes.Animation);
            mask.StartAnimation(nameof(CompositionRadialGradientBrush.EllipseRadius), anim);
            batch.End();
            var done = new TaskCompletionSource<bool>();
            batch.Completed += (_, _) => done.TrySetResult(true);
            await done.Task;

            ElementCompositionPreview.SetElementChildVisual(overlayHost, null);
            surface.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Log($"Theme transition fell back to an instant switch: {ex.Message}");
            ElementCompositionPreview.SetElementChildVisual(overlayHost, null);
            Apply();
        }
        finally { _running = false; }
    }
}
