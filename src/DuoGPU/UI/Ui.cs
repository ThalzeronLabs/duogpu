using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace DuoGPU.UI;

/// <summary>The colors and the small building blocks shared by the windows.</summary>
public static class Ui
{
    public static readonly SolidColorBrush Background = Brush("#14141B");
    public static readonly SolidColorBrush Card = Brush("#1E1E28");
    public static readonly SolidColorBrush Text = Brush("#F4F4F8");
    public static readonly SolidColorBrush Muted = Brush("#9A9AB0");
    public static readonly SolidColorBrush Line = Brush("#2C2C3A");
    public static readonly SolidColorBrush Purple = Brush("#8B5CF6");
    public static readonly SolidColorBrush Green = Brush("#22C55E");
    public static readonly SolidColorBrush Amber = Brush("#F59E0B");
    public static readonly SolidColorBrush OkText = Brush("#7EE2A0");

    public static SolidColorBrush Brush(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    public static readonly FontFamily Font = new("Segoe UI Variable Text, Segoe UI");

    public static TextBlock Label(string text, double size = 13, Brush? color = null, FontWeight? weight = null) => new()
    {
        Text = text,
        FontFamily = Font,
        FontSize = size,
        Foreground = color ?? Text,
        FontWeight = weight ?? FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap,
    };

    /// <summary>A flat colored button with rounded corners and a hover state.</summary>
    public static Border Button(string text, Brush color, Action? onClick, double size = 15)
    {
        var label = Label(text, size, Brushes.White, FontWeights.SemiBold);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.TextAlignment = TextAlignment.Center;
        var b = new Border
        {
            Background = onClick == null ? Line : color,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 11, 16, 11),
            Child = label,
            Cursor = onClick == null ? Cursors.Arrow : Cursors.Hand,
            Focusable = onClick != null,
        };
        if (onClick != null)
        {
            b.MouseEnter += (_, _) => b.Opacity = 0.88;
            b.MouseLeave += (_, _) => b.Opacity = 1;
            b.MouseLeftButtonUp += (_, _) => onClick();
            b.KeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space) onClick(); };
            AutomationProperties(b, text);
        }
        return b;
    }

    public static TextBlock Link(string text, Action onClick, double size = 12)
    {
        var t = Label(text, size, Muted);
        t.TextDecorations = TextDecorations.Underline;
        t.Cursor = Cursors.Hand;
        t.MouseLeftButtonUp += (_, _) => onClick();
        AutomationProperties(t, text);
        return t;
    }

    static void AutomationProperties(UIElement e, string name) =>
        System.Windows.Automation.AutomationProperties.SetName(e, name);

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    const uint SWP_NOSIZE = 0x1, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10;

    /// <summary>Moves a shown window in physical pixels: the size comes from the window, the place from the callback.
    /// Works across monitors with different scales, where WPF's own coordinates do not line up.</summary>
    public static void PlaceInPixels(Window w, Func<int, int, (int x, int y)> where)
    {
        var h = new WindowInteropHelper(w).Handle;
        if (h == IntPtr.Zero || !GetWindowRect(h, out var r)) return;
        var (x, y) = where(r.Right - r.Left, r.Bottom - r.Top);
        SetWindowPos(h, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    /// <summary>The pixel bounds of a monitor by its GDI name, or the primary monitor's.</summary>
    public static System.Drawing.Rectangle Bounds(string? gdiName, bool workArea = false)
    {
        var screen = System.Windows.Forms.Screen.AllScreens
            .FirstOrDefault(s => string.Equals(s.DeviceName, gdiName, StringComparison.OrdinalIgnoreCase))
            ?? System.Windows.Forms.Screen.PrimaryScreen!;
        return workArea ? screen.WorkingArea : screen.Bounds;
    }
}
