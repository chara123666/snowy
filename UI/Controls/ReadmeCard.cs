using System.Drawing;
using System.Windows.Forms;

namespace Snowy.UI;

public class ReadmeCard : Panel
{
    public event EventHandler? CloseRequested;

    private readonly Label _close;
    private readonly Label _title;
    private readonly Label _body;

    public ReadmeCard()
    {
        BackColor = Theme.Background;
        DoubleBuffered = true;

        _close = new Label
        {
            Text = "✕",
            Font = new Font("Consolas", 18F, FontStyle.Bold),
            ForeColor = Theme.TextDim,
            AutoSize = true,
            Cursor = Cursors.Hand
        };
        _close.MouseEnter += (_, _) => _close.ForeColor = Theme.Accent;
        _close.MouseLeave += (_, _) => _close.ForeColor = Theme.TextDim;
        _close.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);

        _title = new Label
        {
            Text = "snowy",
            Font = Theme.FontDisplayLarge,
            ForeColor = Theme.Accent,
            AutoSize = true
        };

        _body = new Label
        {
            Text = I18n.T("readme.body"),
            Font = Theme.FontBody,
            ForeColor = Theme.TextPrimary,
            AutoSize = true,
            MaximumSize = new Size(400, 0),
            TextAlign = ContentAlignment.TopCenter
        };

        Controls.Add(_title);
        Controls.Add(_body);
        Controls.Add(_close);

        Resize += (_, _) => Reflow();
        Reflow();
    }

    private void Reflow()
    {
        _close.Location = new Point(Width - _close.PreferredWidth - 24, 20);

        int centerX = Width / 2;
        const int gap = 20;
        const int minMargin = 16;
        int totalHeight = _title.Height + gap + _body.Height;

        int startY = (Height - totalHeight) / 2;
        startY = Math.Max(minMargin, startY);
        startY = Math.Min(startY, Math.Max(minMargin, Height - totalHeight - minMargin));

        _title.Location = new Point(centerX - _title.PreferredWidth / 2, startY);
        _body.Location = new Point(centerX - _body.Width / 2, startY + _title.Height + gap);

        using var path = Theme.RoundedRect(ClientRectangle, 20);
        Region = new Region(path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(Theme.PanelAlt, 1.5f);
        using var path = Theme.RoundedRect(Rectangle.Inflate(ClientRectangle, -1, -1), 20);
        e.Graphics.DrawPath(pen, path);
    }
}