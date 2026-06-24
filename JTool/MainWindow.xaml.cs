using JTUI.Controls;
using JTUI.Controls.FolderBin;
using JTUI.Controls.ImageGrid;
using JTUI.Controls.Viewer;
using JTUI.Notifications;
using JTUI.Services;
using JTUI.Theming;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using static JTUI.Controls.ImageGrid.JTImageGrid;

namespace JTool
{
    public partial class MainWindow : JTWindow
    {
        public ObservableCollection<string> Cards { get; } =
            new() { "0", "1", "2", "3", "4", "5", "6", "7" };

        public MainWindow()
        {
            InitializeComponent();

            DataContext = this;   // ★ 关键:让 {Binding Cards} 能找到上面的属性


            // 拖入:把外部文件路径翻译成字符串项,控件负责插入集合
            Grid.DropHandler = (data, target, index) =>
            {
                if (data.GetDataPresent(DataFormats.FileDrop) &&
                    data.GetData(DataFormats.FileDrop) is string[] files)
                    return files;                       // 返回要插入的对象(这里就是路径字符串)

                if (data.GetDataPresent(DataFormats.Text))
                    return new[] { (object)data.GetData(DataFormats.Text)! };

                return null;
            };

            // 可选:控制哪些拖入被接受(影响鼠标效果)
            Grid.CanAcceptDrop = (data, target) =>
                data.GetDataPresent(DataFormats.FileDrop) || data.GetDataPresent(DataFormats.Text);

            // 拖出:把选中项打包成文本(若是真实文件路径,可改塞 FileDrop)
            Grid.DragOutHandler = items =>
            {
                var paths = items.OfType<string>().ToArray();
                if (paths.Length == 0) return null;

                var dataObj = new DataObject();
                dataObj.SetData(DataFormats.Text, string.Join(Environment.NewLine, paths));
                // 若 paths 是真实存在的文件,可同时:
                // dataObj.SetData(DataFormats.FileDrop, paths);
                return dataObj;
            };

            Grid.ItemsDropped += (_, e) => System.Diagnostics.Debug.WriteLine($"拖入 {e.InsertedItems.Count} 项");
            Grid.ItemsReordered += (_, e) =>
     System.Diagnostics.Debug.WriteLine($"排序 {e.OldIndex}->{e.NewIndex},数据顺序:{string.Join(",", Cards)}");


            var dir = @"C:\Users\JUNPC\Desktop\葵";
            if (System.IO.Directory.Exists(dir))
                ImageGrid.ImageDirectory = dir;


            // 自定义校验规则(可选):禁止逗号、限制 20 字
            Tags.TagValidator = raw =>
            {
                var s = raw.Trim();
                if (s.Length == 0 || s.Length > 20 || s.Contains(',')) return (false, s);
                return (true, s);
            };



            // 左键:复制图片到剪贴板
            ImageGrid.ImageLeftClick += path =>
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
            ImageGrid.ImageRightClick += path =>
            {
                var win = new JTWindow { Width = 1000, Height = 700, Title = "预览" };
                var viewer = new JTImageViewer { ImagePath = path };   // 自动用同目录构建翻页列表
                win.Content = viewer;
                win.Show();
            };

            ImageGrid.ImageImported += path => MessageBox.Show($"已添加 {System.IO.Path.GetFileName(path)}");
            ImageGrid.ImportFailed += (reason, src) => JTToast.Show("导入失败"); ;
            ImageGrid.ImageDeleted += path => System.IO.File.Delete(path);







            //// 投放结果反馈
            //Bin.Dropped += r =>
            //{
            //    switch (r.Kind)
            //    {
            //        case JTFolderDropKind.MovedFile:
            //            Console.WriteLine($"已移动 {r.Source} → {r.ResultPath}"); break;
            //        case JTFolderDropKind.DownloadedUrl:
            //            Console.WriteLine($"已下载 {r.Source} → {r.ResultPath}"); break;
            //        case JTFolderDropKind.Failed:
            //            Console.WriteLine($"失败 {r.Source}: {r.Error}"); break;
            //    }
            //};

            //// 点击格子(外部决定:比如打开文件夹)
            //Bin.ItemClicked += path =>
            //    Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{path}\"", UseShellExecute = true });




            JTToast.Show("已保存");
            JTToast.Show("文件已复制到剪贴板", 1500);

            JTToast.Show("已保存");
            JTToast.Show("文件已复制到剪贴板", 1500);


        }



        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is { } item)
                Grid.DeleteItem(item);
            e.Handled = true;
        }

        private void PasteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ImageGrid.PasteFromClipboard())
                MessageBox.Show("剪贴板里没有图片");
        }



        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
        {

            JTToast.Show("已保存");
          //  JTThemeManager.Toggle();
           
       

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
