using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Snowy.UI;

namespace Snowy.UI.Controls;

public class ToggleSwitch : Control
{
    private const int TrackHeight = 28;

    private bool _checked;
    private float _knobT;
    private bool _cooling;
    private float _cooldownT;
    private int _lastLineWidth = -1;
    private Action? _knobCancel;
    private Action? _cooldownCancel;

    public event EventHandler? CheckedChanged;

    public int CooldownMs { get; set; }

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;

            _knobCancel?.Invoke();
            float start = _knobT, end = _checked ? 1f : 0f;
            _knobCancel = Animator.Run(140, t => { _knobT = start + (end - start) * t; Invalidate(); });

            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public ToggleSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        Size = new Size(52, TrackHeight + 6);
        Cursor = Cursors.Hand;
        Click += (_, _) =>
        {
            if (_cooling) return;
            Checked = !Checked;
            if (CooldownMs > 0) StartCooldown(CooldownMs);
        };
    }

    private void StartCooldown(int ms)
    {
        _cooldownCancel?.Invoke();
        _cooling = true;
        _cooldownT = 0f;
        _lastLineWidth = -1;
        Invalidate();

        _cooldownCancel = Animator.Run(ms,
            t =>
            {
                _cooldownT = t;
                int w = (int)(Width * (1f - t));
                if (w != _lastLineWidth) { _lastLineWidth = w; Invalidate(); }
            },
            () => { _cooling = false; _cooldownT = 0f; Invalidate(); },
            eased: false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _knobCancel?.Invoke();
            _cooldownCancel?.Invoke();
        }
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float dim = _cooling ? 0.45f : 0f;

        var trackColor = Theme.Lerp(Theme.Lerp(Theme.PanelAlt, Theme.Accent, _knobT), Theme.Background, dim);
        using (var brush = new SolidBrush(trackColor))
        using (var path = Theme.RoundedRect(new Rectangle(0, 0, Width, TrackHeight), TrackHeight / 2))
            g.FillPath(brush, path);

        int knobD = TrackHeight - 6;
        float knobX = 3 + (Width - knobD - 6) * _knobT;
        using (var knob = new SolidBrush(Theme.Lerp(Color.White, Theme.TextDim, dim)))
            g.FillEllipse(knob, knobX, 3, knobD, knobD);

        if (_cooling)
        {
            int w = (int)(Width * (1f - _cooldownT));
            if (w > 0)
            {
                using var bar = new SolidBrush(Theme.AccentDim);
                g.FillRectangle(bar, 0, TrackHeight + 3, w, 2);
            }
        }
    }
}

public class LanguageDropdown : Control
{
    public static readonly (string Flag, string Name, AppLanguage Lang)[] Options =
    {
        ("🇬🇧", "english", AppLanguage.English),
        ("🇷🇺", "русский", AppLanguage.Russian),
        ("🇩🇪", "deutsch", AppLanguage.German),
    };

    private int _selected = 1;
    private bool _hover;
    private DropdownPopup? _popup;

    public event EventHandler? SelectionChanged;
    public AppLanguage SelectedLanguage => Options[_selected].Lang;

    public LanguageDropdown()
    {
        DoubleBuffered = true;
        Size = new Size(220, 40);
        Cursor = Cursors.Hand;
        Font = Theme.FontBody;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnClick(EventArgs e) { base.OnClick(e); TogglePopup(); }

    private void TogglePopup()
    {
        if (_popup is { IsDisposed: false })
        {
            _popup.CloseAnimated();
            _popup = null;
            return;
        }

        var screenPoint = PointToScreen(new Point(0, Height + 4));
        _popup = new DropdownPopup(Options, _selected, screenPoint, Width);
        _popup.OptionPicked += index =>
        {
            _selected = index;
            Invalidate();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        };
        _popup.FormClosed += (_, _) => _popup = null;
        _popup.Show(FindForm());
        _popup.Activate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var bg = new SolidBrush(_hover ? Theme.PanelAlt : Theme.Panel);
        using var path = Theme.RoundedRect(ClientRectangle, 8);
        e.Graphics.FillPath(bg, path);
        using var pen = new Pen(Theme.AccentDim, 1.2f);
        e.Graphics.DrawPath(pen, path);

        var (flag, name, _) = Options[_selected];
        var textRect = new Rectangle(14, 0, Width - 40, Height);
        TextRenderer.DrawText(e.Graphics, $"{flag}  {name}", Font, textRect, Theme.TextPrimary,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

        var chevronRect = new Rectangle(Width - 28, 0, 20, Height);
        TextRenderer.DrawText(e.Graphics, "▾", Theme.FontMonoBold, chevronRect, Theme.TextDim,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
    }
}

internal class DropdownPopup : Form
{
    public event Action<int>? OptionPicked;

    private readonly int _fullHeight;
    private bool _closing;

    public DropdownPopup((string Flag, string Name, AppLanguage Lang)[] options, int selected, Point screenLocation, int width)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Theme.Panel;
        Width = width;
        Location = screenLocation;
        Opacity = 0;

        const int rowHeight = 32;
        _fullHeight = options.Length * rowHeight + 8;
        Height = 0;

        for (int i = 0; i < options.Length; i++)
        {
            int idx = i;
            var (flag, name, _) = options[i];
            var row = new Label
            {
                Text = $"{flag}  {name}",
                Location = new Point(4, 4 + i * rowHeight),
                Size = new Size(width - 8, rowHeight),
                Margin = Padding.Empty,
                Font = Theme.FontBody,
                ForeColor = idx == selected ? Theme.Accent : Theme.TextPrimary,
                BackColor = Theme.Panel,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            row.MouseEnter += (_, _) => row.BackColor = Theme.PanelAlt;
            row.MouseLeave += (_, _) => row.BackColor = Theme.Panel;
            row.Click += (_, _) =>
            {
                OptionPicked?.Invoke(idx);
                CloseAnimated();
            };
            Controls.Add(row);
        }

        Deactivate += (_, _) => CloseAnimated();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Animator.Run(140, t =>
        {
            if (IsDisposed) return;
            Height = (int)(_fullHeight * t);
            Opacity = t;
        });
    }

    public void CloseAnimated()
    {
        if (IsDisposed || _closing) return;
        _closing = true;
        Animator.Run(100, t =>
        {
            if (IsDisposed) return;
            Opacity = Math.Max(0, 1 - t);
        }, () => { if (!IsDisposed) Close(); });
    }
}