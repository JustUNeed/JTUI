using System.Linq;
using System.Windows;

namespace JTUI.Controls
{
    /// <summary>
    /// JTUI 简易确认框：一段提示文字 + “是 / 否”两个按钮，复用 JTWindow 的无边框外观与主题。
    /// 通过静态方法 <see cref="Show(string, string, Window?)"/> 使用，返回 true 表示点了“是”。
    /// </summary>
    public class JTConfirmDialog : JTWindow
    {
        private JTConfirmDialog(string message, string title)
        {
            Title = title;
            Width = 360;
            MaxWidth = 480;                       // 防止超长单行把窗口撑太宽
            SizeToContent = SizeToContent.Height; // 高度自动跟随内容（含换行）
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            // 关键：关掉 WindowChrome 的可缩放边框，
            // 否则 SizeToContent 会把这圈厚度也算进去，在底部留出黑条
            var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(this);
            if (chrome is not null)
            {
                chrome.ResizeBorderThickness = new Thickness(0);
                chrome.GlassFrameThickness = new Thickness(0);
                chrome.CaptionHeight = 32;
            }

            // 让独立窗口稳定拿到主题资源（避免按钮退化成原生样式）
            if (Application.Current is not null)
            {
                foreach (var md in Application.Current.Resources.MergedDictionaries)
                    Resources.MergedDictionaries.Add(md);
            }

            Content = BuildContent(message);
        }



        private System.Windows.Controls.Grid BuildContent(string message)
        {
            var root = new System.Windows.Controls.Grid { Margin = new Thickness(8) };
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition
            {
                Height = GridLength.Auto
            });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition
            {
                Height = new GridLength(16)
            });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition
            {
                Height = GridLength.Auto
            });

            // 提示文字
            var text = new System.Windows.Controls.TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 420,        // 配合窗口 Width，超过就换行
                FontSize = 13,
                LineHeight = 20
            };
            System.Windows.Controls.Grid.SetRow(text, 0);
            root.Children.Add(text);

            // 按钮区（右对齐）
            var buttons = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var yesButton = new JTButton
            {
                Content = "是",
                Width = 72,
                Height = 30,
                
                IsDefault = true,
                Margin = new Thickness(0, 0, 8, 0)
            };

            yesButton.Click += (_, _) => { DialogResult = true; };

            var noButton = new JTButton
            {
                Content = "否",
                Width = 72,
                Height = 30,
              
                IsCancel = true   // Esc 即取消，等同点“否”
            };
            noButton.Click += (_, _) => { DialogResult = false; };

            buttons.Children.Add(yesButton);
            buttons.Children.Add(noButton);
            System.Windows.Controls.Grid.SetRow(buttons, 2);
            root.Children.Add(buttons);

            return root;
        }

        /// <summary>
        /// 弹出“是 / 否”确认框（模态）。返回 true 表示点了“是”，false 表示点了“否”、按了 Esc 或关闭窗口。
        /// </summary>
        /// <param name="message">提示内容。</param>
        /// <param name="title">标题栏文字，默认“确认”。</param>
        /// <param name="owner">父窗口，传入后会居中于父窗口；不传则尝试用当前活动窗口。</param>
        public static bool Show(string message, string title = "确认", Window? owner = null)
        {
            var dialog = new JTConfirmDialog(message, title);

            owner ??= Application.Current?.Windows.OfType<Window>()
                .FirstOrDefault(w => w.IsActive);
            if (owner is not null && owner != dialog)
                dialog.Owner = owner;
            else
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            return dialog.ShowDialog() == true;
        }
    }
}
