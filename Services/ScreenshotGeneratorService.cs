using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TrashAll.Models;
using TrashAll.ViewModels;

namespace TrashAll.Services
{
    public static class ScreenshotGeneratorService
    {
        public static async Task GenerateScreenshotsAsync()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string repoDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            string assetsDir = Path.Combine(repoDir, "docs", "assets", "images");

            if (!Directory.Exists(assetsDir))
            {
                assetsDir = Path.Combine(Directory.GetCurrentDirectory(), "docs", "assets", "images");
            }
            Directory.CreateDirectory(assetsDir);

            string demoFolder = Path.Combine(Path.GetTempPath(), "TrashAll_Demo_Capture");
            if (Directory.Exists(demoFolder))
            {
                try { Directory.Delete(demoFolder, true); } catch { }
            }
            Directory.CreateDirectory(demoFolder);

            string image1Path = Path.Combine(demoFolder, "IMG_2026_Nature_Sunset_4K.jpg");
            CreateScenicLandscapeImage(image1Path, 1920, 1080);

            string image1DupPath = Path.Combine(demoFolder, "IMG_2026_Nature_Sunset_4K (Cópia).jpg");
            File.Copy(image1Path, image1DupPath, true);

            string image2Path = Path.Combine(demoFolder, "Wallpaper_Forest_UltraHD.png");
            CreateForestGradientImage(image2Path, 1920, 1080);

            string zipPath = Path.Combine(demoFolder, "Projetos_Web_Producao_2026.zip");
            CreateSampleZip(zipPath);

            string exePath = Path.Combine(demoFolder, "Instalador_Driver_Video.exe");
            File.WriteAllBytes(exePath, new byte[1024 * 1024 * 45]);

            string docPath = Path.Combine(demoFolder, "Relatorio_Tecnico_Trimestral.pdf");
            File.WriteAllBytes(docPath, new byte[1024 * 720]);

            var window = new MainWindow
            {
                Width = 1240,
                Height = 820,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            window.Show();
            await Task.Delay(600);

            await window.ViewModel.LoadFolderAsync(demoFolder);
            await Task.Delay(1800);

            window.ViewModel.IsListViewMode = false;
            window.RbVisualizer.IsChecked = true;
            window.RbList.IsChecked = false;
            window.ViewModel.SetCategory("Todos");
            int imgIdx = -1;
            for (int i = 0; i < window.ViewModel.Items.Count; i++)
            {
                if (window.ViewModel.Items[i].Type == MediaType.Image)
                {
                    imgIdx = i;
                    break;
                }
            }
            if (imgIdx >= 0)
            {
                window.ViewModel.CurrentIndex = -1;
                window.ViewModel.CurrentIndex = imgIdx;
            }
            await Task.Delay(800);
            await WaitForRenderAsync();

            string heroOut = Path.Combine(assetsDir, "hero-preview.jpg");
            CaptureWindowToJpeg(window, heroOut);
            Console.WriteLine($"[TrashAll Capture] Hero screenshot salva: {heroOut}");

            window.ViewModel.IsListViewMode = true;
            window.RbVisualizer.IsChecked = false;
            window.RbList.IsChecked = true;
            window.ViewModel.SetCategory("Todos");
            var dupItem = window.ViewModel.Items.FirstOrDefault(x => x.IsDuplicate);
            if (dupItem != null)
            {
                window.ViewModel.CurrentItem = dupItem;
            }
            await Task.Delay(800);
            await WaitForRenderAsync();

            string dupOut = Path.Combine(assetsDir, "duplicate-cleaner.jpg");
            CaptureWindowToJpeg(window, dupOut);
            Console.WriteLine($"[TrashAll Capture] Duplicates / List screenshot salva: {dupOut}");

            window.ViewModel.IsListViewMode = false;
            window.RbVisualizer.IsChecked = true;
            window.RbList.IsChecked = false;
            window.ViewModel.SetCategory("Compactados");
            await Task.Delay(400);

            if (window.ViewModel.Items.Count > 0)
            {
                window.ViewModel.CurrentIndex = -1;
                window.ViewModel.CurrentIndex = 0;
            }
            await Task.Delay(1000);
            await WaitForRenderAsync();

            string zipOut = Path.Combine(assetsDir, "zip-inspector.jpg");
            CaptureWindowToJpeg(window, zipOut);
            Console.WriteLine($"[TrashAll Capture] Zip inspector screenshot salva: {zipOut}");

            window.Close();

            try { Directory.Delete(demoFolder, true); } catch { }
        }

        private static void CaptureWindowToJpeg(Window window, string outputPath)
        {
            int width = (int)window.ActualWidth;
            int height = (int)window.ActualHeight;
            if (width <= 0) width = 1240;
            if (height <= 0) height = 820;

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(window);

            var encoder = new JpegBitmapEncoder
            {
                QualityLevel = 95
            };
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            using var stream = File.Create(outputPath);
            encoder.Save(stream);
        }

        private static async Task WaitForRenderAsync()
        {
            await Application.Current.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
        }

        private static void CreateScenicLandscapeImage(string filePath, int width, int height)
        {
            var wb = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
            int[] pixels = new int[width * height];

            for (int y = 0; y < height; y++)
            {
                float t = (float)y / height;
                byte r = (byte)(255 * Math.Clamp(1.0f - t * 0.7f, 0, 1));
                byte g = (byte)(150 * Math.Clamp(1.0f - t * 0.85f, 0, 1));
                byte b = (byte)(45 + 130 * t);

                double mountainHeight = height * 0.68 + Math.Sin(y * 0.05) * 10;
                for (int x = 0; x < width; x++)
                {
                    double curve = Math.Sin(x * 0.005) * 70 + Math.Sin(x * 0.012) * 35 + Math.Cos(x * 0.02) * 15;
                    double hillLimit = height * 0.65 + curve;

                    byte pr = r;
                    byte pg = g;
                    byte pb = b;

                    double dx = x - width * 0.45;
                    double dy = y - height * 0.52;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist < 110)
                    {
                        double glow = (1.0 - dist / 110.0);
                        pr = (byte)Math.Min(255, pr + glow * 120);
                        pg = (byte)Math.Min(255, pg + glow * 110);
                        pb = (byte)Math.Min(255, pb + glow * 40);
                    }

                    if (y > hillLimit)
                    {
                        pr = (byte)(18 + (y - hillLimit) * 0.05);
                        pg = (byte)(24 + (y - hillLimit) * 0.05);
                        pb = (byte)(40 + (y - hillLimit) * 0.08);
                    }

                    pixels[y * width + x] = (pr << 16) | (pg << 8) | pb;
                }
            }

            wb.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);

            var encoder = new JpegBitmapEncoder { QualityLevel = 96 };
            encoder.Frames.Add(BitmapFrame.Create(wb));
            using var fs = File.Create(filePath);
            encoder.Save(fs);
        }

        private static void CreateForestGradientImage(string filePath, int width, int height)
        {
            var wb = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
            int[] pixels = new int[width * height];

            for (int y = 0; y < height; y++)
            {
                float t = (float)y / height;
                byte r = (byte)(15 + 25 * t);
                byte g = (byte)(130 + 80 * (1.0f - t));
                byte b = (byte)(70 + 40 * t);

                int color = (r << 16) | (g << 8) | b;
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = color;
                }
            }

            wb.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(wb));
            using var fs = File.Create(filePath);
            encoder.Save(fs);
        }

        private static void CreateSampleZip(string zipPath)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            File.WriteAllText(Path.Combine(tempDir, "index.html"), "<!DOCTYPE html><html><body><h1>TrashAll Web</h1></body></html>");
            File.WriteAllText(Path.Combine(tempDir, "style.css"), "body { margin: 0; background: #0b0d13; color: #fff; }");
            File.WriteAllText(Path.Combine(tempDir, "app.bundle.js"), "// Production bundle - 450 KB\nconsole.log('App ready');");
            File.WriteAllText(Path.Combine(tempDir, "relatorio_mensal_2026.csv"), "ID,Item,Tamanho,Status\n1,Foto_01,4.2MB,OK\n2,Foto_02,8.9MB,OK");
            File.WriteAllText(Path.Combine(tempDir, "dados_financeiros.json"), "{\"empresa\": \"Dev\", \"ano\": 2026}");

            string assetsSub = Path.Combine(tempDir, "assets");
            Directory.CreateDirectory(assetsSub);
            File.WriteAllBytes(Path.Combine(assetsSub, "logo_vector.svg"), new byte[1024 * 12]);
            File.WriteAllBytes(Path.Combine(assetsSub, "banner_4k.png"), new byte[1024 * 512]);

            ZipFile.CreateFromDirectory(tempDir, zipPath);
            Directory.Delete(tempDir, true);
        }
    }
}
