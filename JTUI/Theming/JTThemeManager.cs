using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace JTUI.Theming
{
    /// <summary>
    /// JTUI 全局主题管理器。
    /// 负责注入控件样式、切换明暗主题、并将用户选择持久化到本地。
    /// </summary>
    public static class JTThemeManager
    {
        // ---------- 资源路径 ----------
        private const string StylesSource =
            "pack://application:,,,/JTUI;component/Themes/JTStyles.xaml";
        private const string LightSource =
            "pack://application:,,,/JTUI;component/Themes/Colors.Light.xaml";
        private const string DarkSource =
            "pack://application:,,,/JTUI;component/Themes/Colors.Dark.xaml";

        // ---------- 状态 ----------
        private static JTTheme _current = JTTheme.System;
        private static bool _stylesInjected;
        private static bool _autoPersist = true;

        /// <summary>主题发生变化时触发。</summary>
        public static event EventHandler? ThemeChanged;

        /// <summary>是否自动持久化主题选择(默认开启)。</summary>
        public static bool AutoPersist
        {
            get => _autoPersist;
            set => _autoPersist = value;
        }

        /// <summary>当前主题设置(可能为 System)。</summary>
        public static JTTheme Current
        {
            get => _current;
            set
            {
                if (_current == value) return;
                _current = value;
                ApplyColors(value);
                if (_autoPersist) Save(value);
                ThemeChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        /// <summary>实际生效的主题(把 System 解析为 Light 或 Dark)。</summary>
        public static JTTheme ActualTheme =>
            _current == JTTheme.System
                ? (IsSystemDark() ? JTTheme.Dark : JTTheme.Light)
                : _current;

        // ---------- 公开方法 ----------

        /// <summary>
        /// 应用启动时调用一次。
        /// 注入控件样式,并优先恢复本地持久化的主题;无记录时使用 fallback。
        /// </summary>
        public static void Initialize(JTTheme fallback = JTTheme.System)
        {
            EnsureStylesInjected();
            var persisted = LoadPersisted();
            _current = persisted ?? fallback;
            ApplyColors(_current);
        }

        /// <summary>在明暗之间一键切换。</summary>
        public static void Toggle()
            => Current = ActualTheme == JTTheme.Dark ? JTTheme.Light : JTTheme.Dark;

        // ---------- 样式注入(全局仅一次) ----------

        private static void EnsureStylesInjected()
        {
            if (_stylesInjected) return;
            var app = Application.Current;
            if (app is null) return;

            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri(StylesSource, UriKind.Absolute)
            });
            _stylesInjected = true;
        }

        // ---------- 颜色字典切换 ----------

        private static void ApplyColors(JTTheme theme)
        {
            var app = Application.Current;
            if (app is null) return;

            var actual = theme == JTTheme.System
                ? (IsSystemDark() ? JTTheme.Dark : JTTheme.Light)
                : theme;

            var newUri = new Uri(
                actual == JTTheme.Dark ? DarkSource : LightSource,
                UriKind.Absolute);

            var merged = app.Resources.MergedDictionaries;

            // 移除旧的颜色字典
            var existing = merged.FirstOrDefault(d =>
                d.Source is not null &&
                (d.Source.OriginalString.Contains("Colors.Light.xaml") ||
                 d.Source.OriginalString.Contains("Colors.Dark.xaml")));
            if (existing is not null)
                merged.Remove(existing);

            // 加入新的颜色字典
            merged.Add(new ResourceDictionary { Source = newUri });
        }

        // ---------- 持久化 ----------

        private record ThemeConfig(string Theme);

        private static string ConfigPath
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "JTUI");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "theme.json");
            }
        }

        private static void Save(JTTheme theme)
        {
            try
            {
                var json = JsonSerializer.Serialize(new ThemeConfig(theme.ToString()));
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // 写入失败(只读磁盘/权限等)不影响运行,静默忽略
            }
        }

        private static JTTheme? LoadPersisted()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return null;
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<ThemeConfig>(json);
                if (cfg is not null && Enum.TryParse<JTTheme>(cfg.Theme, out var t))
                    return t;
            }
            catch
            {
                // 文件损坏当作无记录
            }
            return null;
        }

        // ---------- 系统主题探测 ----------

        private static bool IsSystemDark()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var value = key?.GetValue("AppsUseLightTheme");
                return value is int i && i == 0; // 0 = 深色
            }
            catch
            {
                return false;
            }
        }
    }
}
