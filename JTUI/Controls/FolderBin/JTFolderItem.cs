using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace JTUI.Controls.FolderBin
{
    /// <summary>文件夹格子项:承载文件夹路径与显示名。</summary>
    public class JTFolderItem : INotifyPropertyChanged
    {
        public JTFolderItem(string path)
        {
            Path = path;
            DisplayName = BuildDisplayName(path);
        }

        /// <summary>文件夹完整路径。</summary>
        public string Path { get; }

        /// <summary>显示名(文件夹名)。</summary>
        public string DisplayName { get; }

        // 拖入高亮状态(供模板触发器用)
        private bool _isDropActive;
        public bool IsDropActive
        {
            get => _isDropActive;
            internal set => Set(ref _isDropActive, value);
        }

        private static string BuildDisplayName(string path)
        {
            string trimmed = path.TrimEnd(
                System.IO.Path.DirectorySeparatorChar,
                System.IO.Path.AltDirectorySeparatorChar);
            string name = System.IO.Path.GetFileName(trimmed);
            return string.IsNullOrEmpty(name) ? trimmed : name;   // 处理盘符根 "D:\"
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
