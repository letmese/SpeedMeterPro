using System.Drawing.Drawing2D;
using SpeedMeterPro.Models;
using SpeedMeterPro.Services;
using System.Collections.Generic;

namespace SpeedMeterPro.Pages;

/// <summary>
/// Shared modern control factory (spec §3.3): cards on #232323 surfaces,
/// brand accent #4AF321 buttons, accent-ticked titles, hover feedback.
/// </summary>
public static class PageStyle
{
    public static GraphicsPath RoundedPath(Rectangle r, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>Page heading with an accent tick on the left.</summary>
    public static Control Title(string text)
    {
        var host = new Panel
        {
            Dock = DockStyle.Top,
            Height = 72,
            BackColor = Color.Transparent,
        };
        var bar = new Panel
        {
            BackColor = ThemeManager.Accent,
            Location = new Point(24, 20),
            Size = new Size(4, 32),
        };
        var label = new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = ThemeManager.Heading,
            Location = new Point(38, 16),
            AutoSize = true,
        };
        host.Controls.Add(bar);
        host.Controls.Add(label);
        return host;
    }

    public static Label Body(string text)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = ThemeManager.Text,
            AutoSize = true,
            MaximumSize = new Size(760, 0),
        };
    }

    /// <summary>Rounded card/section surface with header and accent underline.</summary>
    public static CardBox Card(string title) => new(title);

    public enum ButtonKind { Primary, Secondary, Danger }

    public static Button Button(string text, ButtonKind kind = ButtonKind.Primary)
    {
        return new RoundedButton
        {
            Text = text,
            Kind = kind,
        };
    }

    public static TextBox TextBox(string placeholder = "")
    {
        return new TextBox
        {
            BackColor = ThemeManager.BgAlt,
            ForeColor = ThemeManager.Text,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = placeholder,
        };
    }

    public static ComboBox Combo()
    {
        var c = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = ThemeManager.BgAlt,
            ForeColor = ThemeManager.Text,
            FlatStyle = FlatStyle.Flat,
        };
        return c;
    }

    public static CheckBox Check(string text, bool value = false)
    {
        return new CheckBox
        {
            Text = text,
            Checked = value,
            ForeColor = ThemeManager.Text,
            AutoSize = true,
        };
    }

    public static ListView ListView()
    {
        var lv = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            HideSelection = false,
            BackColor = ThemeManager.BgAlt,
            ForeColor = ThemeManager.Text,
            BorderStyle = BorderStyle.FixedSingle,
            HeaderStyle = ColumnHeaderStyle.Clickable,
            Font = new Font("Segoe UI", 9.5f),
        };
        var imgList = new ImageList { ImageSize = new Size(1, 26) };
        lv.SmallImageList = imgList;
        return lv;
    }

    /// <summary>Sidebar nav button: rounded, hover state, accent active state, optional lock badge.</summary>
    public static NavButton NavButton(string text)
    {
        return new NavButton { Text = text };
    }

    /// <summary>Sidebar nav button with a Font Awesome icon image rendered on the left.</summary>
    public static NavButton NavButton(string text, Image? icon)
    {
        return new NavButton { Text = text, Icon = icon };
    }

    /// <summary>
    /// Arranges a set of action buttons horizontally with a consistent, generous gap (default 16px).
    /// </summary>
    public static void LayoutButtonBar(int startX, int y, int gap, params Button[] buttons)
    {
        int x = startX;
        foreach (var b in buttons)
        {
            if (b is null) continue;
            b.Location = new Point(x, y);
            // Ensure preferred size is calculated with its text & padding
            var sz = b.GetPreferredSize(Size.Empty);
            b.Size = new Size(Math.Max(sz.Width, 90), 32);
            x += b.Width + gap;
        }
    }

    /// <summary>
    /// Stretches cards responsively across page width while preserving clean margins
    /// and expanding wide controls (ListViews, progress bars, text boxes) cleanly.
    /// </summary>
    public static void FillLayout(Control page, params Control[] cards)
    {
        var primary = new List<Control>();
        var side = new List<Control>();
        foreach (var c in cards)
        {
            if (c.Tag as string == "fixed") side.Add(c);
            else primary.Add(c);
        }

        var origChildWidth = new Dictionary<Control, int>();

        void Apply()
        {
            int totalAvailable = page.ClientSize.Width - 48;
            int sideWidth = side.Count > 0 ? side[0].Width : 0;
            int primaryWidth = side.Count > 0
                ? Math.Max(totalAvailable - sideWidth - 20, 460)
                : Math.Max(totalAvailable, 560);

            foreach (var card in primary)
            {
                card.Width = primaryWidth;
                card.Location = new Point(24, card.Location.Y);

                foreach (Control child in card.Controls)
                {
                    if (!origChildWidth.TryGetValue(child, out int cw))
                    {
                        cw = child.Width;
                        origChildWidth[child] = cw;
                    }

                    if (child is System.Windows.Forms.ListView or ProgressBar || (child is TextBox tb && tb.Multiline))
                    {
                        child.Width = Math.Max(card.Width - child.Left - 24, 200);
                    }
                }
            }

            if (side.Count > 0 && primary.Count > 0)
            {
                foreach (var fixedCard in side)
                {
                    fixedCard.Location = new Point(primary[0].Right + 20, primary[0].Top);
                }
            }
        }

        page.Resize += (_, _) => Apply();
        Apply();
    }
}

/// <summary>Rounded card surface drawn with Surface background, border and accent-ticked header.</summary>
public sealed class CardBox : GroupBox
{
    public CardBox(string title)
    {
        Text = title;
        ForeColor = ThemeManager.Heading;
        BackColor = ThemeManager.Surface;
        Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = PageStyle.RoundedPath(rect, 10);
        using (var b = new SolidBrush(ThemeManager.Surface))
        {
            g.FillPath(b, path);
        }
        using (var p = new Pen(ThemeManager.Border, 1))
        {
            g.DrawPath(p, path);
        }
        if (!string.IsNullOrEmpty(Text))
        {
            // Stitch Kinetic header style: accent circle badge + title
            using var badgeBrush = new SolidBrush(Color.FromArgb(40, ThemeManager.Accent));
            g.FillEllipse(badgeBrush, 24, 18, 10, 10);
            using var dotBrush = new SolidBrush(ThemeManager.Accent);
            g.FillEllipse(dotBrush, 26, 20, 6, 6);

            using var titleBrush = new SolidBrush(ThemeManager.Heading);
            using var titleFont = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            g.DrawString(Text, titleFont, titleBrush, 42, 14);

            // Subtle header separator line
            using var sep = new Pen(ThemeManager.BorderSubtle, 1);
            g.DrawLine(sep, 16, 44, Width - 16, 44);
        }
    }
}

/// <summary>Flat rounded button with hover/down feedback and dark text on brand accent.</summary>
public sealed class RoundedButton : Button
{
    private bool _hover;
    private bool _down;
    public PageStyle.ButtonKind Kind { get; set; } = PageStyle.ButtonKind.Primary;

    public RoundedButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Height = 32;
        AutoSize = true;
        Padding = new Padding(16, 0, 16, 0);
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        Color back, fore, border;
        if (!Enabled)
        {
            back = ThemeManager.BgAlt;
            fore = ThemeManager.TextDim;
            border = ThemeManager.Border;
        }
        else if (Kind == PageStyle.ButtonKind.Secondary)
        {
            back = _down ? ThemeManager.Surface : _hover ? ThemeManager.BgAlt : ThemeManager.Surface;
            fore = ThemeManager.Heading;
            border = _hover ? ThemeManager.Accent : ThemeManager.Border;
        }
        else if (Kind == PageStyle.ButtonKind.Danger)
        {
            back = _down ? Color.FromArgb(160, 40, 40) : _hover ? Color.FromArgb(220, 60, 60) : Color.FromArgb(180, 50, 50);
            fore = Color.White;
            border = _hover ? Color.FromArgb(240, 80, 80) : Color.Transparent;
        }
        else // Primary
        {
            back = _down ? ThemeManager.AccentDown : _hover ? ThemeManager.AccentHover : ThemeManager.Accent;
            fore = ThemeManager.Bg;
            border = Color.Transparent;
        }

        using var path = PageStyle.RoundedPath(rect, 7);
        using (var b = new SolidBrush(back))
        {
            g.FillPath(b, path);
        }
        if (border != Color.Transparent)
        {
            using var p = new Pen(border, 1);
            g.DrawPath(p, path);
        }
        TextRenderer.DrawText(g, Text, Font, rect, fore,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

/// <summary>Sidebar navigation item: rounded, hover feedback, accent-selected state, lock badge.</summary>
public sealed class NavButton : Control
{
    public bool Selected { get; set; }
    public bool ShowLock { get; set; }
    public Image? Icon { get; set; }
    public ContentAlignment TextAlign { get; set; } = ContentAlignment.MiddleLeft;
    private bool _hover;

    public NavButton()
    {
        Height = 42;
        Width = 190;
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 9.5f);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.Opaque, true);
        DoubleBuffered = true;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Cleanly erase background with sidebar color
        using (var bgBrush = new SolidBrush(ThemeManager.Sidebar))
        {
            g.FillRectangle(bgBrush, ClientRectangle);
        }

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        Color back = Selected ? ThemeManager.SurfaceHigh
            : _hover ? ThemeManager.SidebarHover
            : ThemeManager.Sidebar;
        Color fore = Selected ? ThemeManager.Accent : ThemeManager.Text;

        if (Selected || _hover)
        {
            using var path = PageStyle.RoundedPath(rect, 8);
            using (var b = new SolidBrush(back))
            {
                g.FillPath(b, path);
            }

            if (Selected)
            {
                // Stitch active left accent pill indicator
                using var activeBar = new SolidBrush(ThemeManager.Accent);
                g.FillRectangle(activeBar, 2, 8, 3, Height - 16);
                using var borderPen = new Pen(Color.FromArgb(60, ThemeManager.Accent), 1);
                g.DrawPath(borderPen, path);
            }
            else
            {
                using var borderPen = new Pen(ThemeManager.BorderSubtle, 1);
                g.DrawPath(borderPen, path);
            }
        }

        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
        var layout = new Rectangle(12, 0, Width - 16, Height);
        var iconRect = new Rectangle(8, (Height - 24) / 2, 24, 24);
        if (Icon is not null)
        {
            if (string.IsNullOrWhiteSpace(Text))
            {
                iconRect = new Rectangle((Width - 24) / 2, (Height - 24) / 2, 24, 24);
                layout = new Rectangle(0, 0, Width, Height);
            }
            else
            {
                layout = new Rectangle(40, 0, Width - 44, Height);
            }
            g.DrawImage(Icon, iconRect);
        }
        if (!Selected && ShowLock)
        {
            layout = new Rectangle(40, 0, Width - 68, Height);
            TextRenderer.DrawText(g, "🔒", Font, new Rectangle(Width - 24, 0, 18, Height),
                ThemeManager.TextDim, flags | TextFormatFlags.HorizontalCenter);
        }
        using var fontToUse = Selected ? new Font(Font, FontStyle.Bold) : new Font(Font, FontStyle.Regular);
        TextRenderer.DrawText(g, Text, fontToUse, layout, fore, flags);
    }
}
