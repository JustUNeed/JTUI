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
        }

        private void OnStateChanged(object? sender, EventArgs e)
        {
            // 最大化时窗口会向外溢出约等于可缩放边框的厚度,
            // 补一圈 BorderThickness 把内容缩回屏幕可视区域。
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
