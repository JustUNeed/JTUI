using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace JTUI.Controls.FileGrid
{
    /// <summary>
    /// 用 Win32 SHGetFileInfo 提取文件 / 文件夹 / 快捷方式的系统关联图标。
    /// 提取出的 BitmapSource 已 Freeze,可跨线程传给 UI。
    /// </summary>
    internal static class JTFileIconExtractor
    {
        /// <summary>
        /// 提取系统图标。large=true 取大图标(32×32),否则小图标(16×16)。
        /// 失败返回 null。本方法可在后台线程调用。
        /// </summary>
        public static BitmapSource? Extract(string path, bool large = true)
        {
            try
            {
                uint flags = SHGFI_ICON | (large ? SHGFI_LARGEICON : SHGFI_SMALLICON);

                // 路径可能已不存在(列表里的死链),用 USEFILEATTRIBUTES 退化为按扩展名取通用图标
                if (!System.IO.File.Exists(path) && !System.IO.Directory.Exists(path))
                    flags |= SHGFI_USEFILEATTRIBUTES;

                var shfi = new SHFILEINFO();
                IntPtr res = SHGetFileInfo(path, FILE_ATTRIBUTE_NORMAL, ref shfi,
                    (uint)Marshal.SizeOf(shfi), flags);

                if (res == IntPtr.Zero || shfi.hIcon == IntPtr.Zero)
                    return null;

                try
                {
                    var bmp = Imaging.CreateBitmapSourceFromHIcon(
                        shfi.hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    bmp.Freeze();
                    return bmp;
                }
                finally
                {
                    DestroyIcon(shfi.hIcon);   // 必须释放,否则泄漏 GDI 句柄
                }
            }
            catch
            {
                return null;
            }
        }

        // ---------- Win32 ----------

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;
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
            string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi,
            uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}
