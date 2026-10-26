using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TrashAll.Models;

namespace TrashAll.Services
{
    public class MediaScannerService
    {
        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".tiff", ".tif", ".jfif", ".ico", ".psd", ".psb", ".svg"
        };

        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mkv", ".mov", ".webm", ".avi", ".wmv", ".m4v", ".flv", ".3gp"
        };

        private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".flac", ".m4a", ".ogg", ".aac", ".wma", ".opus"
        };

        private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".msi", ".bat", ".cmd", ".iso", ".img", ".apk", ".jar"
        };

        private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".cab"
        };

        private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".epub", ".md", ".rtf", ".log"
        };

        public async Task ScanDirectoryAsync(
            string folderPath,
            bool recursive,
            Action<List<MediaItem>> onBatchFound,
            CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                if (!Directory.Exists(folderPath)) return;

                var options = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = recursive,
                    AttributesToSkip = FileAttributes.ReparsePoint
                };

                var buffer = new List<MediaItem>();
                DateTime lastReport = DateTime.UtcNow;
                bool isFirstItem = true;

                try
                {
                    foreach (var file in Directory.EnumerateFiles(folderPath, "*.*", options))
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        string ext = Path.GetExtension(file);
                        MediaType type = DetermineType(ext);

                        try
                        {
                            var fi = new FileInfo(file);
                            buffer.Add(new MediaItem(file, type, fi.Length, fi.LastWriteTime));

                            if (isFirstItem)
                            {
                                isFirstItem = false;
                                onBatchFound(new List<MediaItem>(buffer));
                                buffer.Clear();
                                lastReport = DateTime.UtcNow;
                            }
                            else if (buffer.Count >= 30 || (DateTime.UtcNow - lastReport).TotalMilliseconds >= 80)
                            {
                                onBatchFound(new List<MediaItem>(buffer));
                                buffer.Clear();
                                lastReport = DateTime.UtcNow;
                            }
                        }
                        catch
                        {
                        }
                    }

                    if (buffer.Count > 0 && !cancellationToken.IsCancellationRequested)
                    {
                        onBatchFound(new List<MediaItem>(buffer));
                        buffer.Clear();
                    }
                }
                catch
                {
                }
            }, cancellationToken);
        }

        public static MediaType DetermineType(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension)) return MediaType.Other;
            if (ImageExtensions.Contains(extension)) return MediaType.Image;
            if (VideoExtensions.Contains(extension)) return MediaType.Video;
            if (AudioExtensions.Contains(extension)) return MediaType.Audio;
            if (ExecutableExtensions.Contains(extension)) return MediaType.Executable;
            if (ArchiveExtensions.Contains(extension)) return MediaType.Archive;
            if (DocumentExtensions.Contains(extension)) return MediaType.Document;
            return MediaType.Other;
        }
    }
}
