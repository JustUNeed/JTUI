using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using GongSolutions.Wpf.DragDrop;

namespace JTUI.Controls
{
    /// <summary>
    /// JTGridList 的放置处理器:
    ///  - 内部排序:复用 gong 的 DefaultDropHandler(它直接操作可写 ObservableCollection),
    ///    在其前后包三段式事件(ItemsReordering 可取消 / ItemsReordered)。
    ///  - 外部拖入:调用用户 DropHandler 翻译为对象并插入集合。
    /// </summary>
    internal sealed class InternalDropTarget : DefaultDropHandler
    {
        private readonly JTGridList _owner;

        public InternalDropTarget(JTGridList owner) => _owner = owner;

        public override void DragOver(IDropInfo dropInfo)
        {
            // 内部排序(同源)
            if (IsInternalDrag(dropInfo))
            {
                if (_owner.AllowReorder)
                    base.DragOver(dropInfo);            // 用 gong 默认判断
                else
                    dropInfo.Effects = DragDropEffects.None;
                return;
            }

            // 外部拖入
            if (_owner.AllowDropImport && _owner.DropHandler != null && AcceptExternal(dropInfo))
            {
                dropInfo.Effects = DragDropEffects.Copy;
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
            }
            else
            {
                dropInfo.Effects = DragDropEffects.None;
            }
        }

        public override void Drop(IDropInfo dropInfo)
        {
            // 内部排序
            if (IsInternalDrag(dropInfo))
            {
                if (!_owner.AllowReorder) return;

                var list = _owner.MutableSource;
                object? moved = dropInfo.DragInfo?.SourceItem;
                int from = dropInfo.DragInfo?.SourceIndex ?? -1;

                if (list == null || moved == null || from < 0) return;

                int insertIndex = dropInfo.UnfilteredInsertIndex;
                int to = insertIndex;
                if (insertIndex > from) to = insertIndex - 1;
                to = Math.Max(0, Math.Min(to, list.Count - 1));
                if (from == to) return;

                // 前置可取消
                var cancel = new JTGridReorderCancelEventArgs(moved, from, to);
                _owner.RaiseReordering(cancel);
                if (cancel.Cancel) return;

                // ★ 自己搬运:优先用 ObservableCollection.Move(界面只移动一个容器,顺滑)
                if (!TryObservableMove(list, from, to))
                {
                    // 退路:普通 IList 的移除+插入
                    list.RemoveAt(from);
                    list.Insert(to, moved);
                }

                _owner.RaiseReordered(new JTGridReorderEventArgs(moved, from, to));
                return;
            }

            // 外部拖入
            if (_owner.AllowDropImport && _owner.DropHandler != null)
                DoExternalDrop(dropInfo);
        }

        /// <summary>若是 ObservableCollection&lt;T&gt;,调用其 Move 方法(反射),返回是否成功。</summary>
        private static bool TryObservableMove(IList list, int from, int to)
        {
            var type = list.GetType();
            // ObservableCollection<T> 有 public void Move(int, int)
            var move = type.GetMethod("Move",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public,
                null, new[] { typeof(int), typeof(int) }, null);
            if (move == null) return false;
            move.Invoke(list, new object[] { from, to });
            return true;
        }





        // ---------- 外部拖入 ----------

        private bool AcceptExternal(IDropInfo dropInfo)
        {
            if (dropInfo.Data is not IDataObject data) return false;
            return _owner.CanAcceptDrop?.Invoke(data, dropInfo.TargetItem) ?? true;
        }

        private void DoExternalDrop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is not IDataObject data) return;

            int index = dropInfo.UnfilteredInsertIndex;
            var produced = _owner.DropHandler!(data, dropInfo.TargetItem, index);
            if (produced == null) return;

            var list = _owner.MutableSource;
            var inserted = new List<object>();
            if (list != null)
            {
                int i = Math.Max(0, Math.Min(index, list.Count));
                foreach (var obj in produced)
                {
                    if (obj == null) continue;
                    list.Insert(i++, obj);
                    inserted.Add(obj);
                }
            }
            else
            {
                inserted.AddRange(produced.Where(o => o != null)!);
            }

            if (inserted.Count > 0)
                _owner.RaiseDropped(new JTGridDropEventArgs(inserted, index));
        }

        // ---------- 工具 ----------

        // gong:同框架内拖拽时 DragInfo 非空;外部(资源管理器等)拖入时为 null
        private static bool IsInternalDrag(IDropInfo dropInfo) => dropInfo.DragInfo != null;

        private static object? ExtractFirst(object data)
        {
            if (data is string) return data;
            if (data is IEnumerable en) return en.Cast<object>().FirstOrDefault();
            return data;
        }
    }
}
