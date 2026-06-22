using JTUI.Controls;
using JTUI.Controls.ImageGrid;
using JTUI.Controls.Viewer;
using JTUI.Services;
using JTUI.Theming;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using static JTUI.Controls.ImageGrid.JTImageGrid;

namespace JTool
{
    public partial class MainWindow : JTWindow
    {

        public MainWindow()
        {
            InitializeComponent();

            // 自定义校验规则(可选):禁止逗号、限制 20 字
            Tags.TagValidator = raw =>
            {
                var s = raw.Trim();
                if (s.Length == 0 || s.Length > 20 || s.Contains(',')) return (false, s);
                return (true, s);
            };



            // 左键:复制图片到剪贴板
            Grid.ImageLeftClick += path =>
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;   // 读完即释放文件句柄
                    bmp.UriSource = new Uri(path);
                    bmp.EndInit();
                    bmp.Freeze();

                    Clipboard.SetImage(bmp);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"复制失败:{ex.Message}");
                }
            };

            // 右键:打开预览窗口
            Grid.ImageRightClick += path =>
            {
                var win = new JTWindow { Width = 1000, Height = 700, Title = "预览" };
                var viewer = new JTImageViewer { ImagePath = path };   // 自动用同目录构建翻页列表
                win.Content = viewer;
                win.Show();
            };

            Grid.ImageImported += path => MessageBox.Show($"已添加 {System.IO.Path.GetFileName(path)}");
            Grid.ImportFailed += (reason, src) => MessageBox.Show($"导入失败({reason}):{src}");
            Grid.ImageDeleted += path => System.IO.File.Delete(path);

        }

        private void PasteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Grid.PasteFromClipboard())
                MessageBox.Show("剪贴板里没有图片");
        }



        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
        {
         
                JTThemeManager.Toggle();
           
       

        }

        private void ExitMenu_Click(object sender, RoutedEventArgs e) {

            // 也可以自定义标题和父窗口
            bool ok = JTConfirmDialog.Show("未保存的修改将丢失，仍要退出？", "退出确认", this);
            if (ok)
            {
                Close();
            }

        } 

        private void ThemeLight_Click(object sender, RoutedEventArgs e)
            => JTThemeManager.Current = JTTheme.Light;

        private void ThemeDark_Click(object sender, RoutedEventArgs e)
            => JTThemeManager.Current = JTTheme.Dark;

        private void ThemeSystem_Click(object sender, RoutedEventArgs e)
            => JTThemeManager.Current = JTTheme.System;
        // 监听点击,拿到 tag 值
        private void Tags_TagClicked(object sender, TagChangedEventArgs e)
            => MessageBox.Show($"点击了:{e.Text}");

        // 校验失败提示
        private void Tags_TagRejected(object sender, TagRejectedEventArgs e)
            => Console.WriteLine($"被拒绝:{e.Text} —— {e.Reason}");







    }



}
