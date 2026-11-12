using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using DuoGPU.Core;

namespace DuoGPU.UI;

/// <summary>The tray icon is drawn in code so it stays sharp at any scale: purple "AI", green gamepad, amber "!".</summary>
public static class TrayIcons
{
    public static readonly Color Purple = ColorTranslator.FromHtml("#8B5CF6");
    public static readonly Color Green = ColorTranslator.FromHtml("#22C55E");
    public static readonly Color Amber = ColorTranslator.FromHtml("#F59E0B");

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr handle);

    public static Bitmap Draw(Mode mode, bool attention, int size)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        var color = attention || mode == Mode.Unknown ? Amber : mode == Mode.Ai ? Purple : Green;
        float r = size * 0.24f, pad = size * 0.03f;
        using (var path = RoundedRect(new RectangleF(pad, pad, size - 2 * pad, size - 2 * pad), r))
        using (var fill = new SolidBrush(color))
            g.FillPath(fill, path);

        using var white = new SolidBrush(Color.White);
        using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        var box = new RectangleF(0, size * 0.03f, size, size);
        if (attention || mode == Mode.Unknown)
        {
            using var f = new Font("Segoe UI", size * 0.62f, FontStyle.Bold, GraphicsUnit.Pixel);
            g.DrawString("!", f, white, box, fmt);
        }
        else if (mode == Mode.Ai)
        {
            using var f = new Font("Segoe UI", size * 0.46f, FontStyle.Bold, GraphicsUnit.Pixel);
            g.DrawString(S.IconText, f, white, box, fmt);
        }
        else
        {
            // a gamepad: the body, a d-pad and two buttons
            float u = size / 128f;
            using (var body = RoundedRect(new RectangleF(16 * u, 38 * u, 96 * u, 54 * u), 26 * u)) g.FillPath(white, body);
            using var c = new SolidBrush(color);
            g.FillRectangle(c, 33 * u, 60 * u, 22 * u, 8 * u);
            g.FillRectangle(c, 40 * u, 53 * u, 8 * u, 22 * u);
            g.FillEllipse(c, 76 * u, 52 * u, 12 * u, 12 * u);
            g.FillEllipse(c, 90 * u, 64 * u, 12 * u, 12 * u);
        }
        return bmp;
    }

    public static Icon Icon(Mode mode, bool attention, int size)
    {
        using var bmp = Draw(mode, attention, size);
        IntPtr h = bmp.GetHicon();
        try
        {
            return (Icon)System.Drawing.Icon.FromHandle(h).Clone();
        }
        finally
        {
            DestroyIcon(h);
        }
    }

    static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
