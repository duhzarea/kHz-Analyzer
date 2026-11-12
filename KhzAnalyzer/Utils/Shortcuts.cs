using System.Collections.Generic;
using Windows.System;

namespace KhzAnalyzer.Utils;

/// <summary>One keyboard shortcut. <see cref="Keys"/> is what Settings shows; the window matches on Key + modifiers.</summary>
public sealed record Shortcut(string Id, string Group, string Description, VirtualKey Key, bool Ctrl = false, bool Shift = false, bool Alt = false, string? Display = null)
{
    public IReadOnlyList<string> Keys
    {
        get
        {
            var k = new List<string>();
            if (Ctrl) k.Add("Ctrl");
            if (Shift) k.Add("Shift");
            if (Alt) k.Add("Alt");
            k.Add(Display ?? Key.ToString());
            return k;
        }
    }
}

/// <summary>
/// The single list of keyboard shortcuts. MainWindow dispatches from it and Settings › Keyboard Shortcuts
/// renders it, so the two can never disagree. Keys typed into a text field are left to the field.
/// </summary>
public static class Shortcuts
{
    public const string Sidebar = "sidebar", Home = "home", Analyzer = "analyzer", Download = "download", Snippets = "snippets",
        Settings = "settings", About = "about", Theme = "theme", Browse = "browse", Analyze = "analyze", Cancel = "cancel",
        Export = "export", Search = "search", PlayPause = "playpause", Next = "next", Previous = "previous",
        SeekForward = "seekfwd", SeekBack = "seekback", TrackInfo = "trackinfo", Remove = "remove", ShowShortcuts = "shortcuts";

    public static readonly IReadOnlyList<Shortcut> All = new List<Shortcut>
    {
        new(Sidebar, "Navigation", "Show or hide the sidebar", VirtualKey.B, Ctrl: true),
        new(Home, "Navigation", "Start screen", VirtualKey.Number0, Ctrl: true, Display: "0"),
        new(Analyzer, "Navigation", "Analyzer", VirtualKey.Number1, Ctrl: true, Display: "1"),
        new(Download, "Navigation", "Download & Trim", VirtualKey.Number2, Ctrl: true, Display: "2"),
        new(Snippets, "Navigation", "Snippets", VirtualKey.Number3, Ctrl: true, Display: "3"),
        new(Settings, "Navigation", "Settings", (VirtualKey)188, Ctrl: true, Display: ","),
        new(ShowShortcuts, "Navigation", "Keyboard shortcuts", (VirtualKey)191, Ctrl: true, Display: "/"),
        new(About, "Navigation", "About", VirtualKey.F1),
        new(Theme, "Appearance", "Switch light and dark theme", VirtualKey.L, Ctrl: true, Shift: true),

        new(Browse, "Analyzer", "Choose a folder", VirtualKey.O, Ctrl: true),
        new(Analyze, "Analyzer", "Analyze the folder", VirtualKey.F5),
        new(Cancel, "Analyzer", "Cancel the analysis", VirtualKey.Escape, Display: "Esc"),
        new(Export, "Analyzer", "Export .docx and .csv reports", VirtualKey.E, Ctrl: true),
        new(Search, "Analyzer", "Search tracks", VirtualKey.F, Ctrl: true),
        new(TrackInfo, "Analyzer", "Track information", VirtualKey.I, Ctrl: true),
        new(Remove, "Analyzer", "Remove the selected track from the list", VirtualKey.Delete, Display: "Del"),

        new(PlayPause, "Player", "Play or pause", VirtualKey.Space),
        new(Next, "Player", "Next track", VirtualKey.Right, Ctrl: true, Display: "→"),
        new(Previous, "Player", "Previous track", VirtualKey.Left, Ctrl: true, Display: "←"),
        new(SeekForward, "Player", "Forward 10 seconds", VirtualKey.Right, Shift: true, Display: "→"),
        new(SeekBack, "Player", "Back 10 seconds", VirtualKey.Left, Shift: true, Display: "←"),
    };

    public static Shortcut? Match(VirtualKey key, bool ctrl, bool shift, bool alt)
    {
        if ((int)key == 193) key = (VirtualKey)191;          // ABNT2 keyboards: "/" sits on its own key
        if (key is >= VirtualKey.NumberPad0 and <= VirtualKey.NumberPad9) key = VirtualKey.Number0 + (key - VirtualKey.NumberPad0);
        foreach (var s in All)
            if (s.Key == key && s.Ctrl == ctrl && s.Shift == shift && s.Alt == alt) return s;
        return null;
    }
}

/// <summary>Pages that take shortcuts meant for them (Analyzer: browse, analyze, export…).</summary>
public interface IShortcutTarget
{
    bool HandleShortcut(string id);
}
