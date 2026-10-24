using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using static MarkdownEditor.NativeMethods;

namespace MarkdownEditor
{
    // Ícones do Explorer (SHGetFileInfo, os mesmos do shell) para pastas, unidades e tipos de arquivo,
    // convertidos em WriteableBitmap no tamanho físico da tela. Síncrono e com cache por chave, para o
    // ícone já estar no item quando o nó é criado (o x:Bind não precisa acompanhar mudanças).
    internal static class IconesShell
    {
        private const int TamanhoLogico = 16;

        private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

        // Escala da tela (RasterizationScale), definida pela janela antes de montar a árvore.
        public static double Escala { get; set; } = 1.0;

        // Ícone do próprio item (pastas especiais e unidades têm ícone próprio).
        public static ImageSource? DoCaminho(string chave, string caminho) =>
            Obter(chave, caminho, 0, 0);

        // Ícone genérico do tipo, sem acessar o disco (pasta comum ou extensão de arquivo).
        public static ImageSource? DoTipo(string chave, string nomeOuExtensao, bool ehPasta) =>
            Obter(chave, nomeOuExtensao, SHGFI_USEFILEATTRIBUTES, ehPasta ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL);

        private static ImageSource? Obter(string chave, string caminho, uint flagsExtras, uint atributos)
        {
            if (!Cache.TryGetValue(chave, out var imagem))
            {
                imagem = Carregar(caminho, flagsExtras, atributos);
                Cache[chave] = imagem;
            }

            return imagem;
        }

        private static WriteableBitmap? Carregar(string caminho, uint flagsExtras, uint atributos)
        {
            var tamanho = Math.Max(TamanhoLogico, (int)Math.Round(TamanhoLogico * Escala));
            var tamanhoShell = tamanho > TamanhoLogico ? SHGFI_LARGEICON : SHGFI_SMALLICON;

            var info = new SHFILEINFO();
            SHGetFileInfo(caminho, atributos, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | tamanhoShell | flagsExtras);
            if (info.hIcon == IntPtr.Zero)
                return null;

            try
            {
                var pixels = DesenharIcone(info.hIcon, tamanho);
                if (pixels == null)
                    return null;

                var bitmap = new WriteableBitmap(tamanho, tamanho);
                using (var fluxo = bitmap.PixelBuffer.AsStream())
                    fluxo.Write(pixels, 0, pixels.Length);
                bitmap.Invalidate();
                return bitmap;
            }
            finally
            {
                DestroyIcon(info.hIcon);
            }
        }

        // Desenha o ícone numa seção DIB de 32 bits (BGRA pré-multiplicado, o formato do WriteableBitmap).
        private static byte[]? DesenharIcone(IntPtr icone, int tamanho)
        {
            var cabecalho = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = tamanho,
                biHeight = -tamanho, // de cima para baixo, como o WriteableBitmap
                biPlanes = 1,
                biBitCount = 32,
                biCompression = BI_RGB
            };

            var dc = CreateCompatibleDC(IntPtr.Zero);
            if (dc == IntPtr.Zero)
                return null;

            var dib = CreateDIBSection(dc, ref cabecalho, DIB_RGB_COLORS, out var bits, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero || bits == IntPtr.Zero)
            {
                DeleteDC(dc);
                return null;
            }

            var anterior = SelectObject(dc, dib);
            try
            {
                if (!DrawIconEx(dc, 0, 0, icone, tamanho, tamanho, 0, IntPtr.Zero, DI_NORMAL))
                    return null;

                var pixels = new byte[tamanho * tamanho * 4];
                Marshal.Copy(bits, pixels, 0, pixels.Length);
                CorrigirAlfa(pixels);
                return pixels;
            }
            finally
            {
                SelectObject(dc, anterior);
                DeleteObject(dib);
                DeleteDC(dc);
            }
        }

        // Ícones antigos (sem canal alfa) saem com alfa zero: tornam-se opacos onde há cor.
        private static void CorrigirAlfa(byte[] pixels)
        {
            for (var i = 3; i < pixels.Length; i += 4)
                if (pixels[i] != 0)
                    return;

            for (var i = 0; i < pixels.Length; i += 4)
                if (pixels[i] != 0 || pixels[i + 1] != 0 || pixels[i + 2] != 0)
                    pixels[i + 3] = 255;
        }
    }
}
