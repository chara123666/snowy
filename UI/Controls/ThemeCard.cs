using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Snowy.UI.Controls;

public class ThemeCard : Control
{
    private static readonly Size PreviewBitmapSize = new(90, 58);

    private readonly Palette _palette;
    private readonly Tween _hover;
    private Bitmap? _preview;

    public event EventHandler? Picked;

    public ThemeCard(Palette palette)
    {
        _palette = palette;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        Size = new Size(500, 84);
        Cursor = Cursors.Hand;

        _hover = new Tween(0f, 9f, () => Invalidate());
        Click += (_, _) => Picked?.Invoke(this, EventArgs.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hover.Dispose();
            _preview?.Dispose();
            _preview = null;
        }
        base.Dispose(disposing);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover.To(1f); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover.To(0f); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        bool selected = ReferenceEquals(ThemeManager.Target, _palette);
        float hover = _hover.Value;

        var cardRect = Rectangle.Inflate(ClientRectangle, -1, -1);
        using (var path = Theme.RoundedRect(cardRect, 12))
        using (var fill = new SolidBrush(Theme.Lerp(Theme.Panel, Theme.PanelAlt, hover)))
        using (var border = new Pen(
            selected ? Theme.Accent : Theme.Lerp(Theme.AccentDim, Theme.Accent, hover * 0.7f),
            selected ? 2f : 1.5f))
        {
            g.FillPath(fill, path);
            g.DrawPath(border, path);
        }

        g.DrawImageUnscaled(GetPreview(), 13, 13);

        var titleRect = new Rectangle(118, 14, Width - 118 - 52, 22);
        TextRenderer.DrawText(g, I18n.T(_palette.NameKey), Theme.FontMonoBold, titleRect, Theme.TextPrimary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        var descRect = new Rectangle(118, 38, Width - 118 - 52, 38);
        TextRenderer.DrawText(g, I18n.T(_palette.DescKey), Theme.FontBody, descRect, Theme.TextDim,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak);

        if (selected)
        {
            using var pen = new Pen(Theme.Accent, 2.5f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            int cx = Width - 36, cy = Height / 2;
            g.DrawLines(pen, new[] { new Point(cx - 8, cy), new Point(cx - 2, cy + 6), new Point(cx + 9, cy - 7) });
        }
    }

    private Bitmap GetPreview()
    {
        if (_preview != null) return _preview;

        var bmp = new Bitmap(PreviewBitmapSize.Width, PreviewBitmapSize.Height, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            DrawPreview(g, new Rectangle(1, 1, PreviewBitmapSize.Width - 2, PreviewBitmapSize.Height - 2));
        }
        return _preview = bmp;
    }

    private void DrawPreview(Graphics g, Rectangle sw)
    {
        using var swPath = Theme.RoundedRect(sw, 8);
        var state = g.Save();
        g.SetClip(swPath);

        Theme.PaintVerticalGradient(g, sw, _palette[Slot.GradientTop], _palette[Slot.GradientBottom]);

        using (var b = new SolidBrush(_palette[Slot.Background]))
            g.FillRectangle(b, sw.X, sw.Y, 22, sw.Height);
        using (var b = new SolidBrush(_palette[Slot.Accent]))
            g.FillRectangle(b, sw.X, sw.Y + 8, 3, 10);
        using (var b = new SolidBrush(_palette[Slot.TextDim]))
        {
            g.FillRectangle(b, sw.X + 8, sw.Y + 10, 10, 2);
            g.FillRectangle(b, sw.X + 8, sw.Y + 24, 10, 2);
            g.FillRectangle(b, sw.X + 8, sw.Y + 38, 10, 2);
        }

        var dz = new Rectangle(sw.X + 30, sw.Y + 9, 50, 26);
        using (var dzPath = Theme.RoundedRect(dz, 5))
        using (var b = new SolidBrush(_palette[Slot.Panel]))
        using (var p = new Pen(_palette[Slot.AccentDim], 1f) { DashStyle = DashStyle.Dash })
        {
            g.FillPath(b, dzPath);
            g.DrawPath(p, dzPath);
        }

        var pill = new Rectangle(sw.X + 30, sw.Y + 41, 28, 8);
        using (var pillPath = Theme.RoundedRect(pill, 4))
        using (var b = new SolidBrush(_palette[Slot.Accent]))
            g.FillPath(b, pillPath);
        using (var b = new SolidBrush(_palette[Slot.TextDim]))
            g.FillRectangle(b, sw.X + 62, sw.Y + 44, 18, 2);

        g.Restore(state);

        using var outline = new Pen(_palette[Slot.AccentDim], 1.5f);
        g.DrawPath(outline, swPath);
    }
}