using System.Drawing;
using System.Windows.Forms;
using Snowy.Core;
using Snowy.UI;
using Snowy.UI.Controls;
using static Snowy.UI.Theme;

namespace Snowy.UI.Dialogs;

public class ModeSelectDialog : AnimatedForm
{
    public VaultMode SelectedMode { get; private set; } = VaultMode.Simple;

    public ModeSelectDialog(string fileName)
    {
        Text = "snowy";
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Theme.Background;
        ClientSize = new Size(448, 340);
        StartPosition = FormStartPosition.CenterParent;
        Theme.ApplyRoundedRegion(this, 18);

        var header = DialogChrome.Header(I18n.T("mode.title"), fileName);

        var body = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(24, 16, 24, 0),
            BackColor = Theme.Background
        };

        body.Controls.Add(BuildOption(I18n.T("mode.simple.title"), I18n.T("mode.simple.desc"), VaultMode.Simple));
        body.Controls.Add(BuildOption(I18n.T("mode.moderate.title"), I18n.T("mode.moderate.desc"), VaultMode.Moderate));
        body.Controls.Add(BuildOption(I18n.T("mode.protected.title"), I18n.T("mode.protected.desc"), VaultMode.Protected));

        Controls.Add(body);
        Controls.Add(header);
    }

    private Control BuildOption(string title, string desc, VaultMode mode)
    {
        var card = new OptionCard
        {
            Width = 400,
            Height = 64,
            Margin = new Padding(0, 6, 0, 6),
            Title = title,
            Description = desc
        };
        card.Click += (_, _) => { SelectedMode = mode; DialogResult = DialogResult.OK; };
        return card;
    }
}

public class PasswordSetupDialog : AnimatedForm
{
    public string Password { get; private set; } = "";
    private readonly FireTextBox _passwordBox;
    private readonly Label _status;

    public PasswordSetupDialog()
    {
        Text = "snowy";
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Theme.Background;
        ClientSize = new Size(448, 300);
        StartPosition = FormStartPosition.CenterParent;
        Theme.ApplyRoundedRegion(this, 18);

        var header = DialogChrome.Header(I18n.T("pwdsetup.title"), I18n.T("pwdsetup.subtitle"));

        var body = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(24, 16, 24, 0),
            BackColor = Theme.Background
        };

        _passwordBox = new FireTextBox { Width = 400, PlaceholderText = I18n.T("pwdsetup.placeholder"), Margin = new Padding(0, 0, 0, 8) };
        _status = new Label { Width = 400, Height = 22, Font = Theme.FontBody, Margin = new Padding(0, 0, 0, 10) };
        var generate = new GhostButton { Text = I18n.T("pwdsetup.generate"), Width = 400 };

        generate.Click += (_, _) => { _passwordBox.Value = CryptoUtils.GenerateStrongPassword(40); };
        _passwordBox.ValueChanged += (_, _) => UpdateStatus();

        body.Controls.Add(_passwordBox);
        body.Controls.Add(_status);
        body.Controls.Add(generate);

        var footer = DialogChrome.Footer(out var cancelBtn, out var confirmBtn, I18n.T("action.encrypt"));
        cancelBtn.Click += (_, _) => DialogResult = DialogResult.Cancel;
        confirmBtn.Click += (_, _) =>
        {
            if (_passwordBox.Value.Length != 40)
            {
                _status.Text = I18n.T("pwdsetup.length_error", _passwordBox.Value.Length);
                _status.ForeColor = Theme.Danger;
                return;
            }
            Password = _passwordBox.Value;
            DialogResult = DialogResult.OK;
        };

        Controls.Add(body);
        Controls.Add(footer);
        Controls.Add(header);

        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var len = _passwordBox.Value.Length;
        _status.Text = I18n.T("pwdsetup.length_status", len);
        _status.ForeColor = len == 40 ? Theme.Success : Theme.TextDim;
    }
}

public class PuzzleSetupDialog : AnimatedForm
{
    public PuzzleKind Kind { get; private set; }
    public string Hint { get; private set; } = "";
    public string Answer { get; private set; } = "";

    private readonly Panel _questionPanel;
    private readonly Panel _patternPanel;
    private readonly FireTextBox _questionBox;
    private readonly FireTextBox _answerBox;
    private readonly PatternPad _pad;
    private readonly Label _patternStatus;

    private PuzzleKind _selectedKind = PuzzleKind.Question;
    private string? _firstPattern;
    private bool _patternConfirmed;

    public PuzzleSetupDialog()
    {
        Text = "snowy";
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Theme.Background;
        ClientSize = new Size(448, 510);
        StartPosition = FormStartPosition.CenterParent;
        Theme.ApplyRoundedRegion(this, 18);

        var header = DialogChrome.Header(I18n.T("puzzlesetup.title"), I18n.T("puzzlesetup.subtitle"));

        var tabs = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(24, 10, 0, 0) };
        var qTab = new GhostButton { Text = I18n.T("puzzlesetup.tab_question"), Width = 195, Margin = new Padding(0) };
        var pTab = new GhostButton { Text = I18n.T("puzzlesetup.tab_pattern"), Width = 195, Margin = new Padding(10, 0, 0, 0) };
        tabs.Controls.Add(qTab);
        tabs.Controls.Add(pTab);

        var body = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(24, 6, 24, 0),
            BackColor = Theme.Background
        };

        _questionPanel = new Panel { Size = new Size(400, 170), BackColor = Theme.Background };
        _questionBox = new FireTextBox { Width = 400, Location = new Point(0, 0), PlaceholderText = I18n.T("puzzlesetup.question_placeholder") };
        _answerBox = new FireTextBox { Width = 400, Location = new Point(0, 70), PlaceholderText = I18n.T("puzzlesetup.answer_placeholder"), PasswordChar = '•' };
        _questionPanel.Controls.Add(_questionBox);
        _questionPanel.Controls.Add(_answerBox);

        _patternPanel = new Panel { Size = new Size(400, 280), BackColor = Theme.Background, Visible = false };
        _pad = new PatternPad { Location = new Point(90, 0) };
        _patternStatus = new Label
        {
            Location = new Point(0, 230),
            Size = new Size(400, 40),
            ForeColor = Theme.TextDim,
            Font = Theme.FontBody,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = I18n.T("puzzlesetup.pattern_hint")
        };
        _pad.PatternCompleted += (_, _) => HandlePatternDraw();
        _patternPanel.Controls.Add(_pad);
        _patternPanel.Controls.Add(_patternStatus);

        body.Controls.Add(_questionPanel);
        body.Controls.Add(_patternPanel);

        qTab.Click += (_, _) => SwitchKind(PuzzleKind.Question);
        pTab.Click += (_, _) => SwitchKind(PuzzleKind.Pattern);

        var footer = DialogChrome.Footer(out var cancelBtn, out var confirmBtn, I18n.T("action.encrypt"));
        cancelBtn.Click += (_, _) => DialogResult = DialogResult.Cancel;
        confirmBtn.Click += (_, _) => TryConfirm();

        Controls.Add(body);
        Controls.Add(footer);
        Controls.Add(tabs);
        Controls.Add(header);
    }

    private void SwitchKind(PuzzleKind kind)
    {
        _selectedKind = kind;
        _questionPanel.Visible = kind == PuzzleKind.Question;
        _patternPanel.Visible = kind == PuzzleKind.Pattern;
        if (kind == PuzzleKind.Pattern)
        {
            _pad.Reset();
            _firstPattern = null;
            _patternConfirmed = false;
            _patternStatus.Text = I18n.T("puzzlesetup.pattern_hint");
            _patternStatus.ForeColor = Theme.TextDim;
        }
    }

    private void HandlePatternDraw()
    {
        if (_pad.Sequence.Count < 4)
        {
            _patternStatus.Text = I18n.T("puzzlesetup.pattern_too_short");
            _patternStatus.ForeColor = Theme.Danger;
            return;
        }

        var encoded = PatternCodec.Encode(_pad.Sequence);

        if (_firstPattern is null)
        {
            _firstPattern = encoded;
            _patternStatus.Text = I18n.T("puzzlesetup.pattern_first");
            _patternStatus.ForeColor = Theme.TextDim;
        }
        else if (_firstPattern == encoded)
        {
            _patternConfirmed = true;
            _patternStatus.Text = I18n.T("puzzlesetup.pattern_confirmed");
            _patternStatus.ForeColor = Theme.Success;
        }
        else
        {
            _firstPattern = null;
            _patternConfirmed = false;
            _patternStatus.Text = I18n.T("puzzlesetup.pattern_mismatch");
            _patternStatus.ForeColor = Theme.Danger;
        }
    }

    private void TryConfirm()
    {
        if (_selectedKind == PuzzleKind.Question)
        {
            if (_questionBox.Value.Trim().Length == 0 || _answerBox.Value.Trim().Length == 0)
            {
                MessageBox.Show(this, I18n.T("puzzlesetup.warn_fill_both"), "snowy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Kind = PuzzleKind.Question;
            Hint = _questionBox.Value.Trim();
            Answer = _answerBox.Value.Trim();
        }
        else
        {
            if (!_patternConfirmed || _firstPattern is null)
            {
                MessageBox.Show(this, I18n.T("puzzlesetup.warn_confirm_pattern"), "snowy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Kind = PuzzleKind.Pattern;
            Hint = I18n.T("puzzlesetup.stored_hint");
            Answer = _firstPattern;
        }

        DialogResult = DialogResult.OK;
    }
}