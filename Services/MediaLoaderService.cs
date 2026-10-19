using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Docnet.Core;
using Docnet.Core.Models;
using ImageMagick;
using TrashAll.Models;

namespace TrashAll.Services
{
    public class MediaLoaderService
    {
        private readonly ConcurrentDictionary<string, BitmapSource> _cache = new();
        private readonly ConcurrentQueue<string> _cacheOrder = new();
        private const int MaxCacheSize = 14;

        public async Task<BitmapSource?> LoadBitmapAsync(MediaItem item, CancellationToken ct)
        {
            bool isPdf = string.Equals(item.Extension, ".PDF", StringComparison.OrdinalIgnoreCase);

            if (item.Type != MediaType.Image && !isPdf)
                return null;

            if (_cache.TryGetValue(item.FilePath, out var cached))
                return cached;

            return await Task.Run(() =>
            {
                try
                {
                    if (ct.IsCancellationRequested) return null;

                    BitmapSource? bmp = null;

                    if (isPdf)
                    {
                        bmp = LoadPdf(item.FilePath);
                    }
                    else if (string.Equals(item.Extension, ".PSD", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(item.Extension, ".PSB", StringComparison.OrdinalIgnoreCase))
                    {
                        bmp = LoadPsd(item.FilePath);
                    }
                    else
                    {
                        bmp = LoadStandardImage(item.FilePath);
                    }

                    if (bmp != null)
                    {
                        AddToCache(item.FilePath, bmp);
                    }

                    return bmp;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Falha no carregador: {ex.Message}");
                    return null;
                }
            }, ct);
        }

        private BitmapSource? LoadPdf(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return null;

                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (fs.Length == 0) return null;

                byte[] bytes = new byte[fs.Length];
                int readTotal = 0;
                while (readTotal < bytes.Length)
                {
                    int r = fs.Read(bytes, readTotal, bytes.Length - readTotal);
                    if (r <= 0) break;
                    readTotal += r;
                }

                using var docReader = DocLib.Instance.GetDocReader(bytes, new PageDimensions(1400, 1800));
                if (docReader.GetPageCount() == 0) return null;

                using var pageReader = docReader.GetPageReader(0);
                var rawBytes = pageReader.GetImage();
                int width = pageReader.GetPageWidth();
                int height = pageReader.GetPageHeight();

                var bmp = BitmapSource.Create(
                    width, height,
                    96, 96,
                    System.Windows.Media.PixelFormats.Bgra32,
                    null,
                    rawBytes,
                    width * 4);
                bmp.Freeze();

                return bmp;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro Docnet PDF para {filePath}: {ex.Message}");
                return null;
            }
        }

        private BitmapSource? LoadStandardImage(string filePath)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(filePath);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bmp.EndInit();
                bmp.Freeze();

                return bmp;
            }
            catch
            {
                return LoadWithMagick(filePath);
            }
        }

        private BitmapSource? LoadPsd(string filePath)
        {
            return LoadWithMagick(filePath);
        }

        private BitmapSource? LoadWithMagick(string filePath)
        {
            try
            {
                using var image = new MagickImage(filePath);
                byte[] bytes = image.ToByteArray(MagickFormat.Bmp);
                var ms = new MemoryStream(bytes);

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                ms.Dispose();

                return bmp;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro no MagickImage para {filePath}: {ex.Message}");
                return null;
            }
        }

        private void AddToCache(string path, BitmapSource bmp)
        {
            if (_cache.ContainsKey(path)) return;

            _cache[path] = bmp;
            _cacheOrder.Enqueue(path);

            while (_cacheOrder.Count > MaxCacheSize && _cacheOrder.TryDequeue(out var oldKey))
            {
                _cache.TryRemove(oldKey, out _);
            }
        }

        public void InvalidateCache(string path)
        {
            _cache.TryRemove(path, out _);
        }

        public void ClearCache()
        {
            _cache.Clear();
            while (_cacheOrder.TryDequeue(out _)) { }
        }
    }
}
