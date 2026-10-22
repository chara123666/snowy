using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Snowy.UI.Controls;

namespace Snowy.UI;

public static class Theme
{
    private static Palette _current = Palettes.Snowy;

    public static Palette Current => _current;
    internal static void SetCurrent(Palette palette) => _current = palette;

    public static Color Background => _current[Slot.Background];
    public static Color Panel => _current[Slot.Panel];
    public static Color PanelAlt => _current[Slot.PanelAlt];
    public static Color Accent => _current[Slot.Accent];
    public static Color AccentDim => _current[Slot.AccentDim];
    public static Color AccentGlow => _current[Slot.AccentGlow];
    public static Color TextPrimary => _current[Slot.TextPrimary];
    public static Color TextDim => _current[Slot.TextDim];
    public static Color Success => _current[Slot.Success];
    public static Color Danger => _current[Slot.Danger];
    public static Color GradientTop => _current[Slot.GradientTop];
    public static Color GradientBottom => _current[Slot.GradientBottom];

    public static readonly Font FontTitle = new("Consolas", 13F, FontStyle.Bold);
    public static readonly Font FontBody = new("Consolas", 10F);
    public static readonly Font FontMono = new("Cascadia Mono", 9.5F);
    public static readonly Font FontMonoBold = new("Cascadia Mono", 9.5F, FontStyle.Bold);

    public static readonly Font FontDisplaySmall = MakeDisplayFont(20F);
    public static readonly Font FontDisplayLarge = MakeDisplayFont(64F);

    private static Font MakeDisplayFont(float size) => CustomFonts.Lancelot is { } family
        ? new Font(family, size)
        : new Font("Segoe Script", size * 0.75f, FontStyle.Bold);

    public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        if (d <= 0) { path.AddRectangle(bounds); return path; }

        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void ApplyRoundedRegion(Control control, int radius)
    {
        using var path = RoundedRect(new Rectangle(0, 0, Math.Max(control.Width, 1), Math.Max(control.Height, 1)), radius);
        control.Region = new Region(path);
    }

    public static Color Lerp(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return Color.FromArgb(
            (int)(a.A + (b.A - a.A) * t),
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }

    public static void PaintVerticalGradient(Graphics g, Rectangle bounds, Color? top = null, Color? bottom = null)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        Color c0 = top ?? GradientTop, c1 = bottom ?? GradientBottom;

        int maxDelta = Math.Max(Math.Abs(c1.R - c0.R), Math.Max(Math.Abs(c1.G - c0.G), Math.Abs(c1.B - c0.B)));
        int steps = Math.Min(Math.Clamp(maxDelta * 2, 2, 160), bounds.Height);

        var clip = g.ClipBounds;
        int clipTop = (int)Math.Floor(Math.Max(clip.Top, bounds.Top));
        int clipBottom = (int)Math.Ceiling(Math.Min(clip.Bottom, bounds.Bottom));

        using var brush = new SolidBrush(c0);
        for (int i = 0; i < steps; i++)
        {
            int y0 = bounds.Y + (int)((long)bounds.Height * i / steps);
            int y1 = bounds.Y + (int)((long)bounds.Height * (i + 1) / steps);
            if (y1 <= clipTop) continue;
            if (y0 >= clipBottom) break;

            brush.Color = Lerp(c0, c1, (i + 0.5f) / steps);
            g.FillRectangle(brush, bounds.X, y0, bounds.Width, y1 - y0);
        }
    }

    public static class WindowDrag
    {
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        public static void Begin(Form? form, MouseEventArgs e)
        {
            if (form is null || e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(form.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        }
    }

    public static class DialogChrome
    {
        public static Panel Header(string title, string subtitle)
        {
            var panel = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Theme.Background };
            var lbl = new Label
            {
                Text = $"{title}\n{subtitle}",
                Dock = DockStyle.Fill,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontMonoBold,
                Padding = new Padding(24, 12, 0, 0)
            };
            panel.Controls.Add(lbl);

            void Drag(object? _, MouseEventArgs e) => WindowDrag.Begin(panel.FindForm(), e);
            panel.MouseDown += Drag;
            lbl.MouseDown += Drag;
            return panel;
        }

        public static Panel Footer(out GhostButton cancelButton, out RoundedButton confirmButton, string confirmText)
        {
            var panel = new Panel { Dock = DockStyle.Bottom, Height = 70, BackColor = Theme.Background };
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 14, 24, 0)
            };
            confirmButton = new RoundedButton { Text = confirmText, Width = 180 };
            cancelButton = new GhostButton { Text = I18n.T("common.cancel"), Width = 120, Margin = new Padding(10, 0, 0, 0) };
            flow.Controls.Add(confirmButton);
            flow.Controls.Add(cancelButton);
            panel.Controls.Add(flow);
            return panel;
        }
    }
}