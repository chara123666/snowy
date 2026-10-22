using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Snowy.UI;

namespace Snowy.UI.Controls;

public class RoundedButton : Button
{
    public int CornerRadius { get; set; } = 10;
    protected virtual Color NormalColor => Theme.Accent;
    protected virtual Color HoverColor => Theme.AccentGlow;

    public RoundedButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        ForeColor = Theme.Background;
        Font = Theme.FontMonoBold;
        Cursor = Cursors.Hand;
        Height = 38;
        BackColor = NormalColor;
    }

    protected override void OnMouseEnter(EventArgs e) { BackColor = HoverColor; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { BackColor = NormalColor; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnResize(EventArgs e) { base.OnResize(e); Theme.ApplyRoundedRegion(this, CornerRadius); }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(BackColor);
        using var path = Theme.RoundedRect(ClientRectangle, CornerRadius);
        pevent.Graphics.FillPath(brush, path);
        TextRenderer.DrawText(pevent.Graphics, Text, Font, ClientRectangle, ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

public class GhostButton : RoundedButton
{
    protected override Color NormalColor => Theme.Panel;
    protected override Color HoverColor => Theme.PanelAlt;
    private bool _hover;

    public GhostButton() { ForeColor = Theme.TextPrimary; }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var bg = new SolidBrush(BackColor);
        using var pen = new Pen(_hover ? Theme.Accent : Theme.AccentDim, 1.5f);
        using var path = Theme.RoundedRect(Rectangle.Inflate(ClientRectangle, -1, -1), CornerRadius);
        pevent.Graphics.FillPath(bg, path);
        pevent.Graphics.DrawPath(pen, path);
        TextRenderer.DrawText(pevent.Graphics, Text, Font, ClientRectangle, ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

public class OptionCard : GhostButton
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";

    protected override void OnPaint(PaintEventArgs pevent)
    {
        base.OnPaint(pevent);
        TextRenderer.DrawText(pevent.Graphics, Title, Theme.FontMonoBold,
            new Rectangle(16, 8, Width - 32, 22), Theme.Accent, TextFormatFlags.Left);
        TextRenderer.DrawText(pevent.Graphics, Description, Theme.FontBody,
            new Rectangle(16, 30, Width - 32, 22), Theme.TextDim, TextFormatFlags.Left);
    }
}

public class FireTextBox : Panel
{
    private readonly TextBox _box;
    private bool _focused;

    public event EventHandler? ValueChanged;
    public string PlaceholderText { get; set; } = "";

    public string Value
    {
        get => _box.Text;
        set => _box.Text = value;
    }

    public char PasswordChar
    {
        get => _box.PasswordChar;
        set => _box.PasswordChar = value;
    }

    public FireTextBox()
    {
        Height = 34;
        DoubleBuffered = true;
        BackColor = Theme.Background;
        Padding = new Padding(2, 6, 2, 6);

        _box = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Background,
            ForeColor = Theme.TextPrimary,
            Font = Theme.FontMono,
            Dock = DockStyle.Fill
        };
        _box.GotFocus += (_, _) => { _focused = true; Invalidate(); };
        _box.LostFocus += (_, _) => { _focused = false; Invalidate(); };
        _box.TextChanged += (_, e) => { ValueChanged?.Invoke(this, e); Invalidate(); };

        Controls.Add(_box);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(_focused ? Theme.Accent : Theme.AccentDim, 2f);
        e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);

        if (_box.Text.Length == 0 && !_focused && PlaceholderText.Length > 0)
        {
            using var b = new SolidBrush(Theme.TextDim);
            e.Graphics.DrawString(PlaceholderText, Theme.FontMono, b, new PointF(4, 6));
        }
    }
}

public class GradientPanel : Panel
{
    public GradientPanel()
    {
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint, true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
        => Theme.PaintVerticalGradient(e.Graphics, ClientRectangle);
}

public class GradientFlowPanel : FlowLayoutPanel
{
    public GradientFlowPanel()
    {
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint, true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
        => Theme.PaintVerticalGradient(e.Graphics, ClientRectangle);
}

public class DropZonePanel : Panel
{
    public event EventHandler<string>? FileDropped;
    private bool _hover;

    public DropZonePanel()
    {
        AllowDrop = true;
        DoubleBuffered = true;
        BackColor = Theme.Background;
        Cursor = Cursors.Hand;

        DragEnter += (_, e) =>
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                e.Effect = DragDropEffects.Copy;
                _hover = true;
                Invalidate();
            }
        };
        DragLeave += (_, _) => { _hover = false; Invalidate(); };
        DragDrop += (_, e) =>
        {
            _hover = false;
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
                FileDropped?.Invoke(this, files[0]);
            Invalidate();
        };
        Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog { Title = I18n.T("filepicker.title") };
            if (ofd.ShowDialog() == DialogResult.OK)
                FileDropped?.Invoke(this, ofd.FileName);
        };
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        using var path = Theme.RoundedRect(Rectangle.Inflate(ClientRectangle, -2, -2), 16);
        Region = new Region(path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var borderColor = _hover ? Theme.Accent : Theme.AccentDim;
        using var path = Theme.RoundedRect(Rectangle.Inflate(ClientRectangle, -2, -2), 16);
        using var bg = new SolidBrush(_hover ? Theme.PanelAlt : Theme.Panel);
        e.Graphics.FillPath(bg, path);

        using var pen = new Pen(borderColor, 2f) { DashStyle = DashStyle.Dash };
        e.Graphics.DrawPath(pen, path);

        TextRenderer.DrawText(e.Graphics, I18n.T("dropzone.text"), Theme.FontMono, ClientRectangle, Theme.TextPrimary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
    }
}

public class NavButton : Control
{
    private readonly Tween _hover;

    public string Label { get; set; } = "";
    public bool IsActive { get; set; }
    public event EventHandler? Activated;

    public NavButton()
    {
        DoubleBuffered = true;
        Height = 48;
        Cursor = Cursors.Hand;
        Font = Theme.FontMonoBold;
        Click += (_, _) => Activated?.Invoke(this, EventArgs.Empty);
        _hover = new Tween(0f, 8f, () => Invalidate());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _hover.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover.To(1f); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover.To(0f); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        float t = _hover.Value;
        var bg = IsActive ? Theme.Panel : Theme.Lerp(Theme.Background, Theme.PanelAlt, t);
        using var bgBrush = new SolidBrush(bg);
        e.Graphics.FillRectangle(bgBrush, ClientRectangle);

        var textColor = IsActive ? Theme.Accent : Theme.Lerp(Theme.TextDim, Theme.TextPrimary, t * 0.6f);
        var textRect = new Rectangle(20, 0, Width - 24, Height);
        TextRenderer.DrawText(e.Graphics, Label, Font, textRect, textColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
    }
}

public class HistoryEntryRow : Panel
{
    public HistoryEntryRow(string timestamp, string message, Color statusColor, string? filePath = null)
    {
        Height = 46;
        BackColor = Theme.Panel;

        var dot = new Label
        {
            Text = "●",
            Font = new Font("Segoe UI", 10F),
            ForeColor = statusColor,
            AutoSize = true,
            Location = new Point(16, 15)
        };
        var time = new Label
        {
            Text = timestamp,
            Font = Theme.FontMono,
            ForeColor = Theme.TextDim,
            AutoSize = true,
            Location = new Point(40, 14)
        };
        var msg = new Label
        {
            Text = message,
            Font = Theme.FontBody,
            ForeColor = Theme.TextPrimary,
            AutoSize = true,
            Location = new Point(118, 13)
        };

        Controls.Add(dot);
        Controls.Add(time);
        Controls.Add(msg);

        if (filePath != null)
        {
            var openBtn = new Label
            {
                Text = $"📂 {I18n.T("history.open")}",
                Font = Theme.FontBody,
                ForeColor = Theme.TextDim,
                AutoSize = true,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            openBtn.MouseEnter += (_, _) => openBtn.ForeColor = Theme.Accent;
            openBtn.MouseLeave += (_, _) => openBtn.ForeColor = Theme.TextDim;
            openBtn.Click += (_, _) => RevealInExplorer(filePath);
            Controls.Add(openBtn);
            openBtn.Location = new Point(Width - openBtn.PreferredWidth - 16, (Height - openBtn.PreferredHeight) / 2);
        }
    }

    private static void RevealInExplorer(string path)
    {
        try
        {
            if (File.Exists(path))
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
            else if (Directory.Exists(path))
                System.Diagnostics.Process.Start("explorer.exe", $"\"{path}\"");
        }
        catch
        {
        }
    }
}

public class PatternPad : Control
{
    private const int Grid = 3;
    private readonly PointF[] _nodes = new PointF[Grid * Grid];
    private readonly List<int> _path = new();
    private bool _dragging;
    private bool _laidOut;

    public IReadOnlyList<int> Sequence => _path;
    public event EventHandler? PatternCompleted;

    public PatternPad()
    {
        DoubleBuffered = true;
        BackColor = Theme.Background;
        Size = new Size(220, 220);
    }

    public void Reset() { _path.Clear(); Invalidate(); }

    protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutNodes(); }

    private void LayoutNodes()
    {
        const float pad = 30f;
        float stepX = (Width - pad * 2) / (Grid - 1);
        float stepY = (Height - pad * 2) / (Grid - 1);
        for (int row = 0; row < Grid; row++)
            for (int col = 0; col < Grid; col++)
                _nodes[row * Grid + col] = new PointF(pad + col * stepX, pad + row * stepY);
        _laidOut = true;
    }

    private int? HitTest(Point p)
    {
        const float radius = 18f;
        for (int i = 0; i < _nodes.Length; i++)
        {
            var dx = _nodes[i].X - p.X;
            var dy = _nodes[i].Y - p.Y;
            if (dx * dx + dy * dy <= radius * radius) return i;
        }
        return null;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _path.Clear();
        if (HitTest(e.Location) is int i) { _path.Add(i); _dragging = true; }
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging) return;
        if (HitTest(e.Location) is int i && !_path.Contains(i)) _path.Add(i);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
        if (_path.Count >= 2) PatternCompleted?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!_laidOut) LayoutNodes();
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        if (_path.Count > 1)
        {
            using var pen = new Pen(Theme.Accent, 3f);
            for (int i = 0; i < _path.Count - 1; i++)
                e.Graphics.DrawLine(pen, _nodes[_path[i]], _nodes[_path[i + 1]]);
        }

        for (int i = 0; i < _nodes.Length; i++)
        {
            bool active = _path.Contains(i);
            using var brush = new SolidBrush(active ? Theme.Accent : Theme.PanelAlt);
            using var pen = new Pen(active ? Theme.AccentGlow : Theme.AccentDim, 2f);
            var rect = new RectangleF(_nodes[i].X - 12, _nodes[i].Y - 12, 24, 24);
            e.Graphics.FillEllipse(brush, rect);
            e.Graphics.DrawEllipse(pen, rect);
        }
    }
}