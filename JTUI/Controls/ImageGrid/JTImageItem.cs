using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace JTUI.Controls.ImageGrid
{
    /// <summary>图片项:只承载路径和当场解码出的缩略图。</summary>
    public class JTImageItem : INotifyPropertyChanged
    {
        public JTImageItem(string path) => Path = path;

        /// <summary>图片文件完整路径。</summary>
        public string Path { get; }

        private ImageSource? _thumbnail;
        public ImageSource? Thumbnail
        {
            get => _thumbnail;
            internal set => Set(ref _thumbnail, value);
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
