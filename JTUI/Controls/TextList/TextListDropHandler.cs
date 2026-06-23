using System;
using System.Windows;
using GongSolutions.Wpf.DragDrop;

namespace JTUI.Controls.TextList
{
    /// <summary>JTTextList 的拖拽处理:内部排序 + 外部文本拖入 + 拖出携带纯文本。</summary>
    internal sealed class TextListDropHandler : IDropTarget, IDragSource
    {
        private readonly JTTextList _owner;

        public TextListDropHandler(JTTextList owner) => _owner = owner;

        // ===== 放置目标:排序 / 外部拖入 =====

        public void DragOver(IDropInfo dropInfo)
        {
            // 内部排序
            if (dropInfo.Data is JTTextItem && _owner.AllowReorderInternal)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Move;
                return;
            }
            // 外部拖入文本
            if (_owner.AllowDropImportInternal && HasText(dropInfo))
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
                dropInfo.Effects = DragDropEffects.Copy;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            // 内部排序
            if (dropInfo.Data is JTTextItem moving && _owner.AllowReorderInternal)
            {
                int oldIndex = _owner.Items_.IndexOf(moving);
                int newIndex = dropInfo.InsertIndex;
                if (oldIndex < 0) return;
                if (newIndex > oldIndex) newIndex--;
                if (newIndex < 0) newIndex = 0;
                if (newIndex > _owner.Items_.Count - 1) newIndex = _owner.Items_.Count - 1;
                if (newIndex != oldIndex)
                {
                    _owner.Items_.Move(oldIndex, newIndex);
                    _owner.NotifyListChanged();
                }
                return;
            }

            // 外部拖入文本
            // 外部拖入文本(整段作为一项,不拆行)
            if (_owner.AllowDropImportInternal && dropInfo.Data is IDataObject data)
            {
                string? text = ExtractText(data);
                if (!string.IsNullOrEmpty(text) && _owner.AddTextSilent(text))
                    _owner.NotifyListChanged();
            }
        }


        /// <summary>从拖放数据里取文本,优先 Unicode 避免浏览器中文乱码。</summary>
        private static string? ExtractText(IDataObject data)
        {
            // 1) Unicode 文本(CF_UNICODETEXT,UTF-16)——浏览器拖放首选
            if (data.GetDataPresent(DataFormats.UnicodeText))
                return data.GetData(DataFormats.UnicodeText) as string;

            // 2) 退回普通文本
            if (data.GetDataPresent(DataFormats.Text))
                return data.GetData(DataFormats.Text) as string;

            return null;
        }








        private static bool HasText(IDropInfo dropInfo) =>
            dropInfo.Data is IDataObject d && d.GetDataPresent(DataFormats.Text);

        // ===== 拖拽源:拖出携带文本 =====

        public void StartDrag(IDragInfo dragInfo)
        {
            if (dragInfo.SourceItem is JTTextItem vm)
            {
                var data = new DataObject();
                data.SetText(vm.Text);          // 外部文本框/浏览器接收的是纯文本
                dragInfo.Data = vm;             // 内部排序用对象本身
                dragInfo.DataObject = data;     // 拖到外部时用这个
                dragInfo.Effects = DragDropEffects.Copy | DragDropEffects.Move;
            }
        }

        public bool CanStartDrag(IDragInfo dragInfo) => dragInfo.SourceItem is JTTextItem;
        public void Dropped(IDropInfo dropInfo) { }
        public void DragDropOperationFinished(DragDropEffects operationResult, IDragInfo dragInfo) { }
        public void DragCancelled() { }
        public bool TryCatchOccurredException(Exception exception) => false;
    }
}
