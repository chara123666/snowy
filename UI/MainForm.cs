using System.Drawing;
using System.Windows.Forms;
using Snowy.Core;
using Snowy.UI.Controls;
using Snowy.UI.Dialogs;

namespace Snowy.UI;

public class MainForm : AnimatedForm
{
    private DropZonePanel _dropZone = null!;
    private Label _fileLabel = null!;
    private Label _disclaimerLabel = null!;

    private Panel _historyContainer = null!;
    private Label _historyEmpty = null!;
    private readonly List<HistoryEntryRow> _historyRows = new();

    private Label _themesHeading = null!;
    private readonly List<ThemeCard> _themeCards = new();

    private readonly SnowEngine _snow = new();
    private Label _langSettingLabel = null!;
    private Label _snowSettingLabel = null!;
    private ToggleSwitch _snowToggle = null!;

    private SidebarNav _nav = null!;
    private Panel _host = null!;
    private Panel _encryptPanel = null!, _historyPanel = null!, _themesPanel = null!, _settingsPanel = null!;
    private Panel[] _tabPanels = null!;
    private Panel? _currentPanel;
    private FormWindowState _lastWindowState = FormWindowState.Normal;
    private bool _repaintQueued;

    public MainForm()
    {
        Text = "snowy";
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;
        BackColor = Theme.Background;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
        ClientSize = new Size(780, 600);
        MinimumSize = new Size(720, 480);
        StartPosition = FormStartPosition.CenterScreen;

        _nav = new SidebarNav(
            I18n.T("tab.encrypt"), I18n.T("tab.history"), I18n.T("tab.themes"), I18n.T("tab.settings"))
        {
            Dock = DockStyle.Left,
            Width = 150
        };

        _host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };

        _encryptPanel = BuildEncryptPanel();
        _historyPanel = BuildHistoryPanel();
        _themesPanel = BuildThemesPanel();
        _settingsPanel = BuildSettingsPanel();
        _tabPanels = new[] { _encryptPanel, _historyPanel, _themesPanel, _settingsPanel };

        _snow.AddTarget(_encryptPanel);
        _snow.AddTarget(_themesPanel);
        _snow.AddTarget(_settingsPanel);
        _snow.AddTarget(_historyContainer, scrolls: true);

        Controls.Add(_host);
        Controls.Add(_nav);

        _host.Resize += (_, _) => _snow.SetBounds(_host.ClientSize);
        _snow.SetBounds(_host.ClientSize);

        _nav.SelectedChanged += (_, _) => ShowTab(_nav.SelectedIndex);
        ShowTab(0);

        I18n.LanguageChanged += RefreshLocalizedText;
        FormClosed += (_, _) =>
        {
            I18n.LanguageChanged -= RefreshLocalizedText;
            foreach (var panel in _tabPanels)
                if (!ReferenceEquals(panel, _currentPanel)) panel.Dispose();
            _snow.Dispose();
            SoundFx.Shutdown();
        };

        Shown += (_, _) => ShowReadmeCard();
    }

    private void ShowTab(int index)
    {
        var panel = _tabPanels[index];
        if (ReferenceEquals(panel, _currentPanel)) return;

        _host.SuspendLayout();
        if (_currentPanel != null) _host.Controls.Remove(_currentPanel);
        panel.Dock = DockStyle.Fill;
        _host.Controls.Add(panel);
        _currentPanel = panel;
        _host.ResumeLayout(true);
        panel.Invalidate(true);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (WindowState == _lastWindowState) return;
        _lastWindowState = WindowState;
        QueueFullRepaint();
    }

    protected override void OnResizeEnd(EventArgs e)
    {
        base.OnResizeEnd(e);
        QueueFullRepaint();
    }

    private void QueueFullRepaint()
    {
        if (_repaintQueued || !IsHandleCreated) return;
        _repaintQueued = true;
        BeginInvoke(new Action(() =>
        {
            _repaintQueued = false;
            if (!IsDisposed) _currentPanel?.Invalidate(true);
        }));
    }

    private Panel BuildEncryptPanel()
    {
        var panel = new GradientFlowPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(24)
        };

        _dropZone = new DropZonePanel { Width = 500, Height = 200 };
        _fileLabel = new Label
        {
            Width = 500,
            Height = 24,
            BackColor = Color.Transparent,
            ForeColor = Theme.TextDim,
            Font = Theme.FontMono,
            Margin = new Padding(0, 8, 0, 12)
        };
        _disclaimerLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(500, 0),
            BackColor = Color.Transparent,
            ForeColor = Theme.TextDim,
            Font = Theme.FontBody,
            Text = I18n.T("disclaimer")
        };

        panel.Controls.Add(_dropZone);
        panel.Controls.Add(_fileLabel);
        panel.Controls.Add(_disclaimerLabel);

        _dropZone.FileDropped += (_, path) => HandleDroppedFile(path);
        return panel;
    }

    private Panel BuildHistoryPanel()
    {
        var outer = new BufferedPanel { Dock = DockStyle.Fill, BackColor = Theme.Background };

        _historyContainer = new BufferedPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Background };
        _historyEmpty = new Label
        {
            Text = I18n.T("history.empty"),
            AutoSize = true,
            Location = new Point(24, 24),
            ForeColor = Theme.TextDim,
            Font = Theme.FontBody
        };

        _historyContainer.Controls.Add(_historyEmpty);
        _historyContainer.Resize += (_, _) => RelayoutHistory();
        outer.Controls.Add(_historyContainer);
        return outer;
    }

    private void AddHistoryEntry(string message, Color color, string? filePath = null)
    {
        var row = new HistoryEntryRow(DateTime.Now.ToString("HH:mm:ss"), message, color, filePath)
        {
            Width = Math.Max(240, _historyContainer.ClientSize.Width - 40)
        };
        _historyRows.Insert(0, row);
        _historyContainer.Controls.Add(row);
        RelayoutHistory();
    }

    private void RelayoutHistory()
    {
        _historyEmpty.Visible = _historyRows.Count == 0;
        int y = 24;
        foreach (var row in _historyRows)
        {
            row.Width = Math.Max(240, _historyContainer.ClientSize.Width - 40);
            row.Location = new Point(20, y);
            y += row.Height + 8;
        }
    }

    private Panel BuildThemesPanel()
    {
        var panel = new BufferedPanel { Dock = DockStyle.Fill, BackColor = Theme.Background };

        _themesHeading = new Label
        {
            Text = I18n.T("themes.heading"),
            Location = new Point(24, 24),
            AutoSize = true,
            ForeColor = Theme.TextPrimary,
            Font = Theme.FontMonoBold
        };
        panel.Controls.Add(_themesHeading);

        int y = 60;
        foreach (var palette in Palettes.All)
        {
            var card = new ThemeCard(palette) { Location = new Point(24, y) };
            card.Picked += (_, _) =>
            {
                if (ReferenceEquals(ThemeManager.Target, palette)) return;
                ThemeManager.SwitchTo(palette, this, _encryptPanel, _historyPanel, _themesPanel, _settingsPanel);
                SoundFx.Play(Sound.Theme);
            };
            _themeCards.Add(card);
            panel.Controls.Add(card);
            y += card.Height + 12;
        }

        return panel;
    }

    private Panel BuildSettingsPanel()
    {
        var panel = new BufferedPanel { Dock = DockStyle.Fill, BackColor = Theme.Background };

        _langSettingLabel = new Label
        {
            Text = I18n.T("settings.language"),
            Location = new Point(24, 24),
            AutoSize = true,
            ForeColor = Theme.TextPrimary,
            Font = Theme.FontMonoBold
        };

        var langDropdown = new LanguageDropdown { Location = new Point(24, 54) };
        langDropdown.SelectionChanged += (_, _) =>
        {
            var lang = langDropdown.SelectedLanguage;
            if (lang == I18n.Current) return;
            I18n.SetLanguage(lang);
            SoundFx.Play();
        };

        _snowSettingLabel = new Label
        {
            Text = I18n.T("settings.snow"),
            Location = new Point(24, 116),
            AutoSize = true,
            ForeColor = Theme.TextPrimary,
            Font = Theme.FontMonoBold
        };

        _snowToggle = new ToggleSwitch { Location = new Point(24, 148), CooldownMs = 3000 };
        _snowToggle.CheckedChanged += (_, _) =>
        {
            _snow.SetActive(_snowToggle.Checked);
            if (_snowToggle.Checked) SoundFx.Play();
        };

        panel.Controls.Add(_langSettingLabel);
        panel.Controls.Add(langDropdown);
        panel.Controls.Add(_snowSettingLabel);
        panel.Controls.Add(_snowToggle);
        return panel;
    }

    private void RefreshLocalizedText()
    {
        _nav.SetLabel(0, I18n.T("tab.encrypt"));
        _nav.SetLabel(1, I18n.T("tab.history"));
        _nav.SetLabel(2, I18n.T("tab.themes"));
        _nav.SetLabel(3, I18n.T("tab.settings"));

        _disclaimerLabel.Text = I18n.T("disclaimer");
        _historyEmpty.Text = I18n.T("history.empty");
        _themesHeading.Text = I18n.T("themes.heading");
        _langSettingLabel.Text = I18n.T("settings.language");
        _snowSettingLabel.Text = I18n.T("settings.snow");

        _dropZone.Invalidate();
        foreach (var card in _themeCards) card.Invalidate();
    }

    private void ShowReadmeCard()
    {
        const int margin = 36;
        var card = new ReadmeCard
        {
            Location = new Point(margin, margin),
            Size = new Size(ClientSize.Width - margin * 2, ClientSize.Height - margin * 2),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        card.CloseRequested += (_, _) =>
        {
            Controls.Remove(card);
            card.Dispose();
        };
        Controls.Add(card);
        card.BringToFront();
    }

    private void HandleDroppedFile(string path)
    {
        _fileLabel.Text = $"> {Path.GetFileName(path)}";

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".sny" or ".snow") HandleDecrypt(path);
        else HandleEncrypt(path);
    }

    private void HandleEncrypt(string path)
    {
        using var modeDlg = new ModeSelectDialog(Path.GetFileName(path));
        if (modeDlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            string outPath;
            switch (modeDlg.SelectedMode)
            {
                case VaultMode.Simple:
                    outPath = VaultEngine.EncryptSimple(path);
                    AddHistoryEntry(I18n.T("log.encrypted_simple", Path.GetFileName(outPath)), Theme.Success, outPath);
                    break;

                case VaultMode.Moderate:
                    using (var pwdDlg = new PasswordSetupDialog())
                    {
                        if (pwdDlg.ShowDialog(this) != DialogResult.OK) return;
                        outPath = VaultEngine.EncryptModerate(path, pwdDlg.Password);
                        AddHistoryEntry(I18n.T("log.encrypted_moderate", Path.GetFileName(outPath)), Theme.Success, outPath);
                        AddHistoryEntry(I18n.T("log.password_note"), Theme.TextDim);
                    }
                    break;

                case VaultMode.Protected:
                    using (var puzzleDlg = new PuzzleSetupDialog())
                    {
                        if (puzzleDlg.ShowDialog(this) != DialogResult.OK) return;
                        outPath = VaultEngine.EncryptProtected(path, puzzleDlg.Kind, puzzleDlg.Hint, puzzleDlg.Answer);
                        AddHistoryEntry(I18n.T("log.encrypted_protected", Path.GetFileName(outPath)), Theme.Success, outPath);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            AddHistoryEntry(I18n.T("log.encrypt_error", ex.Message), Theme.Danger);
        }
    }

    private void HandleDecrypt(string path)
    {
        VaultHeader header;
        try
        {
            header = VaultEngine.ReadHeader(path);
        }
        catch (Exception ex)
        {
            AddHistoryEntry(I18n.T("log.not_snowy_file", ex.Message), Theme.Danger);
            return;
        }

        var outDir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".", "Decrypted");

        switch (header.Mode)
        {
            case VaultMode.Simple: DecryptSimpleFlow(path, outDir); break;
            case VaultMode.Moderate: DecryptModerateFlow(path, outDir); break;
            case VaultMode.Protected: DecryptProtectedFlow(path, header, outDir); break;
        }
    }

    private void DecryptSimpleFlow(string path, string outDir)
    {
        using var progress = new ExtractingProgressForm(Path.GetFileName(path));
        progress.Show(this);
        progress.Refresh();
        Application.DoEvents();

        try
        {
            var outPath = VaultEngine.DecryptSimple(path, outDir);
            AddHistoryEntry(I18n.T("log.decrypted", outPath), Theme.Success, outPath);
        }
        catch (Exception ex)
        {
            AddHistoryEntry(I18n.T("log.decrypt_error", ex.Message), Theme.Danger);
        }
        finally
        {
            progress.Close();
        }
    }

    private void DecryptModerateFlow(string path, string outDir)
    {
        const int maxAttempts = 3;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var dlg = new PasswordPromptDialog(maxAttempts - attempt + 1);
            if (dlg.ShowDialog(this) != DialogResult.OK) { AddHistoryEntry(I18n.T("log.cancelled"), Theme.TextDim); return; }

            if (VaultEngine.TryDecryptModerate(path, dlg.Password, outDir, out var outPath))
            {
                AddHistoryEntry(I18n.T("log.decrypted", outPath), Theme.Success, outPath);
                return;
            }
            AddHistoryEntry(I18n.T("log.wrong_password", attempt, maxAttempts), Theme.Danger);
        }
        AddHistoryEntry(I18n.T("log.locked_out"), Theme.Danger);
    }

    private void DecryptProtectedFlow(string path, VaultHeader header, string outDir)
    {
        bool wrongLast = false;
        while (true)
        {
            using var dlg = new PuzzleSolveDialog(header, wrongLast);
            if (dlg.ShowDialog(this) != DialogResult.OK) { AddHistoryEntry(I18n.T("log.cancelled"), Theme.TextDim); return; }

            var secrets = new ProtectedSecrets();
            if (header.FactorsMask.HasFlag(Factors.Pattern))  secrets.Pattern = dlg.Answer;
            if (header.FactorsMask.HasFlag(Factors.Question)) secrets.Answer  = dlg.Answer;

            DecryptResult result;
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                result = VaultEngine.DecryptProtected(path, secrets, outDir);
            }
            catch (Exception ex)
            {
                AddHistoryEntry(I18n.T("log.decrypt_error", ex.Message), Theme.Danger);
                return;
            }
            finally { Cursor.Current = Cursors.Default; }

            switch (result.Status)
            {
                case DecryptStatus.Success:
                    AddHistoryEntry(I18n.T("log.decrypted", result.OutputPath!), Theme.Success, result.OutputPath);
                    return;

                case DecryptStatus.Locked:
                    AddHistoryEntry(I18n.T("log.locked_wait", (int)Math.Ceiling(result.RetryAfter.TotalSeconds)), Theme.Danger);
                    return;

                case DecryptStatus.WrongSecret:
                    AddHistoryEntry(I18n.T("log.wrong_puzzle"), Theme.Danger);
                    if (result.RetryAfter > TimeSpan.Zero)
                    {
                        AddHistoryEntry(I18n.T("log.locked_wait", (int)Math.Ceiling(result.RetryAfter.TotalSeconds)), Theme.Danger);
                        return;
                    }
                    wrongLast = true;
                    continue;

                default:
                    AddHistoryEntry(I18n.T("log.corrupted"), Theme.Danger);
                    return;
            }
        }
    }
}