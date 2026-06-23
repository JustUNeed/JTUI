using System;
using System.Linq;
using System.Windows;
using GongSolutions.Wpf.DragDrop;
using GongSolutions.Wpf.DragDrop.Utilities;

namespace JTUI.Controls.FileGrid
{
    /// <summary>
    /// JTFileGrid 的拖放处理器:
    /// - 列表内拖拽 → 重排顺序(受 AllowReorder 控制),完成后触发 ListChanged;
    /// - 从外部拖入文件 → 追加(受 AllowDropImport 控制),完成后触发 ListChanged。
    /// </summary>
    internal sealed class FileGridDropHandler : IDropTarget
    {
        private readonly JTFileGrid _grid;

        public FileGridDropHandler(JTFileGrid grid) => _grid = grid;

        public void DragOver(IDropInfo dropInfo)
        {
            // 1) 列表内重排:拖动的源就是 JTFileItem
            if (dropInfo.Data is JTFileItem || dropInfo.Data is System.Collections.IEnumerable seq
                && seq.Cast<object>().All(o => o is JTFileItem))
            {
                if (!_grid.AllowReorderInternal) { dropInfo.Effects = DragDropEffects.None; return; }
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Move;
                return;
            }

            // 2) 外部文件拖入
            if (_grid.AllowDropImportInternal &&
       dropInfo.Data is IDataObject data && data.GetDataPresent(DataFormats.FileDrop))
            {
                var paths = data.GetData(DataFormats.FileDrop) as string[];
                bool hasFile = paths != null && paths.Any(p =>
                    !string.IsNullOrWhiteSpace(p) && !System.IO.Directory.Exists(p));

                if (hasFile)
                {
                    dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                    dropInfo.Effects = DragDropEffects.Copy;
                }
                else
                {
                    dropInfo.Effects = DragDropEffects.None;   // 全是文件夹 → 禁止
                }
                return;
            }

            dropInfo.Effects = DragDropEffects.None;
        }

        public void Drop(IDropInfo dropInfo)
        {
            // ---- 外部文件拖入 ----
            if (dropInfo.Data is IDataObject data && data.GetDataPresent(DataFormats.FileDrop))
            {
                if (!_grid.AllowDropImportInternal) return;
                if (data.GetData(DataFormats.FileDrop) is not string[] paths) return;

                int insertIndex = dropInfo.InsertIndex;
                bool any = false;
                foreach (var p in paths)
                {
                    if (string.IsNullOrWhiteSpace(p)) continue;
                    if (System.IO.Directory.Exists(p)) continue;        // ★ 跳过文件夹，只收文件
                    if (_grid.DistinctInternal && _grid.ContainsPath(p)) continue;
                    var item = new JTFileItem(p);
                    if (insertIndex >= 0 && insertIndex <= _grid.Items_.Count)
                        _grid.Items_.Insert(insertIndex++, item);
                    else
                        _grid.Items_.Add(item);
                    any = true;
                }

                if (any) _grid.NotifyListChanged();
                return;
            }

            // ---- 列表内重排 ----
            if (!_grid.AllowReorderInternal) return;

            var collection = _grid.Items_;

            // 取被拖动的项(支持多选)
            var dragged = dropInfo.Data is System.Collections.IEnumerable e && dropInfo.Data is not string
                ? e.Cast<object>().OfType<JTFileItem>().ToList()
                : new System.Collections.Generic.List<JTFileItem>();
            if (dragged.Count == 0 && dropInfo.Data is JTFileItem single)
                dragged.Add(single);
            if (dragged.Count == 0) return;

            int insert = dropInfo.InsertIndex;

            // 先按原始顺序移除,同时校正插入点(被移除的、位于插入点之前的项会让插入点左移)
            foreach (var item in dragged)
            {
                int oldIndex = collection.IndexOf(item);
                if (oldIndex < 0) continue;
                collection.RemoveAt(oldIndex);
                if (oldIndex < insert) insert--;
            }

            if (insert < 0) insert = 0;
            if (insert > collection.Count) insert = collection.Count;

            foreach (var item in dragged)
                collection.Insert(insert++, item);

            _grid.NotifyListChanged();
        }
    }
}
