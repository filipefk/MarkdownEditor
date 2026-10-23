using System.Runtime.InteropServices;

namespace MarkdownEditor
{
    internal static partial class NativeMethods
    {
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        public const int DWMWCP_ROUNDSMALL = 3;

        public const uint SHGFI_ICON = 0x100;
        public const uint SHGFI_SMALLICON = 0x1;
        public const uint SHGFI_LARGEICON = 0x0;

        public const int TVM_SETEXTENDEDSTYLE = 0x1100 + 44;
        public const int TVS_EX_DOUBLEBUFFER = 0x0004;
        public const int TVS_EX_FADEINOUTEXPANDOS = 0x0040;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [LibraryImport("dwmapi.dll")]
        public static partial int DwmSetWindowAttribute(IntPtr hwnd, int atributo, ref int valor, int tamanho);

        [LibraryImport("uxtheme.dll", StringMarshalling = StringMarshalling.Utf16)]
        public static partial int SetWindowTheme(IntPtr hwnd, string? nomeSubApp, string? listaSubId);

        [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
        public static partial IntPtr SendMessage(IntPtr hwnd, int mensagem, IntPtr wParam, IntPtr lParam);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool DestroyIcon(IntPtr hIcon);

        // A struct com strings de tamanho fixo (ByValTStr) não é suportada pelo LibraryImport.
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);
    }
}
