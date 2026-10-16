using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using BatoceraMovieScraper;
using BatoceraMovieScraper.Render;
using BatoceraMovieScraper.Scrape;

namespace BatoceraMovieScraper.Win;

internal sealed class MainForm : Form
{
    static readonly string ConfigPath =
        Path.Combine(AppContext.BaseDirectory, "config.json");

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    readonly TextBox _key = new();
    readonly TextBox _tok = new();
    readonly TextBox _proxy = new();
    readonly TextBox _dir = new();
    readonly TextBox _lang = new();
    readonly ComboBox _core = new();
    readonly CheckBox _subOn = new() { Text = "启用字幕下载", AutoSize = true };
    readonly ComboBox _subSrc = new();
    readonly TextBox _subLang = new();
    readonly TextBox _osToken = new();
    readonly TextBox _osKey = new();
    readonly TextBox _osUser = new();
    readonly TextBox _osPass = new() { UseSystemPasswordChar = true };
    readonly TextBox _log = new();
    readonly Label _status = new();
    readonly Button _browse = new() { Text = "选择文件夹…", AutoSize = true };
    readonly Button _save = new() { Text = "保存设置", AutoSize = true };
    readonly Button _run = new() { Text = "开始刮削", AutoSize = true };
    readonly Button _open = new() { Text = "打开片库目录", AutoSize = true };

    CancellationTokenSource? _cts;
    TableLayoutPanel? _root;

    public MainForm()
    {
        Text = "电影刮削器";
        Width = 880;
        Height = 820;
        MinimumSize = new Size(780, 700);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9F);

        foreach (var c in CoreRegistry.All)
            _core.Items.Add(new CoreItem(c.Id, c.Display));
        _core.DropDownStyle = ComboBoxStyle.DropDownList;

        _subSrc.Items.Add(new CoreItem(SubtitleSourceFactory.Assrt, "射手网(伪)·国内"));
        _subSrc.Items.Add(new CoreItem(SubtitleSourceFactory.OpenSubtitles, "OpenSubtitles"));
        _subSrc.DropDownStyle = ComboBoxStyle.DropDownList;
        _subSrc.SelectedIndexChanged += (_, _) => UpdateSubSourceFields();
        _subSrc.SelectedIndex = 0;

        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.WordWrap = true;
        _log.BackColor = Color.FromArgb(28, 28, 30);
        _log.ForeColor = Color.Gainsboro;
        _log.Font = new Font("Consolas", 9.5F);

        _browse.Click += (_, _) => BrowseDir();
        _save.Click += (_, _) => SaveConfig(ReadForm());
        _run.Click += async (_, _) => await RunAsync();
        _open.Click += (_, _) => OpenDir();

        BuildLayout();
        Load += (_, _) => LoadConfigToForm();
    }

    void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 16,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 13; i++) root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 字段行
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // 按钮行
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // 日志
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));    // 状态

        AddField(root, 0, "TMDB API Key (v3):", _key);
        AddField(root, 1, "TMDB Read Token (v4):", _tok);
        AddField(root, 2, "代理（留空=直连）:", _proxy);
        _proxy.PlaceholderText = "socks5h://127.0.0.1:10808";

        // 片库目录：文本框 + 选择文件夹按钮
        var dirBox = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            Margin = new Padding(0, 3, 0, 3),
        };
        dirBox.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        dirBox.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _dir.Dock = DockStyle.Fill;
        _browse.Anchor = AnchorStyles.Right;
        dirBox.Controls.Add(_dir, 0, 0);
        dirBox.Controls.Add(_browse, 1, 0);
        root.Controls.Add(new Label { Text = "片库目录:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 6, 0) }, 0, 3);
        root.Controls.Add(dirBox, 1, 3);

        AddField(root, 4, "元数据语言:", _lang);
        _lang.PlaceholderText = "zh-CN";

        root.Controls.Add(new Label { Text = "刮削核心:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 6, 0) }, 0, 5);
        _core.Dock = DockStyle.Fill;
        _core.Margin = new Padding(0, 3, 0, 3);
        root.Controls.Add(_core, 1, 5);

        root.Controls.Add(new Label { Text = "字幕下载:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 6, 0) }, 0, 6);
        _subOn.Anchor = AnchorStyles.Left;
        _subOn.Margin = new Padding(0, 6, 0, 0);
        root.Controls.Add(_subOn, 1, 6);

        root.Controls.Add(new Label { Text = "字幕源:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 6, 0) }, 0, 7);
        _subSrc.Dock = DockStyle.Fill;
        _subSrc.Margin = new Padding(0, 3, 0, 3);
        root.Controls.Add(_subSrc, 1, 7);

        AddField(root, 8, "字幕语言:", _subLang);
        _subLang.PlaceholderText = "zh-cn,en";
        AddField(root, 9, "射手网 Token（国内源）:", _osToken);
        AddField(root, 10, "OpenSubtitles API Key:", _osKey);
        AddField(root, 11, "OpenSubtitles 账号:", _osUser);
        AddField(root, 12, "OpenSubtitles 密码:", _osPass);

        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 8, 0, 8) };
        bar.Controls.Add(_run);
        bar.Controls.Add(_save);
        bar.Controls.Add(_open);
        root.Controls.Add(bar, 0, 13);
        root.SetColumnSpan(bar, 2);

        _log.Dock = DockStyle.Fill;
        root.Controls.Add(_log, 0, 14);
        root.SetColumnSpan(_log, 2);

        _status.Text = "就绪。";
        _status.Dock = DockStyle.Fill;
        _status.AutoSize = true;
        root.Controls.Add(_status, 0, 15);
        root.SetColumnSpan(_status, 2);

        Controls.Add(root);
        _root = root;
        UpdateSubSourceFields();
    }

    // 字幕源与下方的凭据字段联动：选射手网只留它的 Token，选 OpenSubtitles 只留它的三件套。
    void UpdateSubSourceFields()
    {
        if (_root is null) return;
        var assrt = (_subSrc.SelectedItem as CoreItem)?.Id == SubtitleSourceFactory.Assrt;
        SetRowVisible(9, assrt);     // 射手网 Token（国内源）
        SetRowVisible(10, !assrt);   // OpenSubtitles API Key
        SetRowVisible(11, !assrt);   // OpenSubtitles 账号
        SetRowVisible(12, !assrt);   // OpenSubtitles 密码
    }

    void SetRowVisible(int row, bool visible)
    {
        for (var c = 0; c < _root!.ColumnCount; c++)
        {
            var ctl = _root.GetControlFromPosition(c, row);
            if (ctl is not null) ctl.Visible = visible;
        }
        _root.PerformLayout();
    }

    static void AddField(TableLayoutPanel root, int row, string label, TextBox box)
    {
        root.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 6, 0) }, 0, row);
        box.Dock = DockStyle.Fill;
        box.Margin = new Padding(0, 3, 0, 3);
        root.Controls.Add(box, 1, row);
    }

    // ---- 配置读写（与 Batocera 端 config.json 同 schema）----

    void LoadConfigToForm()
    {
        if (File.Exists(ConfigPath))
        {
            try
            {
                var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath), JsonOpts);
                if (cfg is not null) Apply(cfg);
                _status.Text = "已载入 " + ConfigPath;
                return;
            }
            catch (Exception ex)
            {
                _status.Text = "配置读取失败，用默认值：" + ex.Message;
            }
        }
        _lang.Text = "zh-CN";
        if (_core.Items.Count > 0) _core.SelectedIndex = 0;
    }

    void Apply(AppConfig cfg)
    {
        _key.Text = cfg.Tmdb.ApiKey ?? "";
        _tok.Text = cfg.Tmdb.ReadAccessToken ?? "";
        _proxy.Text = cfg.Proxy.Url ?? "";
        _dir.Text = cfg.Library.SourceDir ?? "";
        _lang.Text = string.IsNullOrWhiteSpace(cfg.Tmdb.Language) ? "zh-CN" : cfg.Tmdb.Language;
        var idx = _core.Items.OfType<CoreItem>().ToList().FindIndex(c => c.Id == cfg.Scraper.Core);
        _core.SelectedIndex = idx >= 0 ? idx : 0;

        _subOn.Checked = cfg.Subtitles.Enabled;
        var sidx = _subSrc.Items.OfType<CoreItem>().ToList().FindIndex(c => c.Id == SubtitleSourceFactory.Id(cfg));
        _subSrc.SelectedIndex = sidx >= 0 ? sidx : 0;
        _subLang.Text = string.IsNullOrWhiteSpace(cfg.Subtitles.Languages) ? "zh-cn,en" : cfg.Subtitles.Languages;
        _osToken.Text = cfg.Subtitles.AssrtToken ?? "";
        _osKey.Text = cfg.Subtitles.ApiKey ?? "";
        _osUser.Text = cfg.Subtitles.Username ?? "";
        _osPass.Text = cfg.Subtitles.Password ?? "";
        UpdateSubSourceFields();
    }

    AppConfig ReadForm()
    {
        var cfg = new AppConfig
        {
            Scraper = new ScraperConfig { Core = (_core.SelectedItem as CoreItem)?.Id ?? "douban" },
            Tmdb = new TmdbConfig
            {
                ApiKey = _key.Text.Trim(),
                ReadAccessToken = _tok.Text.Trim(),
                Language = string.IsNullOrWhiteSpace(_lang.Text) ? "zh-CN" : _lang.Text.Trim(),
            },
            Proxy = new ProxyConfig
            {
                Enabled = !string.IsNullOrWhiteSpace(_proxy.Text),
                Url = _proxy.Text.Trim(),
            },
            Library = new LibraryConfig { SourceDir = _dir.Text.Trim() },
            Subtitles = new SubtitleConfig
            {
                Enabled = _subOn.Checked,
                Source = (_subSrc.SelectedItem as CoreItem)?.Id ?? SubtitleSourceFactory.Assrt,
                Languages = string.IsNullOrWhiteSpace(_subLang.Text) ? "zh-cn,en" : _subLang.Text.Trim(),
                AssrtToken = _osToken.Text.Trim(),
                ApiKey = _osKey.Text.Trim(),
                Username = _osUser.Text.Trim(),
                Password = _osPass.Text,
            },
        };
        return cfg;
    }

    void SaveConfig(AppConfig cfg)
    {
        try
        {
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(cfg, JsonOpts));
            _status.Text = "已保存 " + ConfigPath;
        }
        catch (Exception ex)
        {
            _status.Text = "保存失败：" + ex.Message;
        }
    }

    void BrowseDir()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "选择电影文件夹",
            ShowNewFolderButton = true,
        };
        if (Directory.Exists(_dir.Text)) dlg.SelectedPath = _dir.Text;
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _dir.Text = dlg.SelectedPath;
    }

    void OpenDir()
    {
        var d = _dir.Text.Trim();
        if (Directory.Exists(d)) Process.Start(new ProcessStartInfo("explorer.exe", "\"" + d + "\"") { UseShellExecute = true });
        else _status.Text = "目录不存在：" + d;
    }

    // ---- 刮削 ----

    async Task RunAsync()
    {
        if (_cts is not null) return; // 已在跑

        var cfg = ReadForm();
        if (string.IsNullOrWhiteSpace(cfg.Library.SourceDir) || !Directory.Exists(cfg.Library.SourceDir))
        {
            MessageBox.Show(this, "请先选择有效的电影文件夹。", "电影刮削器", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var core = CoreRegistry.Find(cfg.Scraper.Core);
        if (core is null)
        {
            MessageBox.Show(this, "未知刮削核心：" + cfg.Scraper.Core, "电影刮削器", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (core.RequiresKeys && string.IsNullOrWhiteSpace(cfg.Tmdb.ApiKey) && string.IsNullOrWhiteSpace(cfg.Tmdb.ReadAccessToken))
        {
            MessageBox.Show(this, "请先填写 TMDB API Key 或 Read Token。", "电影刮削器", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (cfg.Subtitles.Enabled)
        {
            var missing = SubtitleSourceFactory.Id(cfg) == SubtitleSourceFactory.OpenSubtitles
                ? (string.IsNullOrWhiteSpace(cfg.Subtitles.ApiKey) ? "未填 OpenSubtitles API Key（在 opensubtitles.com 账号里免费生成）。" : null)
                : (string.IsNullOrWhiteSpace(cfg.Subtitles.AssrtToken) ? "未填射手网(伪) Token（登录 assrt.net 后在「用户面板」复制 32 位 Token）。" : null);
            if (missing is not null)
            {
                MessageBox.Show(this, "已启用字幕下载，但" + missing, "电影刮削器", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        SaveConfig(cfg);

        _log.Clear();
        _status.Text = "刮削中…";
        SetRunning(true);
        _cts = new CancellationTokenSource();
        var cts = _cts;
        var outDir = cfg.Library.SourceDir;
        var writer = new TextBoxWriter(AppendLog);
        AppendLog($"刮削核心: {core.Display}{Environment.NewLine}片库: {outDir}{Environment.NewLine}--------------------------------{Environment.NewLine}");

        try
        {
            var manifest = await Task.Run(() => core.ScrapeAsync(cfg, outDir, outDir, writer, cts.Token));
            manifest.Save(Path.Combine(outDir, "manifest.json"));
            GamelistRenderer.Write(manifest, Path.Combine(outDir, "gamelist.xml"));
            AppendLog($"{Environment.NewLine}完成：manifest.json / gamelist.xml / 海报 已生成，共 {manifest.Movies.Count} 部。{Environment.NewLine}");
            _status.Text = $"完成：{manifest.Movies.Count} 部";
        }
        catch (Exception ex)
        {
            AppendLog($"{Environment.NewLine}刮削失败: {ex.Message}{Environment.NewLine}");
            _status.Text = "失败：" + ex.Message;
        }
        finally
        {
            cts.Dispose();
            _cts = null;
            SetRunning(false);
        }
    }

    void SetRunning(bool running)
    {
        _run.Enabled = !running;
        _save.Enabled = !running;
        _browse.Enabled = !running;
        _open.Enabled = !running;
    }

    void AppendLog(string s)
    {
        if (!_log.IsHandleCreated) return;
        _log.BeginInvoke(() =>
        {
            _log.AppendText(s);
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
        });
    }

    sealed record CoreItem(string Id, string Display)
    {
        public override string ToString() => Display;
    }
}
