using System.Drawing;
using System.Windows.Forms;

namespace Snowy.UI.Controls;

public sealed class SidebarNav : Control
{
    public const int ItemHeight = 48;

    private readonly string[] _labels;
    private readonly Tween[] _hover;
    private int _selected;
    private int _hot = -1;
    private float _indicatorY;
    private Action? _cancelSlide;

    public event EventHandler? SelectedChanged;

    public SidebarNav(params string[] labels)
    {
        _labels = (string[])labels.Clone();
        _hover = new Tween[_labels.Length];
        for (int i = 0; i < _hover.Length; i++)
            _hover[i] = new Tween(0f, 8f, () => Invalidate());

        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        Font = Theme.FontMonoBold;
        BackColor = Theme.Background;
    }

    public int SelectedIndex
    {
        get => _selected;
        set => Select(value, animate: true);
    }

    public void Select(int index, bool animate)
    {
        if (index < 0 || index >= _labels.Length) return;
        bool changed = index != _selected;
        _selected = index;

        _cancelSlide?.Invoke();
        _cancelSlide = null;

        float target = index * ItemHeight;
        if (!animate || !IsHandleCreated)
        {
            _indicatorY = target;
        }
        else if (Math.Abs(_indicatorY - target) > 0.5f)
        {
            float start = _indicatorY;
            _cancelSlide = Animator.Run(180,
                t => { _indicatorY = start + (target - start) * t; Invalidate(); },
                () => { _cancelSlide = null; _indicatorY = target; Invalidate(); });
        }

        Invalidate();
        if (changed) SelectedChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetLabel(int index, string text)
    {
        if (index < 0 || index >= _labels.Length) return;
        _labels[index] = text;
        Invalidate();
    }

    private int IndexAt(int y)
    {
        if (y < 0) return -1;
        int i = y / ItemHeight;
        return i < _labels.Length ? i : -1;
    }

    private void SetHot(int index)
    {
        if (index == _hot) return;
        if (_hot >= 0) _hover[_hot].To(0f);
        _hot = index;
        if (_hot >= 0) _hover[_hot].To(1f);
        Cursor = _hot >= 0 ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); SetHot(IndexAt(e.Y)); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); SetHot(-1); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left) return;
        int i = IndexAt(e.Y);
        if (i >= 0) Select(i, animate: true);
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.Background);

        using var brush = new SolidBrush(Color.Black);
        for (int i = 0; i < _labels.Length; i++)
        {
            var row = new Rectangle(0, i * ItemHeight, Width, ItemHeight);
            if (!row.IntersectsWith(e.ClipRectangle)) continue;

            float t = _hover[i].Value;
            bool active = i == _selected;

            brush.Color = active ? Theme.Panel : Theme.Lerp(Theme.Background, Theme.PanelAlt, t);
            g.FillRectangle(brush, row);

            var textColor = active ? Theme.Accent : Theme.Lerp(Theme.TextDim, Theme.TextPrimary, t * 0.6f);
            TextRenderer.DrawText(g, _labels[i], Font, new Rectangle(20, row.Y, Width - 24, ItemHeight), textColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        brush.Color = Theme.Accent;
        g.FillRectangle(brush, 0, (int)Math.Round(_indicatorY), 3, ItemHeight);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cancelSlide?.Invoke();
            foreach (var t in _hover) t.Dispose();
        }
        base.Dispose(disposing);
    }
}