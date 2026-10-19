using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace TrashAll.Helpers
{
    public static class IconHelper
    {
        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_SMALLICON = 0x000000001;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            ref SHFILEINFO psfi,
            uint cbFileInfo,
            uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private static readonly ConcurrentDictionary<string, BitmapSource> _extCache = new(StringComparer.OrdinalIgnoreCase);

        public static BitmapSource? GetIcon(string filePath)
        {
            string ext = Path.GetExtension(filePath);

            // Cacheia ícones comuns por extensão (exceto executáveis que têm ícones personalizados)
            if (!string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase) &&
                _extCache.TryGetValue(ext, out var cached))
            {
                return cached;
            }

            try
            {
                var shinfo = new SHFILEINFO();
                IntPtr res = SHGetFileInfo(
                    filePath,
                    FILE_ATTRIBUTE_NORMAL,
                    ref shinfo,
                    (uint)Marshal.SizeOf(shinfo),
                    SHGFI_ICON | SHGFI_SMALLICON);

                if (res != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
                {
                    try
                    {
                        var bs = Imaging.CreateBitmapSourceFromHIcon(
                            shinfo.hIcon,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        bs.Freeze();

                        if (!string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            _extCache[ext] = bs;
                        }

                        return bs;
                    }
                    finally
                    {
                        DestroyIcon(shinfo.hIcon);
                    }
                }
            }
            catch
            {
                // Fallback silencioso se o shell falhar
            }

            return null;
        }
    }
}
