using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using TrashAll.Models;

namespace TrashAll.Services
{
    public class ZipInspectorService
    {
        public async Task<ZipPreviewData?> InspectZipAsync(string zipPath, CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                if (!File.Exists(zipPath)) return null;

                try
                {
                    using var fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var archive = new ZipArchive(fs, ZipArchiveMode.Read);

                    var data = new ZipPreviewData
                    {
                        ZipFileName = Path.GetFileName(zipPath),
                        TotalEntries = archive.Entries.Count
                    };

                    long totalBytes = 0;
                    int count = 0;
                    const int maxDisplayEntries = 80;

                    foreach (var entry in archive.Entries)
                    {
                        if (ct.IsCancellationRequested) return null;

                        totalBytes += entry.Length;

                        if (count < maxDisplayEntries)
                        {
                            bool isDir = entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\");
                            string ext = Path.GetExtension(entry.Name).ToLowerInvariant();

                            string icon = isDir ? "📁" : ext switch
                            {
                                ".exe" or ".msi" or ".bat" or ".cmd" => "⚙️",
                                ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" or ".psd" or ".bmp" or ".svg" => "🖼️",
                                ".mp4" or ".mkv" or ".mov" or ".webm" or ".avi" or ".wmv" => "🎬",
                                ".mp3" or ".wav" or ".flac" or ".ogg" or ".m4a" => "🎵",
                                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "📦",
                                ".pdf" => "📕",
                                ".txt" or ".doc" or ".docx" or ".log" or ".md" or ".json" or ".xml" => "📄",
                                _ => "📄"
                            };

                            data.Entries.Add(new ZipEntryItem
                            {
                                Name = string.IsNullOrEmpty(entry.Name) ? entry.FullName : entry.Name,
                                RelativePath = entry.FullName,
                                SizeBytes = entry.Length,
                                FormattedSize = isDir ? "-" : MediaItem.FormatBytes(entry.Length),
                                FormattedDate = entry.LastWriteTime.ToString("dd/MM/yyyy HH:mm"),
                                Icon = icon,
                                IsDirectory = isDir
                            });
                            count++;
                        }
                    }

                    data.TotalUncompressedBytes = totalBytes;
                    data.FormattedTotalSize = MediaItem.FormatBytes(totalBytes);
                    data.HasMoreEntries = archive.Entries.Count > maxDisplayEntries;

                    return data;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Erro ao inspecionar ZIP '{zipPath}': {ex.Message}");
                    return null;
                }
            }, ct);
        }
    }
}
