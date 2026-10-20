using System;
using System.Collections.Generic;

namespace TrashAll.Models
{
    public class ZipEntryItem
    {
        public string Name { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string FormattedSize { get; set; } = string.Empty;
        public string FormattedDate { get; set; } = string.Empty;
        public string Icon { get; set; } = "📄";
        public bool IsDirectory { get; set; }
    }

    public class ZipPreviewData
    {
        public string ZipFileName { get; set; } = string.Empty;
        public int TotalEntries { get; set; }
        public long TotalUncompressedBytes { get; set; }
        public string FormattedTotalSize { get; set; } = string.Empty;
        public List<ZipEntryItem> Entries { get; set; } = new();
        public bool HasMoreEntries { get; set; }
        public string MoreEntriesNotice => HasMoreEntries ? $"+ {TotalEntries - Entries.Count} itens adicionais..." : string.Empty;
    }
}
