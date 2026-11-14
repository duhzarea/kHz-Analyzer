using System;
using System.IO;
using System.Linq;
using KhzAnalyzer.Pages;
using KhzAnalyzer.Services;
using KhzAnalyzer.Utils;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;

namespace KhzAnalyzer;

public sealed partial class MainWindow : Window
{
    private bool _onStart = true;

    public MainWindow()
    {
        InitializeComponent();

        // Glass: acrylic first (spec v2.1), Mica where acrylic is unavailable,
        // and the opaque fallback brush of RootGrid when neither exists.
        if (DesktopAcrylicController.IsSupported())
        {
            SystemBackdrop = new DesktopAcrylicBackdrop();
            RootGrid.Background = null;
        }
        else if (MicaController.IsSupported())
        {
            SystemBackdrop = new MicaBackdrop();
            RootGrid.Background = null;
        }

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

        // Absolute paths: an unpackaged app can be started with any working directory,
        // and ms-appx:/// URIs need package identity, which it does not have.
        string icone = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(icone)) AppWindow.SetIcon(icone);
        string iconePng = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon-64.png");
        if (File.Exists(iconePng))
        {
            AppTitleBar.IconSource = new ImageIconSource
            {
                ImageSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconePng)),
            };
        }

        // Studio-sized window, centred on the current display.
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        int w = Math.Min(1480, area.Width - 40), h = Math.Min(940, area.Height - 40);
        AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h));

        ApplyTheme(AppSettings.Current.Theme);
        RootGrid.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(Root_PreviewKeyDown), true);
        NavView.PaneOpened += (_, _) => SyncSidebarUi();
        NavView.PaneClosed += (_, _) => SyncSidebarUi();
        NavView.ItemInvoked += NavView_ItemInvoked;
        NavView.Loaded += NavView_Loaded;

        Activated += (_, e) =>
        {
            if (e.WindowActivationState == WindowActivationState.Deactivated) return;
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, EnsureFocus);
        };
        StartFrame.Navigate(typeof(StartPage));
        NavFrame.Navigate(typeof(HomePage)); // warm the Analyzer behind the start screen
        NavView.SelectedItem = NavAnalyzer;
        NavView.Visibility = Visibility.Collapsed; // nothing behind the start screen can take focus
    }

    // ───────────────────────────────────────────── Theme

    public bool IsDark => RootGrid.RequestedTheme == ElementTheme.Dark;

    /// <summary>Applies "Light" or "Dark" to the whole window, title bar buttons included.</summary>
    public void ApplyTheme(string theme)
    {
        bool dark = string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase);
        RootGrid.RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light;
        // Dark acrylic lets the wallpaper colour through strongly; a ground tint keeps it glassy but neutral.
        if (SystemBackdrop is not null)
            RootGrid.Background = dark ? new SolidColorBrush(Windows.UI.Color.FromArgb(0xC8, 0x0A, 0x0C, 0x11)) : null;
        AppWindow.TitleBar.PreferredTheme = dark ? TitleBarTheme.Dark : TitleBarTheme.Light;
        ThemeKnob.HorizontalAlignment = dark ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        ThemeGlyph.Glyph = dark ? "\uE708" : "\uE706"; // moon : sun
    }

    /// <summary>Switches theme with the circular reveal, growing from <paramref name="origin"/> (a point in the window).</summary>
    public async void SwitchTheme(string theme, Point? origin = null)
    {
        bool wantDark = string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase);
        if (wantDark == IsDark) return;
        Logger.Log($"Theme switch to {theme}");
        AppSettings.Current.Theme = theme;
        AppSettings.Current.Save();
        var o = origin ?? new Point(RootGrid.ActualWidth - 120, 24);
        var oldGround = IsDark ? Windows.UI.Color.FromArgb(255, 0x0A, 0x0C, 0x11) : Windows.UI.Color.FromArgb(255, 0xF3, 0xF4, 0xF7);
        await ThemeTransition.RunAsync(RootGrid, OverlayHost, o, oldGround, () => ApplyTheme(theme));
        ThemeChanged?.Invoke();
    }

    public event Action? ThemeChanged;

    public Point CenterOf(FrameworkElement e)
    {
        var p = e.TransformToVisual(RootGrid).TransformPoint(new Point(e.ActualWidth / 2, e.ActualHeight / 2));
        return p;
    }

    /// <summary>Where the theme reveal grows from: the switch when the sidebar is open, the item's icon when it is compact.</summary>
    private Point ThemeOrigin() => NavView.IsPaneOpen ? CenterOf(ThemeToggle) : CenterOf(NavTheme);

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (ReferenceEquals(args.InvokedItemContainer, NavTheme))
            SwitchTheme(IsDark ? "Light" : "Dark", ThemeOrigin());
    }

    /// <summary>Lifts the footer group (Theme, About, Settings) off the window edge so it keeps the sidebar's rhythm.</summary>
    private void NavView_Loaded(object sender, RoutedEventArgs e) => LiftFooter();

    /// <summary>
    /// Lifts the footer group (Theme, About, Settings) 20 px off the window edge. The margin goes on the footer's
    /// scroll viewer, which sits in an Auto row: margin on an item inside it would only make the list scroll.
    /// </summary>
    private void LiftFooter()
    {
        if (FindByName(NavView, "FooterItemsScrollViewer") is FrameworkElement host)
            host.Margin = new Thickness(0, 0, 0, 20);
        else if (NavView.Visibility == Visibility.Visible)
            Logger.Log("Sidebar footer host not found; footer keeps the default position.");
    }

    private static FrameworkElement? FindByName(DependencyObject root, string name)
    {
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var c = VisualTreeHelper.GetChild(root, i);
            if (c is FrameworkElement fe && fe.Name == name) return fe;
            if (FindByName(c, name) is FrameworkElement hit) return hit;
        }
        return null;
    }

    // ───────────────────────────────────────────── Start screen and modules

    public void OpenModule(string tag)
    {
        var item = tag switch
        {
            "download" => NavDownload,
            "snippets" => NavSnippets,
            "about" => NavAbout,
            _ => NavAnalyzer,
        };
        // Show the shell first: while it is collapsed the NavigationView has no template, so it has no Settings item yet.
        HideStart();
        if (tag == "settings")
        {
            NavView.UpdateLayout();
            LiftFooter();
            if (NavView.SettingsItem is not null) NavView.SelectedItem = NavView.SettingsItem;
            else Navigate("settings");
        }
        else if (!ReferenceEquals(NavView.SelectedItem, item)) NavView.SelectedItem = item;
        else Navigate(tag);
    }

    public void ShowStart()
    {
        if (_onStart) return;
        _onStart = true;
        StartFrame.Navigate(typeof(StartPage), null, new SuppressNavigationTransitionInfo());
        StartFrame.Visibility = Visibility.Visible;
        StartFrame.Opacity = 1;
        NavView.Visibility = Visibility.Collapsed;
        SidebarToggle.Visibility = Visibility.Collapsed;
    }

    private void HideStart()
    {
        if (!_onStart) return;
        _onStart = false;
        NavView.Visibility = Visibility.Visible;
        SidebarToggle.Visibility = Visibility.Visible;
        NavView.UpdateLayout();
        LiftFooter();
        SyncSidebarUi();
        var sb = new Storyboard();
        var fade = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(260) };
        Storyboard.SetTarget(fade, StartFrame); Storyboard.SetTargetProperty(fade, "Opacity");
        sb.Children.Add(fade);
        sb.Completed += (_, _) => { if (!_onStart) { StartFrame.Visibility = Visibility.Collapsed; StartFrame.Content = null; KeepFocus(); } };
        sb.Begin();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected) { Navigate("settings"); return; }
        if (args.SelectedItem is not NavigationViewItem item) return;
        if ((item.Tag as string) == "home")
        {
            ShowStart();
            return;
        }
        Navigate(item.Tag as string ?? "analyzer");
    }

    private void Navigate(string tag)
    {
        Type page = tag switch
        {
            "download" => typeof(DownloadPage),
            "snippets" => typeof(SnippetsPage),
            "about" => typeof(AboutPage),
            "settings" => typeof(SettingsPage),
            _ => typeof(HomePage),
        };
        if (NavFrame.CurrentSourcePageType != page)
            NavFrame.Navigate(page, null, new EntranceNavigationTransitionInfo());
        if (!_onStart) DispatcherQueue.TryEnqueue(KeepFocus);
    }

    private void EnsureFocus()
    {
        if (RootGrid.XamlRoot is null) return;
        if (FocusManager.GetFocusedElement(RootGrid.XamlRoot) is not null) return;
        if (_onStart) (StartFrame.Content as StartPage)?.FocusDefault();
        else KeepFocus();
    }

    /// <summary>Keyboard shortcuts need a focused element in the window; park focus on the selected sidebar item when it was lost.</summary>
    private void KeepFocus()
    {
        var f = FocusManager.GetFocusedElement(RootGrid.XamlRoot) as UIElement;
        bool lost = f is null || f.Visibility != Visibility.Visible || (f is FrameworkElement fe && FindParent<Frame>(fe) == StartFrame);
        if (lost) (NavView.SelectedItem as Control ?? NavView.SettingsItem as Control)?.Focus(FocusState.Pointer);
    }

    // ───────────────────────────────────────────── Sidebar

    public void ToggleSidebar()
    {
        if (_onStart) return;
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
        SyncSidebarUi();
    }

    private void SyncSidebarUi()
    {
        bool open = NavView.IsPaneOpen;
        SidebarGlyph.Glyph = open ? "" : ""; // ClosePane : OpenPane
        ToolTipService.SetToolTip(SidebarToggle, open ? "Hide sidebar (Ctrl+B)" : "Show sidebar (Ctrl+B)");
    }

    private void SidebarToggle_Click(object sender, RoutedEventArgs e) => ToggleSidebar();

    // ───────────────────────────────────────────── Keyboard shortcuts

    private static bool Down(VirtualKey k) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(k).HasFlag(CoreVirtualKeyStates.Down);

    private void Root_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        bool ctrl = Down(VirtualKey.Control), shift = Down(VirtualKey.Shift), alt = Down(VirtualKey.Menu);
        var sc = Shortcuts.Match(e.Key, ctrl, shift, alt);
#if DEBUG
        if (ctrl || e.Key is VirtualKey.F5 or VirtualKey.Space) Logger.Log($"Key {e.Key} ({(int)e.Key}) ctrl={ctrl} shift={shift} -> {sc?.Id ?? "none"}; focus {FocusManager.GetFocusedElement(RootGrid.XamlRoot)?.GetType().Name}");
#endif
        if (sc is null) return;

        // Typing wins: plain keys (Space, arrows, Del) stay with text fields and open menus.
        var focused = FocusManager.GetFocusedElement(RootGrid.XamlRoot);
        bool typing = focused is TextBox or PasswordBox or RichEditBox or AutoSuggestBox
            || (focused is FrameworkElement fe && FindParent<AutoSuggestBox>(fe) is not null);
        if (typing && !ctrl && sc.Key != VirtualKey.Escape && sc.Key != VirtualKey.F5 && sc.Key != VirtualKey.F1) return;
        if (typing && sc.Id is Shortcuts.Next or Shortcuts.Previous) return; // Ctrl+arrows move by word

        if (Run(sc.Id)) e.Handled = true;
    }

    private static T? FindParent<T>(DependencyObject d) where T : DependencyObject
    {
        for (var p = VisualTreeHelper.GetParent(d); p is not null; p = VisualTreeHelper.GetParent(p))
            if (p is T t) return t;
        return null;
    }

    /// <summary>Runs a shortcut by id. Returns false when it does not apply here.</summary>
    public bool Run(string id)
    {
        Logger.Log($"Shortcut: {id}");
        var player = Player.Instance;
        switch (id)
        {
            case Shortcuts.Sidebar: ToggleSidebar(); return true;
            case Shortcuts.Home: if (!_onStart) { NavView.SelectedItem = NavView.MenuItems[0]; } return true;
            case Shortcuts.Analyzer: OpenModule("analyzer"); return true;
            case Shortcuts.Download: OpenModule("download"); return true;
            case Shortcuts.Snippets: OpenModule("snippets"); return true;
            case Shortcuts.Settings: OpenModule("settings"); return true;
            case Shortcuts.ShowShortcuts:
                OpenModule("settings");
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, async () =>
                {
                    await System.Threading.Tasks.Task.Delay(350);
                    (NavFrame.Content as SettingsPage)?.ShowShortcuts();
                });
                return true;
            case Shortcuts.About: OpenModule("about"); return true;
            case Shortcuts.Theme: SwitchTheme(IsDark ? "Light" : "Dark", _onStart ? null : ThemeOrigin()); return true;
            case Shortcuts.PlayPause: player.Toggle(); return true;
            case Shortcuts.Next: player.Next(); return true;
            case Shortcuts.Previous: player.Previous(); return true;
            case Shortcuts.SeekForward: player.SeekBy(10); return true;
            case Shortcuts.SeekBack: player.SeekBy(-10); return true;
            // Esc stops whatever is running, from any screen. Falls through when nothing is.
            case Shortcuts.Cancel when TaskRegistry.CancelAll(): return true;
        }
        // Analyzer actions: open the Analyzer first if needed.
        if (id is Shortcuts.Browse or Shortcuts.Analyze or Shortcuts.Export or Shortcuts.Search or Shortcuts.TrackInfo or Shortcuts.Remove or Shortcuts.Cancel)
        {
            if (id is Shortcuts.Cancel or Shortcuts.Remove && (_onStart || NavFrame.Content is not HomePage)) return false;
            if (_onStart || NavFrame.Content is not HomePage) OpenModule("analyzer");
            return (NavFrame.Content as IShortcutTarget)?.HandleShortcut(id) ?? false;
        }
        return false;
    }
}
