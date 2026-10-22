using System.Drawing;
using System.Windows.Forms;
using Snowy.Core;
using Snowy.UI;
using Snowy.UI.Controls;
using static Snowy.UI.Theme;

namespace Snowy.UI.Dialogs;

public class PasswordPromptDialog : AnimatedForm
{
    public string Password { get; private set; } = "";

    public PasswordPromptDialog(int attemptsLeft)
    {
        Text = "snowy";
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Theme.Background;
        ClientSize = new Size(448, 250);
        StartPosition = FormStartPosition.CenterParent;
        Theme.ApplyRoundedRegion(this, 18);

        var header = DialogChrome.Header(I18n.T("pwdprompt.title"), I18n.T("pwdprompt.subtitle"));

        var body = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(24, 16, 24, 0),
            BackColor = Theme.Background
        };

        var box = new FireTextBox { Width = 400, PlaceholderText = I18n.T("pwdprompt.placeholder"), PasswordChar = '•', Margin = new Padding(0, 0, 0, 8) };
        var status = new Label
        {
            Width = 400,
            Height = 24,
            Font = Theme.FontBody,
            Text = I18n.T("pwdprompt.attempts", attemptsLeft),
            ForeColor = attemptsLeft < 3 ? Theme.Danger : Theme.TextDim
        };

        body.Controls.Add(box);
        body.Controls.Add(status);

        var footer = DialogChrome.Footer(out var cancelBtn, out var confirmBtn, I18n.T("action.decrypt"));
        cancelBtn.Click += (_, _) => DialogResult = DialogResult.Cancel;
        confirmBtn.Click += (_, _) => { Password = box.Value; DialogResult = DialogResult.OK; };

        Controls.Add(body);
        Controls.Add(footer);
        Controls.Add(header);
    }
}

public class PuzzleSolveDialog : AnimatedForm
{
    public string Answer { get; private set; } = "";
    private readonly FireTextBox? _answerBox;
    private readonly PatternPad? _pad;

    public PuzzleSolveDialog(VaultHeader header, bool showWrongAnswerNotice)
    {
        Text = "snowy";
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Theme.Background;
        ClientSize = new Size(448, header.PuzzleKind == PuzzleKind.Pattern ? 420 : 260);
        StartPosition = FormStartPosition.CenterParent;
        Theme.ApplyRoundedRegion(this, 18);

        var headerPanel = DialogChrome.Header(I18n.T("puzzlesolve.title"), header.PuzzleHint);

        var body = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(24, 16, 24, 0),
            BackColor = Theme.Background
        };

        if (showWrongAnswerNotice)
        {
            body.Controls.Add(new Label
            {
                Width = 400,
                Height = 24,
                Font = Theme.FontBody,
                Text = I18n.T("puzzlesolve.wrong_notice"),
                ForeColor = Theme.Danger
            });
        }

        if (header.PuzzleKind == PuzzleKind.Question)
        {
            _answerBox = new FireTextBox { Width = 400, PlaceholderText = I18n.T("puzzlesolve.answer_placeholder"), Margin = new Padding(0, 6, 0, 0) };
            body.Controls.Add(_answerBox);
        }
        else
        {
            _pad = new PatternPad { Margin = new Padding(90, 10, 0, 0) };
            body.Controls.Add(_pad);
        }

        var footer = DialogChrome.Footer(out var cancelBtn, out var confirmBtn, I18n.T("action.decrypt"));
        cancelBtn.Click += (_, _) => DialogResult = DialogResult.Cancel;
        confirmBtn.Click += (_, _) =>
        {
            Answer = header.PuzzleKind == PuzzleKind.Question ? _answerBox!.Value : PatternCodec.Encode(_pad!.Sequence);
            DialogResult = DialogResult.OK;
        };

        Controls.Add(body);
        Controls.Add(footer);
        Controls.Add(headerPanel);
    }
}

public class ExtractingProgressForm : AnimatedForm
{
    private readonly ProgressBar _bar;
    private readonly System.Windows.Forms.Timer _timer;
    private int _tick;

    public ExtractingProgressForm(string fileName)
    {
        Text = "snowy";
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Theme.Background;
        ClientSize = new Size(448, 150);
        StartPosition = FormStartPosition.CenterParent;
        ControlBox = false;
        Theme.ApplyRoundedRegion(this, 18);

        var title = new Label
        {
            Text = I18n.T("progress.title"),
            Dock = DockStyle.Top,
            Height = 40,
            ForeColor = Theme.TextPrimary,
            Font = Theme.FontMonoBold,
            Padding = new Padding(24, 12, 0, 0)
        };
        var status = new Label
        {
            Text = $"> {fileName}",
            Dock = DockStyle.Top,
            Height = 26,
            ForeColor = Theme.TextDim,
            Font = Theme.FontMono,
            Padding = new Padding(24, 0, 0, 0)
        };
        var barHost = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(24, 8, 24, 0) };
        _bar = new ProgressBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100 };
        barHost.Controls.Add(_bar);

        Controls.Add(barHost);
        Controls.Add(status);
        Controls.Add(title);

        _timer = new System.Windows.Forms.Timer { Interval = 40 };
        _timer.Tick += (_, _) => { _tick = (_tick + 3) % 100; _bar.Value = _tick; };
    }

    protected override void OnShown(EventArgs e) { base.OnShown(e); _timer.Start(); }
    protected override void OnFormClosed(FormClosedEventArgs e) { _timer.Stop(); _timer.Dispose(); base.OnFormClosed(e); }
}