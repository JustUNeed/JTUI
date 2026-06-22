using System.Windows;

namespace JTUI.Controls.ImageGrid
{
    /// <summary>携带图片路径的事件参数。</summary>
    public class JTImageEventArgs : RoutedEventArgs
    {
        public JTImageEventArgs(RoutedEvent routedEvent, object source, string imagePath)
            : base(routedEvent, source) => ImagePath = imagePath;

        /// <summary>被操作图片的完整路径。</summary>
        public string ImagePath { get; }
    }
}
