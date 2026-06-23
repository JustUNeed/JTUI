using JTUI.Theming;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shell;

namespace JTUI.Controls
{
    /// <summary>
    /// JTUI 无边框窗口控件。标题栏提供最小化/最大化(还原)/关闭按钮,
    /// 同时仍支持双击标题栏、拖拽到顶部等系统原生的最大化操作。
    /// </summary>
    [TemplatePart(Name = PartMinButton, Type = typeof(Button))]
    [TemplatePart(Name = PartMaxButton, Type = typeof(Button))]
    [TemplatePart(Name = PartCloseButton, Type = typeof(Button))]
    public class JTWindow : Window
    {
        public const string PartMinButton = "PART_MinButton";
        public const string PartMaxButton = "PART_MaxButton";
        public const string PartCloseButton = "PART_CloseButton";

        // Segoe MDL2 Assets 图标：最大化 / 还原
        private const string GlyphMaximize = "\uE922";
        private const string GlyphRestore = "\uE923";

        private Button? _minButton;
        private Button? _maxButton;
        private Button? _closeButton;

        static JTWindow()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JTWindow),
                new FrameworkPropertyMetadata(typeof(JTWindow)));
        }

        public JTWindow()
        {
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 32,
                ResizeBorderThickness = new Thickness(6),
                CornerRadius = new CornerRadius(0),
                GlassFrameThickness = new Thickness(0),
                UseAeroCaptionButtons = false
            });

            // 双击/拖拽仍会最大化,保留溢出补偿
            StateChanged += OnStateChanged;

            ApplyChromeForMode(TitleBarMode);
        }

        private void OnStateChanged(object? sender, EventArgs e)
        {
            // NoTitleBar 模式不做溢出补偿,保持纯净铺满
            if (TitleBarMode == JTTitleBarMode.NoTitleBar)
                BorderThickness = new Thickness(0);
            else
                BorderThickness = WindowState == WindowState.Maximized
                    ? new Thickness(7)
                    : new Thickness(0);

            UpdateMaxButtonGlyph();
        }


        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            DetachButtonHandlers();

            _minButton = GetTemplateChild(PartMinButton) as Button;
            _maxButton = GetTemplateChild(PartMaxButton) as Button;
            _closeButton = GetTemplateChild(PartCloseButton) as Button;

            if (_minButton is not null)
                _minButton.Click += OnMinButtonClick;
            if (_maxButton is not null)
                _maxButton.Click += OnMaxButtonClick;
            if (_closeButton is not null)
                _closeButton.Click += OnCloseButtonClick;

            UpdateMaxButtonGlyph();
        }

        private void DetachButtonHandlers()
        {
            if (_minButton is not null)
                _minButton.Click -= OnMinButtonClick;
            if (_maxButton is not null)
                _maxButton.Click -= OnMaxButtonClick;
            if (_closeButton is not null)
                _closeButton.Click -= OnCloseButtonClick;
        }

        private void OnMinButtonClick(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void OnMaxButtonClick(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
            => Close();

        /// <summary>根据当前窗口状态切换最大化/还原按钮的图标与提示。</summary>
        private void UpdateMaxButtonGlyph()
        {
            if (_maxButton is null) return;

            bool maximized = WindowState == WindowState.Maximized;
            _maxButton.Content = maximized ? GlyphRestore : GlyphMaximize;
            _maxButton.ToolTip = maximized ? "还原" : "最大化";
        }

        public static readonly DependencyProperty ThemeProperty =
            DependencyProperty.Register(
                nameof(Theme),
                typeof(JTTheme),
                typeof(JTWindow),
                new FrameworkPropertyMetadata(JTTheme.System, OnThemePropertyChanged));



        public static readonly DependencyProperty TitleBarModeProperty =
    DependencyProperty.Register(
        nameof(TitleBarMode),
        typeof(JTTitleBarMode),
        typeof(JTWindow),
        new FrameworkPropertyMetadata(
            JTTitleBarMode.Normal,
            OnTitleBarModeChanged));

        /// <summary>标题栏显示模式:常规 / 无标题栏 / 沉浸。</summary>
        public JTTitleBarMode TitleBarMode
        {
            get => (JTTitleBarMode)GetValue(TitleBarModeProperty);
            set => SetValue(TitleBarModeProperty, value);
        }

        private static void OnTitleBarModeChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is JTWindow w)
                w.ApplyChromeForMode((JTTitleBarMode)e.NewValue);
        }

        /// <summary>
        /// 不同模式下调整 WindowChrome 的标题栏可拖拽高度。
        /// NoTitleBar 模式没有标题栏区域,CaptionHeight 设为 0,避免顶部 32px 误吞鼠标事件;
        /// 其余模式保留 32px 拖拽区。
        /// </summary>
        private void ApplyChromeForMode(JTTitleBarMode mode)
        {
            var chrome = WindowChrome.GetWindowChrome(this);
            if (chrome is null) return;

            if (mode == JTTitleBarMode.NoTitleBar)
            {
                // 纯无边框:不允许边缘拖动改尺寸,也没有标题栏拖拽区
                chrome.CaptionHeight = 0;
                chrome.ResizeBorderThickness = new Thickness(0);
            }
            else
            {
                chrome.CaptionHeight = 32;
                chrome.ResizeBorderThickness = new Thickness(6);
            }
        }







        /// <summary>窗口主题。设置后会切换整个应用的 JTUI 主题。</summary>
        public JTTheme Theme
        {
            get => (JTTheme)GetValue(ThemeProperty);
            set => SetValue(ThemeProperty, value);
        }

        private static void OnThemePropertyChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is JTTheme theme)
                JTThemeManager.Current = theme;
        }
    }
}
