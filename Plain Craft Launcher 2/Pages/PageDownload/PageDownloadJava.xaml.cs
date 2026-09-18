using System.Threading;
using System.Windows;
using System.Windows.Controls;
using PCL.Core.App.Localization;

namespace PCL;

/// <summary>
///     PCL-In：「下载 → Java」页面。列出 Mojang 官方提供的 Java 运行时（数据来源与自动下载完全一致），
///     让玩家自己挑版本下载，而不是只能等启动时自动下载。
/// </summary>
public partial class PageDownloadJava : MyPageRight
{
    // 版本列表里的一项：Tag 存 Java 大版本号
    private MyComboBox? _comboVersion;
    private MyButton? _btnDownload;
    private TextBlock? _labStatus;
    private bool _isDownloading;

    public PageDownloadJava()
    {
        InitializeComponent();
        BuildUi();
        Loaded += (_, _) => RefreshList();
    }

    /// <summary>
    ///     重新拉取可下载的 Java 版本列表。
    /// </summary>
    public void RefreshList()
    {
        if (_comboVersion is null)
            return;
        SetStatus(Lang.Text("Download.Java.Status.Loading"));
        _comboVersion.Items.Clear();
        ModBase.RunInNewThread(() =>
        {
            try
            {
                var list = ModJava.GetJavaRuntimeList();
                ModBase.RunInUi(() =>
                {
                    if (_comboVersion is null)
                        return;
                    foreach (var (major, version, component) in list)
                        _comboVersion.Items.Add(new MyComboBoxItem
                        {
                            Content = Lang.Text("Download.Java.VersionItem", major, version, component),
                            Tag = major
                        });
                    if (_comboVersion.Items.Count > 0)
                        _comboVersion.SelectedIndex = 0;
                    SetStatus(list.Count == 0 ? Lang.Text("Download.Java.Empty") : "");
                });
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[Java] 获取可下载的 Java 版本列表失败", ModBase.LogLevel.Debug);
                ModBase.RunInUi(() => SetStatus(Lang.Text("Download.Java.Empty")));
            }
        });
    }

    private void BuildUi()
    {
        if (PanMain is null || PanMain.Children.Count > 0)
            return;
        var card = new MyCard
        {
            Margin = new Thickness(20, 20, 20, 0),
            Title = Lang.Text("Download.Java.Intro.Title")
        };
        var content = new StackPanel { Margin = new Thickness(25, 40, 15, 20) };
        content.Children.Add(new TextBlock
        {
            Text = Lang.Text("Download.Java.Intro.Description"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 11)
        });
        content.Children.Add(new TextBlock
        {
            Text = Lang.Text("Download.Java.Version"),
            Margin = new Thickness(0, 0, 0, 5)
        });
        // 宽度跟随卡片（和设置页里的下拉框一样撑满整行），别缩成一小条
        _comboVersion = new MyComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        content.Children.Add(_comboVersion);
        _btnDownload = new MyButton
        {
            Text = Lang.Text("Download.Java.Start"),
            Height = 35,
            MinWidth = 140,
            ColorType = MyButton.ColorState.Highlight,
            Margin = new Thickness(0, 12, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _btnDownload.Click += (_, _) => StartDownload();
        content.Children.Add(_btnDownload);
        _labStatus = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 0),
            Opacity = 0.75
        };
        content.Children.Add(_labStatus);
        card.Children.Add(content);
        PanMain.Children.Add(card);
    }

    private void SetStatus(string text)
    {
        if (_labStatus is not null)
            _labStatus.Text = text;
    }

    private void StartDownload()
    {
        if (_isDownloading || _comboVersion?.SelectedItem is not MyComboBoxItem item || item.Tag is null)
            return;
        var major = (int)ModBase.Val(item.Tag);
        _isDownloading = true;
        if (_btnDownload is not null)
            _btnDownload.IsEnabled = false;
        SetStatus(Lang.Text("Download.Java.Status.Downloading", "Java " + major));
        ModBase.RunInNewThread(() =>
        {
            var success = false;
            var message = "";
            try
            {
                // 与启动时自动下载用的是同一个加载器，只是由玩家自己指定版本号
                var loader = ModJava.GetJavaDownloadLoader();
                loader.Start(major, true);
                while (loader.State == ModBase.LoadState.Loading)
                    Thread.Sleep(50);
                success = loader.State is not (ModBase.LoadState.Failed or ModBase.LoadState.Aborted);
            }
            catch (Exception ex)
            {
                message = ex.Message;
                ModBase.Log(ex, "[Java] 手动下载 Java 失败", ModBase.LogLevel.Debug);
            }

            ModBase.RunInUi(() =>
            {
                _isDownloading = false;
                if (_btnDownload is not null)
                    _btnDownload.IsEnabled = true;
                SetStatus(success
                    ? Lang.Text("Download.Java.Status.Done", "Java " + major)
                    : Lang.Text("Download.Java.Status.Failed", "Java " + major, message));
            });
        });
    }
}
