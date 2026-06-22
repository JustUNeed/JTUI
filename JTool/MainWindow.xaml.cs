using JTUI.Controls;
using JTUI.Controls.ImageGrid;
using JTUI.Theming;
using System.Windows;

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


        private void Grid_ImageLeftClick(object sender, JTImageEventArgs e)
        {
            string path = e.ImagePath;   // 左键点击的图片路径
        }

        private void Grid_ImageRightClick(object sender, JTImageEventArgs e)
        {
            string path = e.ImagePath;   // 右键点击的图片路径
        }

        private void Grid_ImageDelete(object sender, JTImageEventArgs e)
        {
            string path = e.ImagePath;   // 被删除的图片路径
                                         // 控件已从列表移除;若要删磁盘文件,自己在这里删:
                                         // System.IO.File.Delete(path);
        }

    }
}
