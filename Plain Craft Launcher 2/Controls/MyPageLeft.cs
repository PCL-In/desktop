using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Core.App;

namespace PCL;

public class MyPageLeft : Grid
{
    public static DependencyProperty AnimatedControlProperty =
        DependencyProperty.Register("AnimatedControl", typeof(FrameworkElement), typeof(MyPageLeft));

    private readonly int uuid = ModBase.GetUuid();

    private bool _animatedControlNullWarned;

    // 执行逐个进入动画的控件
    public FrameworkElement AnimatedControl
    {
        get
        {
            var res = GetValue(AnimatedControlProperty);
            if (res is null && !_animatedControlNullWarned)
            {
                _animatedControlNullWarned = true;
                ModBase.Log($"[MyPageLeft] 获取到 AnimatedControl(来自 {Name}) 的值为 null", ModBase.LogLevel.Debug);
            }

            return (FrameworkElement)res;
        }
        set => SetValue(AnimatedControlProperty, value);
    }

    public void TriggerShowAnimation()
    {
        if (AnimatedControl is null)
        {
            // 缩放动画
            if (RenderTransform is not ScaleTransform)
            {
                RenderTransform = new ScaleTransform(0.96d, 0.96d);
                RenderTransformOrigin = new Point(0.5d, 0.5d);
            }

            Opacity = 0d;
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaScaleTransform(this, 1d - ((ScaleTransform)RenderTransform).ScaleX,
                        ease: new ModAnimation.AniEaseOutBack((ModAnimation.AniEasePower)2)),
                    ModAnimation.AaOpacity(this, 1d, 100)
                }, "PageLeft PageChange " + uuid);
        }
        else
        {
            // 逐个进入动画
            var aniList = new List<ModAnimation.AniData>();
            var id = 0;
            var delay = 0;
            foreach (var ElementRaw in GetAllAnimControls(true))
            {
                var element = MyVirtualizingElement.TryInit(ElementRaw);
                if (element.Visibility == Visibility.Collapsed)
                {
                    // 还原之前的隐藏动画可能导致的改变（#2436）
                    element.Opacity = 1d;
                    element.RenderTransform = new TranslateTransform(0d, 0d);
                    if (element is MyListItem)
                        ((MyListItem)element).isMouseOverAnimationEnabled = true;
                }
                else
                {
                    element.Opacity = 0d;
                    element.RenderTransform = new TranslateTransform(-25, 0d);
                    if (element is MyListItem)
                        ((MyListItem)element).isMouseOverAnimationEnabled = false;
                    aniList.Add(ModAnimation.AaOpacity(element, element is TextBlock ? 0.6d : 1d, 100, delay,
                        new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)));
                    aniList.Add(ModAnimation.AaTranslateX(element, 5d, 200, delay,
                        new ModAnimation.AniEaseOutFluent()));
                    aniList.Add(ModAnimation.AaTranslateX(element, 20d, 300, delay,
                        new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)));
                    if (element is MyListItem)
                        aniList.Add(ModAnimation.AaCode(() =>
                        {
                            ((MyListItem)element).isMouseOverAnimationEnabled = true;
                            ((MyListItem)element).RefreshColor(this, new EventArgs());
                        }, delay + 280));
                    delay += Math.Max(15 - id, 7) * 2;
                    id += 1;
                }
            }

            ModAnimation.AniStart(aniList, "PageLeft PageChange " + uuid);
        }
    }

    public void TriggerHideAnimation()
    {
        if (AnimatedControl is null)
        {
            // 缩放动画
            if (RenderTransform is not ScaleTransform)
            {
                RenderTransform = new ScaleTransform(1d, 1d);
                RenderTransformOrigin = new Point(0.5d, 0.5d);
            }

            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaScaleTransform(this, 0.95d - ((ScaleTransform)RenderTransform).ScaleX, 110,
                        ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaOpacity(this, -Opacity, 80, 30)
                }, "PageLeft PageChange " + uuid);
        }
        else
        {
            // 逐个退出动画
            var aniList = new List<ModAnimation.AniData>();
            var id = 0;
            var controls = GetAllAnimControls();
            foreach (var Element in controls)
            {
                aniList.Add(ModAnimation.AaOpacity(Element, -Element.Opacity, 50,
                    (int)Math.Round(70d / controls.Count * id)));
                aniList.Add(ModAnimation.AaTranslateX(Element, -6, 50, (int)Math.Round(70d / controls.Count * id)));
                id += 1;
            }

            ModAnimation.AniStart(aniList, "PageLeft PageChange " + uuid);
        }
    }

    // 遍历获取所有需要生成动画的控件
    private List<FrameworkElement> GetAllAnimControls(bool ignoreInvisibility = false)
    {
        var allControls = new List<FrameworkElement>();
        GetAllAnimControls(AnimatedControl, ref allControls, ignoreInvisibility);
        return allControls;
    }

    private void GetAllAnimControls(FrameworkElement element, ref List<FrameworkElement> allControls,
        bool ignoreInvisibility)
    {
        if (!ignoreInvisibility && element.Visibility == Visibility.Collapsed)
            return;
        if (element is MyTextButton)
            allControls.Add(element);
        else if (element is MyListItem)
            allControls.Add(element);
        else if (element is ContentControl)
            GetAllAnimControls((FrameworkElement)((ContentControl)element).Content, ref allControls,
                ignoreInvisibility);
        else if (element is Panel)
            foreach (FrameworkElement Element2 in ((Panel)element).Children)
                GetAllAnimControls(Element2, ref allControls, ignoreInvisibility);
        else
            allControls.Add(element);
    }

    // PCL-In：左侧栏仅显示图标

    private static readonly List<MyPageLeft> IconOnlyPages = new();

    public MyPageLeft()
    {
        Loaded += (_, _) =>
        {
            if (!IconOnlyPages.Contains(this))
                IconOnlyPages.Add(this);
            RefreshIconOnly();
        };
        Unloaded += (_, _) => IconOnlyPages.Remove(this);
    }

    /// <summary>
    ///     按当前设置刷新所有已加载的左侧栏。
    /// </summary>
    public static void RefreshIconOnlyAll()
    {
        foreach (var page in IconOnlyPages.ToArray())
            page.RefreshIconOnly();
    }

    /// <summary>
    ///     按当前设置刷新左侧栏：开启“仅显示图标”时隐藏列表项标题与分组文字。
    /// </summary>
    public void RefreshIconOnly()
    {
        try
        {
            var iconOnly = Config.Preference.Hide.PageLeftIconOnly;
            var items = new List<MyListItem>();
            CollectListItems(this, items);
            foreach (var item in items)
                item.ApplyLeftIconOnly(iconOnly);
            RefreshGroupTextVisibility();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新左侧栏仅显示图标状态出错");
        }
    }

    private bool _groupTextRefreshQueued;

    /// <summary>
    ///     排队刷新分组标题（PCL-In）。
    ///     列表项加载完成时会逐项调用这里，所以必须合并成一次：否则 n 个列表项各自遍历一遍
    ///     整棵视觉树去找列表项，几百项的版本列表就是 O(n²)。
    /// </summary>
    public void QueueGroupTextRefresh()
    {
        if (_groupTextRefreshQueued)
            return;
        _groupTextRefreshQueued = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _groupTextRefreshQueued = false;
            RefreshGroupTextVisibility();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    ///     刷新分组标题（如“添加或导入”“实用工具”）的可见性。
    ///     只有确实带了图标的列表才隐藏分组标题，否则（文件夹列表、日志列表等）保持原样（PCL-In）。
    /// </summary>
    public void RefreshGroupTextVisibility()
    {
        try
        {
            var panel = FindItemPanel(this);
            if (panel is null)
                return;
            var items = new List<MyListItem>();
            CollectListItems(this, items);
            var hideGroupText = Config.Preference.Hide.PageLeftIconOnly && items.Any(item => item.HasListLogo);
            foreach (var child in panel.Children)
                if (child is TextBlock text)
                    text.Visibility = hideGroupText ? Visibility.Collapsed : Visibility.Visible;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新左侧栏分组标题出错", ModBase.LogLevel.Debug);
        }
    }

    // 递归收集左侧栏内的所有列表项
    private static void CollectListItems(DependencyObject? element, List<MyListItem> result)
    {
        switch (element)
        {
            case MyListItem item: // MyListItem 本身就是 Panel，这一条必须放在前面
                result.Add(item);
                break;
            case Panel panel:
                foreach (UIElement child in panel.Children)
                    CollectListItems(child, result);
                break;
            case ContentControl content when content.Content is DependencyObject inner:
                CollectListItems(inner, result);
                break;
        }
    }

    // 找到直接容纳列表项的那个面板，好一并处理与列表项同级的分类标题
    private static Panel? FindItemPanel(DependencyObject? element)
    {
        switch (element)
        {
            case Panel panel:
                foreach (UIElement child in panel.Children)
                    if (child is MyListItem)
                        return panel;
                foreach (UIElement child in panel.Children)
                {
                    var found = FindItemPanel(child);
                    if (found is not null)
                        return found;
                }
                return null;
            case ContentControl content when content.Content is DependencyObject inner:
                return FindItemPanel(inner);
            default:
                return null;
        }
    }
}

public interface IRefreshable
{
    void Refresh();
}