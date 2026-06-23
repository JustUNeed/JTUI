using JTUI.Theming;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace JTUI.Notifications
{
    /// <summary>
    /// 鼠标位置堆叠式 toast 通知，可在应用任意位置静态调用。
    /// 运行在独立 UI 线程上，主线程卡顿不影响其动画与淡出；配色跟随 JTUI 明暗主题。
    /// </summary>
    public static class JTToast
    {
        private const int Gap = 6;            // 两条之间的间隙
        private const int CursorOffsetY = 24; // 与鼠标的竖直偏移，避免压住光标
        private const int MaxCount = 6;       // 最大同时显示条数

        private static Dispatcher? _dispatcher;
        private static readonly object _initLock = new();

        // 以下字段只在 toast 线程上访问
        private static readonly List<JTToastWindow> _stack = new();
        private static Point _anchor;

        // 当前主题资源（在 toast 线程上构建并缓存，主题变化时重建）
        private static ResourceDictionary? _colors;


        // ---- 对外 API ----

        /// <summary>弹出一条通知。可在任意线程调用。</summary>
        public static void Show(string message, int ms = 2500)
        {
            var dispatcher = EnsureThread();
            var cursor = NativeMethods.GetCursorScreenPoint();

            dispatcher.BeginInvoke(() =>
            {
                _anchor = cursor;

                // 超出上限：先挤掉最旧的一条
                while (_stack.Count >= MaxCount)
                    _stack[0].CloseNow();   // CloseNow 内部会回调移除并触发重排

                var toast = new JTToastWindow(message, ms, EnsureColors(), OnToastClosed);
                _stack.Add(toast);
                toast.Show();
                Relayout(toast);
            });
        }

        // ---- 独立 UI 线程 ----

        private static Dispatcher EnsureThread()
        {
            if (_dispatcher != null) return _dispatcher;
            lock (_initLock)
            {
                if (_dispatcher != null) return _dispatcher;

                var ready = new ManualResetEventSlim(false);
                var thread = new Thread(() =>
                {
                    _dispatcher = Dispatcher.CurrentDispatcher;
                    ready.Set();
                    Dispatcher.Run();
                })
                {
                    IsBackground = true,
                    Name = "JTToastThread"
                };
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                ready.Wait();

                // 主题变化时，跨线程刷新配色并重绘已存在的 toast
                JTThemeManager.ThemeChanged += (_, _) =>
                    _dispatcher!.BeginInvoke(() =>
                    {
                        _colors = null;             // 强制下次重建
                        var c = EnsureColors();
                        foreach (var t in _stack)
                            t.ApplyColors(c);
                    });

                return _dispatcher!;
            }
        }

        // ---- 主题配色（在 toast 线程上加载一份独立字典） ----

        private static ResourceDictionary EnsureColors()
        {
            if (_colors != null) return _colors;

            // JTThemeManager.ActualTheme 把 System 解析为 Light/Dark，读属性是线程安全的
            bool dark = JTThemeManager.ActualTheme == JTTheme.Dark;
            var uri = new Uri(
                dark
                    ? "pack://application:,,,/JTUI;component/Themes/Colors.Dark.xaml"
                    : "pack://application:,,,/JTUI;component/Themes/Colors.Light.xaml",
                UriKind.Absolute);

            _colors = new ResourceDictionary { Source = uri };
            return _colors;
        }

        private static void OnToastClosed(JTToastWindow t)
        {
            _stack.Remove(t);
            Relayout();
        }

        /// <summary>以锚点（鼠标处）为基准，从下往上摆放，最新一条贴近鼠标。</summary>
        /// <param name="instant">需要立即定位（不滑动）的那条，通常是刚新增的；其余条平滑补位。</param>
        private static void Relayout(JTToastWindow? instant = null)
        {
            double baseY = _anchor.Y - CursorOffsetY;
            double left = _anchor.X;

            double y = baseY;
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                var t = _stack[i];
                double h = t.ActualHeight > 0 ? t.ActualHeight : t.DesiredHeight;
                y -= h;
                t.MoveTo(left, y, animate: t != instant);   // 新条 instant：不滑动
                y -= Gap;
            }
        }

    }

    internal sealed class JTToastWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private readonly Action<JTToastWindow> _onClosed;
        private readonly Border _border;
        private readonly TextBlock _text;
        private bool _closed;

        public double DesiredHeight => Content is FrameworkElement fe ? fe.ActualHeight : 36;

        public JTToastWindow(string message, int ms, ResourceDictionary colors,
                             Action<JTToastWindow> onClosed)
        {
            _onClosed = onClosed;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            IsHitTestVisible = false;   // 点击穿透
            ShowActivated = false;      // 不抢焦点

            _text = new TextBlock
            {
                Text = message,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 280,
                VerticalAlignment = VerticalAlignment.Center
            };

            _border = new Border
            {
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 6, 12, 6),        
                Child = _text
            };

            Content = _border;
            ApplyColors(colors);

            Loaded += (_, _) => FadeIn();

            _timer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(ms)
            };
            _timer.Tick += (_, _) => { _timer.Stop(); FadeOutAndClose(); };
            _timer.Start();
        }

        /// <summary>套用一份配色字典（主题切换时复用）。</summary>
        public void ApplyColors(ResourceDictionary colors)
        {
            _border.Background = colors["JT.Toast.Background"] as Brush ?? Brushes.Gray;
            _border.BorderBrush = colors["JT.Toast.Border"] as Brush ?? Brushes.Transparent;
            _text.Foreground = colors["JT.Toast.Foreground"] as Brush ?? Brushes.White;
        }

        public void MoveTo(double left, double top, bool animate)
        {
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
            Left = left;

            if (animate)
            {
                var anim = new DoubleAnimation(Top, top, TimeSpan.FromMilliseconds(120));
                BeginAnimation(TopProperty, anim);
            }
            else
            {
                Top = top;         // 直接落到鼠标处，只靠 FadeIn 淡入
            }
        }


        private void FadeIn()
        {
            Opacity = 0;
            BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
        }

        private void FadeOutAndClose()
        {
            if (_closed) return;
            var anim = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(250));
            anim.Completed += (_, _) => CloseNow();
            BeginAnimation(OpacityProperty, anim);
        }

        public void CloseNow()
        {
            if (_closed) return;
            _closed = true;
            _timer.Stop();
            try { Close(); } catch { }
            _onClosed(this);
        }
    }

    internal static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        public static Point GetCursorScreenPoint()
        {
            if (GetCursorPos(out var p))
                return new Point(p.X, p.Y);
            return new Point(0, 0);
        }
    }
}
