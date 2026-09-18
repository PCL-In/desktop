using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using PCL.Core.App;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageChatRight
{
    /// <summary>
    ///     聊天室地址。
    /// </summary>
    private const string ChatUrl = "https://minichat.astras.cc";

    /// <summary>
    ///     网页的显示比例。
    ///     启动器窗口默认只有 810x470，WebView2 的可用高度约 400px，
    ///     按 100% 渲染时聊天室的登录卡片会被裁掉一截，所以默认缩小到 80%。
    ///     想让网页更大/更小，改这一个数就行。
    /// </summary>
    private const double ChatZoomFactor = 0.8;

    /// <summary>
    ///     网页当前的显示状态。
    /// </summary>
    private enum ChatState
    {
        Loading,
        Loaded,
        Failed
    }

    private readonly WebView2 _view;
    private readonly TextBlock _labStatus;
    private readonly StackPanel _panButtons;

    private ChatState _state = ChatState.Loading;
    private bool _isInitializing;
    private bool _isCoreReady;
    private bool _isDialogHooked;

    /// <summary>
    ///     是否有模态弹窗正在显示。WebView2 是独立的子窗口，会盖住所有 WPF 控件，
    ///     因此弹窗出现时必须把网页先藏起来，否则弹窗会被网页挡住，看起来就像启动器卡死了。
    /// </summary>
    private bool _isDialogOpen;

    /// <summary>
    ///     离开聊天页多久之后释放内嵌浏览器（PCL-In）。留在页面上时保持热启动，不释放。
    /// </summary>
    private static readonly TimeSpan ReleaseDelay = TimeSpan.FromSeconds(90);

    private System.Windows.Threading.DispatcherTimer? _releaseTimer;
    private bool _isReleased;

    public PageChatRight()
    {
        InitializeComponent();

        // 内置浏览器同样在代码里创建：本页 XAML 中放 WebView2 也会卡住加载
        _view = new WebView2 { Visibility = Visibility.Collapsed };
        ViewHost.Children.Add(_view);

        var btnRetry = new MyButton
        {
            Text = Lang.Text("Chat.Action.Retry"),
            MinWidth = 110,
            Margin = new Thickness(0, 0, 12, 0)
        };
        btnRetry.Click += (_, _) => Refresh();
        var btnOpenBrowser = new MyButton
        {
            Text = Lang.Text("Chat.Action.OpenBrowser"),
            MinWidth = 140
        };
        btnOpenBrowser.Click += (_, _) => ModBase.OpenWebsite(ChatUrl);

        _panButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 18, 0, 0),
            Visibility = Visibility.Collapsed
        };
        _panButtons.Children.Add(btnRetry);
        _panButtons.Children.Add(btnOpenBrowser);

        _labStatus = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            FontSize = 13.5,
            Text = Lang.Text("Chat.Status.Loading")
        };
        _labStatus.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1");

        var panStatus = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 440,
            Margin = new Thickness(30)
        };
        panStatus.Children.Add(_labStatus);
        panStatus.Children.Add(_panButtons);
        ((Grid)Child).Children.Add(panStatus);

        Loaded += (_, _) => Refresh();
    }

    /// <summary>
    ///     初始化 WebView2 并打开聊天室。重复调用不会重新加载网页。
    /// </summary>
    public async void Refresh()
    {
        if (_isCoreReady)
        {
            _RefreshVisibility();
            return;
        }

        if (_isInitializing)
            return;
        _isInitializing = true;
        try
        {
            string version;
            try
            {
                version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "未检测到可用的 WebView2 运行时，无法在内嵌窗口中打开聊天室");
                _Fail("Chat.Status.WebViewMissing");
                return;
            }

            ModBase.Log("[Chat] 正在初始化 WebView2 内核，运行时版本：" + version);
            // 用户数据目录放在 LocalAppData：它会被 WebView2 写入大量缓存，不适合塞进启动器目录或漫游配置
            var userDataFolder = Path.Combine(Paths.SharedLocalData, "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await _view.EnsureCoreWebView2Async(environment);
            _view.ZoomFactor = ChatZoomFactor;

            var core = _view.CoreWebView2;
            // 这里只是一个聊天室窗口，不需要开发者工具、右键菜单与密码保存
            var settings = core.Settings;
            settings.AreDevToolsEnabled = false;
            settings.AreDefaultContextMenusEnabled = false;
            settings.IsPasswordAutosaveEnabled = false;
            settings.IsGeneralAutofillEnabled = false;
            settings.AreHostObjectsAllowed = false;
            settings.IsZoomControlEnabled = false;
            settings.IsSwipeNavigationEnabled = false;
            settings.AreBrowserAcceleratorKeysEnabled = false;

            core.NewWindowRequested += (_, e) =>
            {
                // 聊天室里的外链一律交给系统浏览器，不在启动器内另开窗口
                e.Handled = true;
                ModBase.OpenWebsite(e.Uri);
            };
            core.ProcessFailed += (_, e) =>
                ModBase.Log("[Chat] WebView2 进程异常：" + e.ProcessFailedKind, ModBase.LogLevel.Hint);
            core.NavigationCompleted += (_, e) =>
            {
                if (e.IsSuccess)
                {
                    ModBase.Log("[Chat] 聊天室加载完成，状态码 " + e.HttpStatusCode);
                    _state = ChatState.Loaded;
                    _RefreshVisibility();
                }
                else
                {
                    ModBase.Log("[Chat] 聊天室加载失败：" + e.WebErrorStatus, ModBase.LogLevel.Hint);
                    _Fail("Chat.Status.LoadFailed");
                }
            };

            _isCoreReady = true;
            _HookDialogOverlay();
            core.Navigate(ChatUrl);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "聊天室初始化失败");
            _Fail("Chat.Status.InitFailed");
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private void _HookDialogOverlay()
    {
        if (_isDialogHooked || ModMain.frmMain is null)
            return;
        _isDialogHooked = true;
        ModMain.frmMain.PanMsgBackground.IsVisibleChanged += (_, _) =>
        {
            _isDialogOpen = ModMain.frmMain.PanMsgBackground.Visibility == Visibility.Visible;
            _RefreshVisibility();
        };
    }

    private void _RefreshVisibility()
    {
        // PCL-In：第一次刷新时挂上「离开聊天页一段时间后释放内嵌浏览器」的计时器
        if (_releaseTimer is null)
        {
            _releaseTimer = new System.Windows.Threading.DispatcherTimer { Interval = ReleaseDelay };
            _releaseTimer.Tick += (_, _) =>
            {
                _releaseTimer?.Stop();
                ReleaseChatWebView();
            };
            IsVisibleChanged += (_, _) =>
            {
                if (IsVisible)
                    _releaseTimer?.Stop(); // 回到聊天页，取消待释放
                else if (_state == ChatState.Loaded)
                    _releaseTimer?.Start(); // 离开聊天页，开始倒计时
            };
        }

        _view.Visibility = _state == ChatState.Loaded && !_isDialogOpen
            ? Visibility.Visible
            : Visibility.Collapsed;
        _labStatus.Visibility = _state == ChatState.Loaded ? Visibility.Collapsed : Visibility.Visible;
        _panButtons.Visibility = _state == ChatState.Failed ? Visibility.Visible : Visibility.Collapsed;
        if (_state == ChatState.Loading)
            _labStatus.Text = Lang.Text("Chat.Status.Loading");
    }

    /// <summary>
    ///     释放内嵌浏览器，把内存与后台进程还给系统（PCL-In）。
    ///     调用后本页面实例作废：ReleaseChatWebView() 会把 ModMain.frmChatRight 置空，
    ///     下次进入聊天页会重新创建一个（代价是 WebView2 冷启动）。
    /// </summary>
    public void Release()
    {
        if (_isReleased)
            return;
        _isReleased = true;
        _releaseTimer?.Stop();
        try
        {
            if (ViewHost.Children.Contains(_view))
                ViewHost.Children.Remove(_view);
            _view.Dispose();
            ModBase.Log("[Chat] 已释放内嵌浏览器");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Chat] 释放内嵌浏览器失败", ModBase.LogLevel.Debug);
        }
    }

    /// <summary>
    ///     释放聊天页占用的内存（PCL-In）：离开聊天页 90 秒后、在设置里隐藏聊天页时、
    ///     以及开始启动游戏前调用。
    /// </summary>
    public static void ReleaseChatWebView()
    {
        try
        {
            var page = ModMain.frmChatRight;
            if (page is null || page._isReleased)
                return;
            page.Release();
            ModMain.frmChatRight = null; // 下次进聊天页重建
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Chat] 释放聊天页失败", ModBase.LogLevel.Debug);
        }
    }

    private void _Fail(string key)
    {
        _state = ChatState.Failed;
        _labStatus.Text = Lang.Text(key);
        _RefreshVisibility();
    }
}