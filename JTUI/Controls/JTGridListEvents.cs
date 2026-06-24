using System.Collections.Generic;
using System.ComponentModel;

namespace JTUI.Controls
{
    public class JTGridItemEventArgs : System.EventArgs
    {
        public JTGridItemEventArgs(object item) => Item = item;
        public object Item { get; }
    }

    public class JTGridItemCancelEventArgs : CancelEventArgs
    {
        public JTGridItemCancelEventArgs(object item) => Item = item;
        public object Item { get; }
    }

    public class JTGridReorderEventArgs : System.EventArgs
    {
        public JTGridReorderEventArgs(object item, int oldIndex, int newIndex)
        { Item = item; OldIndex = oldIndex; NewIndex = newIndex; }
        public object Item { get; }
        public int OldIndex { get; }
        public int NewIndex { get; }
    }

    public class JTGridReorderCancelEventArgs : CancelEventArgs
    {
        public JTGridReorderCancelEventArgs(object item, int oldIndex, int newIndex)
        { Item = item; OldIndex = oldIndex; NewIndex = newIndex; }
        public object Item { get; }
        public int OldIndex { get; }
        public int NewIndex { get; }
    }

    public class JTGridDropEventArgs : System.EventArgs
    {
        public JTGridDropEventArgs(IReadOnlyList<object> insertedItems, int insertIndex)
        { InsertedItems = insertedItems; InsertIndex = insertIndex; }
        public IReadOnlyList<object> InsertedItems { get; }
        public int InsertIndex { get; }
    }
}
