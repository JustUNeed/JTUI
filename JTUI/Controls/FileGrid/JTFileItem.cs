using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace JTUI.Controls.FileGrid
{
    /// <summary>文件项类型。</summary>
    public enum JTFileKind
    {
        File,       // 普通文件
        Folder,     // 文件夹
        Shortcut    // 快捷方式(.lnk / .url)
    }

    /// <summary>
    /// 文件项:承载路径、显示名、类型,以及后台提取出的系统图标。
    /// 仅保存路径列表用,不扫描目录。
    /// </summary>
    public class JTFileItem : INotifyPropertyChanged
    {
        public JTFileItem(string path)
        {
            Path = path;
            Kind = DetectKind(path);
            DisplayName = BuildDisplayName(path, Kind);
        }

        /// <summary>文件 / 文件夹 / 快捷方式的完整路径。</summary>
        public string Path { get; }

        /// <summary>项类型。</summary>
        public JTFileKind Kind { get; }

        /// <summary>显示名称(快捷方式去掉 .lnk 后缀,文件夹用目录名)。</summary>
        public string DisplayName { get; }

        private ImageSource? _icon;
        /// <summary>后台提取出的系统关联图标。</summary>
        public ImageSource? Icon
        {
            get => _icon;
            internal set => Set(ref _icon, value);
        }

        private static JTFileKind DetectKind(string path)
        {
            if (Directory.Exists(path)) return JTFileKind.Folder;

            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".lnk" || ext == ".url") return JTFileKind.Shortcut;

            return JTFileKind.File;
        }

        private static string BuildDisplayName(string path, JTFileKind kind)
        {
            // 文件夹:取目录名(去掉末尾分隔符后的最后一段)
            if (kind == JTFileKind.Folder)
            {
                string trimmed = path.TrimEnd(System.IO.Path.DirectorySeparatorChar,
                                              System.IO.Path.AltDirectorySeparatorChar);
                string name = System.IO.Path.GetFileName(trimmed);
                return string.IsNullOrEmpty(name) ? trimmed : name;   // 处理盘符根(如 "D:\")
            }

            // 快捷方式 / 文件:去掉扩展名更像启动器(文件保留扩展更直观,这里保留无扩展名)
            return System.IO.Path.GetFileNameWithoutExtension(path);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
