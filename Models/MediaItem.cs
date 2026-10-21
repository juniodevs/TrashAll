using System;
using System.IO;
using System.Windows.Media.Imaging;
using TrashAll.Helpers;

namespace TrashAll.Models
{
    public class MediaItem
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string DirectoryPath { get; set; } = string.Empty;
        public MediaType Type { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string CategoryIcon { get; set; } = "📁";
        public long FileSizeBytes { get; set; }
        public string FormattedSize { get; set; } = string.Empty;
        public DateTime DateModified { get; set; }
        public string FormattedDate { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public bool IsSelected { get; set; } = false;
        public bool IsDuplicate { get; set; } = false;
        public string? DuplicateGroupId { get; set; }
        public bool IsDuplicateCopy { get; set; } = false;
        public string DuplicateBadgeText => IsDuplicate ? (IsDuplicateCopy ? "👯 Cópia" : "⭐ Original") : string.Empty;

        private BitmapSource? _fileIcon;
        public BitmapSource? FileIcon
        {
            get
            {
                if (_fileIcon == null && !string.IsNullOrEmpty(FilePath))
                {
                    _fileIcon = IconHelper.GetIcon(FilePath);
                }
                return _fileIcon;
            }
            set => _fileIcon = value;
        }

        public MediaItem(string filePath, MediaType type, long sizeBytes, DateTime dateModified)
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            DirectoryPath = Path.GetDirectoryName(filePath) ?? string.Empty;
            Type = type;
            FileSizeBytes = sizeBytes;
            FormattedSize = FormatBytes(sizeBytes);
            DateModified = dateModified;
            FormattedDate = dateModified.ToString("dd/MM/yyyy HH:mm");
            Extension = Path.GetExtension(filePath).ToUpperInvariant();

            (CategoryName, CategoryIcon) = type switch
            {
                MediaType.Image => ("Imagem", "🖼️"),
                MediaType.Video => ("Vídeo", "🎬"),
                MediaType.Audio => ("Áudio", "🎵"),
                MediaType.Executable => ("Executável", "⚙️"),
                MediaType.Archive => ("Compactado", "📦"),
                MediaType.Document => ("Documento", "📄"),
                _ => ("Outro", "📁")
            };
        }

        public static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }
    }
}
