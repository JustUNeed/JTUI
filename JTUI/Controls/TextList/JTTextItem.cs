using System.ComponentModel;

namespace JTUI.Controls.TextList
{
    /// <summary>文字列表的单项,只承载一段文本。</summary>
    public class JTTextItem : INotifyPropertyChanged
    {
        private string _text;

        public JTTextItem(string text) => _text = text ?? string.Empty;

        public string Text
        {
            get => _text;
            set
            {
                if (_text == value) return;
                _text = value ?? string.Empty;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
