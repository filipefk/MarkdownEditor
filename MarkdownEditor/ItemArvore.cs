using Microsoft.UI.Xaml.Media;
using System.ComponentModel;

namespace MarkdownEditor
{
    // Conteúdo de cada TreeViewNode da árvore. O ícone chega depois (carregado do shell de forma assíncrona).
    public sealed partial class ItemArvore(string nome, string caminho, bool ehArquivo) : INotifyPropertyChanged
    {
        private ImageSource? _icone;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Nome { get; } = nome;

        public string Caminho { get; } = caminho;

        public bool EhArquivo { get; } = ehArquivo;

        public (string Caminho, bool EhArquivo) Chave => (Caminho, EhArquivo);

        public ImageSource? Icone
        {
            get => _icone;
            set
            {
                if (_icone == value)
                    return;

                _icone = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icone)));
            }
        }
    }
}
