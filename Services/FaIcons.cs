using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Reflection;
using SpeedMeterPro.Models;

namespace SpeedMeterPro.Services;

/// <summary>
/// Renders the original Font Awesome 6 Solid glyph set (embedded from the
/// decompiled dump's NetSpeedMeterPlus.Resources.Fonts.fa-solid-900.ttf) as
/// text and colored bitmaps for the sidebar, header and the 27-item tray menu.
/// </summary>
public static class FaIcons
{
    private static readonly PrivateFontCollection Fonts = new();
    private static readonly Font GlyphFont;
    private static bool _loaded;

    static FaIcons()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("SpeedMeterPro.Assets.fa-solid-900.ttf");
            if (stream is null)
            {
                GlyphFont = new Font("Segoe UI Symbol", 14f, FontStyle.Regular, GraphicsUnit.Pixel);
                return;
            }
            var data = new byte[stream.Length];
            stream.Read(data, 0, data.Length);
            unsafe
            {
                fixed (byte* p = data)
                {
                    Fonts.AddMemoryFont((System.IntPtr)p, data.Length);
                }
            }
            _loaded = Fonts.Families.Length > 0;
            GlyphFont = _loaded
                ? new Font(Fonts.Families[0], 14f, FontStyle.Regular, GraphicsUnit.Pixel)
                : new Font("Segoe UI Symbol", 14f, FontStyle.Regular, GraphicsUnit.Pixel);
        }
        catch
        {
            _loaded = false;
            GlyphFont = new Font("Segoe UI Symbol", 14f, FontStyle.Regular, GraphicsUnit.Pixel);
        }
    }

    public static bool Available => _loaded;

    /// <summary>The single glyph character for an icon.</summary>
    public static string Glyph(IconChar icon)
    {
        if (icon == IconChar.None) return "";
        return ((char)(int)icon).ToString();
    }

    /// <summary>Render a glyph to a colored, antialiased bitmap (for menus and nav buttons).</summary>
    public static Bitmap Bitmap(IconChar icon, int size, Color color)
    {
        var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            using var brush = new SolidBrush(color);
            using var font = new Font(Fonts.Families.Length > 0 ? Fonts.Families[0] : GlyphFont.FontFamily,
                size * 0.72f, FontStyle.Regular, GraphicsUnit.Pixel);
            var s = Glyph(icon);
            var rect = new RectangleF(0, 0, size, size);
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            g.DrawString(s, font, brush, rect, format);
        }
        return bmp;
    }
}
