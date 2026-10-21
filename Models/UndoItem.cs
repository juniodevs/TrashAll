namespace TrashAll.Models
{
    public class UndoItem
    {
        public MediaItem Item { get; set; }
        public int OriginalIndex { get; set; }

        public UndoItem(MediaItem item, int originalIndex)
        {
            Item = item;
            OriginalIndex = originalIndex;
        }
    }
}
