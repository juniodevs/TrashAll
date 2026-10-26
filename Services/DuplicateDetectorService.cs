using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using TrashAll.Models;

namespace TrashAll.Services
{
    public class DuplicateDetectionResult
    {
        public int TotalDuplicateFiles { get; set; }
        public int TotalDuplicateCopies { get; set; }
        public long TotalWastedBytes { get; set; }
        public string FormattedWastedBytes => MediaItem.FormatBytes(TotalWastedBytes);
    }

    public class DuplicateDetectorService
    {
        public async Task<DuplicateDetectionResult> DetectDuplicatesAsync(
            IReadOnlyList<MediaItem> items,
            CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                var result = new DuplicateDetectionResult();
                if (items == null || items.Count == 0) return result;

                var sizeGroups = items
                    .Where(x => x.FileSizeBytes > 0)
                    .GroupBy(x => x.FileSizeBytes)
                    .Where(g => g.Count() > 1)
                    .ToList();

                if (sizeGroups.Count == 0)
                {
                    foreach (var item in items)
                    {
                        item.IsDuplicate = false;
                        item.DuplicateGroupId = null;
                        item.IsDuplicateCopy = false;
                    }
                    return result;
                }

                foreach (var item in items)
                {
                    item.IsDuplicate = false;
                    item.DuplicateGroupId = null;
                    item.IsDuplicateCopy = false;
                }

                var hashGroups = new Dictionary<string, List<MediaItem>>(StringComparer.Ordinal);

                foreach (var group in sizeGroups)
                {
                    if (ct.IsCancellationRequested) return result;

                    foreach (var item in group)
                    {
                        if (ct.IsCancellationRequested) return result;

                        string? hash = ComputeFastHash(item.FilePath, item.FileSizeBytes);
                        if (!string.IsNullOrEmpty(hash))
                        {
                            string key = $"{item.FileSizeBytes}_{hash}";
                            if (!hashGroups.TryGetValue(key, out var list))
                            {
                                list = new List<MediaItem>();
                                hashGroups[key] = list;
                            }
                            list.Add(item);
                        }
                    }
                }

                int duplicateFilesCount = 0;
                int duplicateCopiesCount = 0;
                long wastedBytes = 0;

                foreach (var kvp in hashGroups)
                {
                    var groupItems = kvp.Value;
                    if (groupItems.Count > 1)
                    {
                        var sorted = groupItems.OrderBy(x => x.DateModified).ToList();

                        for (int i = 0; i < sorted.Count; i++)
                        {
                            var it = sorted[i];
                            it.IsDuplicate = true;
                            it.DuplicateGroupId = kvp.Key;
                            it.IsDuplicateCopy = (i > 0);
                            duplicateFilesCount++;

                            if (i > 0)
                            {
                                duplicateCopiesCount++;
                                wastedBytes += it.FileSizeBytes;
                            }
                        }
                    }
                }

                result.TotalDuplicateFiles = duplicateFilesCount;
                result.TotalDuplicateCopies = duplicateCopiesCount;
                result.TotalWastedBytes = wastedBytes;

                return result;
            }, ct);
        }

        private static string? ComputeFastHash(string filePath, long fileSize)
        {
            try
            {
                if (!File.Exists(filePath)) return null;

                using var sha256 = SHA256.Create();
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                const int chunkSize = 64 * 1024;

                if (fileSize <= chunkSize * 2)
                {
                    byte[] hash = sha256.ComputeHash(fs);
                    return Convert.ToHexString(hash);
                }

                byte[] buffer = new byte[chunkSize * 2];
                int readFirst = fs.Read(buffer, 0, chunkSize);

                fs.Seek(Math.Max(0, fileSize - chunkSize), SeekOrigin.Begin);
                int readLast = fs.Read(buffer, chunkSize, chunkSize);

                byte[] partialHash = sha256.ComputeHash(buffer, 0, readFirst + readLast);
                return Convert.ToHexString(partialHash);
            }
            catch
            {
                return null;
            }
        }
    }
}
