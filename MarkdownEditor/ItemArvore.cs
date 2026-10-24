using Microsoft.UI.Xaml.Media;
using System.ComponentModel;

namespace MarkdownEditor
{
    // Conteúdo de cada TreeViewNode da árvore. O ícone chega depois (carregado do shell de forma assíncrona).
    public sealed partial class ItemArvore(string nome, string caminho, bool ehArquivo) : INotifyPropertyChanged
    {
        private string _nome = nome;
        private ImageSource? _icone;

        public event PropertyChangedEventHandler? PropertyChanged;

        // Muda na troca de idioma (raízes "Área de Trabalho", "Documentos" e "Downloads").
        public string Nome
        {
            get => _nome;
            set
            {
                if (_nome == value)
                    return;

                _nome = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Nome)));
            }
        }

        public string Caminho { get; } = caminho;

        public bool EhArquivo { get; } = ehArquivo;

        // Chave do texto traduzido do nome (só nas pastas especiais da raiz); null = nome do disco.
        public string? ChaveNome { get; init; }

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
