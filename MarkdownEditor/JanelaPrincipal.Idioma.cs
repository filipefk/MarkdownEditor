using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MarkdownEditor
{
    public sealed partial class JanelaPrincipal
    {
        // Injetado no visualizador (sem alterar o HTML): troca os textos fixos da página.
        // A quebra de linha ("\n") do texto da área de soltar vira <br>.
        private const string ScriptIdiomaPrevia = """
            window.aplicarIdioma = (t) => {
                document.documentElement.lang = t.lang;
                document.title = t.titulo;
                const definir = (seletor, texto) => {
                    const elemento = document.querySelector(seletor);
                    if (elemento) elemento.textContent = texto;
                };
                definir('.topbar .tag', t.tag);
                const tema = document.getElementById('themeBtn');
                if (tema) {
                    tema.textContent = t.tema;
                    tema.title = t.temaDica;
                }
                definir('#printBtn', t.imprimir);
                const copiar = document.getElementById('copyBtn');
                const iconeCopiar = document.getElementById('copyIcon');
                if (copiar) copiar.replaceChildren(...(iconeCopiar ? [iconeCopiar, ' '] : []), t.copiar);
                definir('#newBtn', t.novo);
                definir('#dropzone h1', t.solteTitulo);
                const instrucao = document.querySelector('#dropzone p');
                if (instrucao) {
                    instrucao.replaceChildren();
                    t.solteTexto.split('\n').forEach((linha, i) => {
                        if (i > 0) instrucao.append(document.createElement('br'));
                        instrucao.append(linha);
                    });
                }
                definir('#browseBtn', t.escolher);
                definir('#dropzone .or-paste', t.colar);
                definir('#paperWrap .footnote', t.rodape);
                definir('.drag-overlay-box', t.solteAqui);
            };
            """;

        // Escolha salva (null = idioma do Windows); define o item marcado no menu Idioma.
        private string? _idiomaEscolhido;

        private void rmiIdioma_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioMenuFlyoutItem { Tag: string codigo })
                return;

            _idiomaEscolhido = string.IsNullOrEmpty(codigo) ? null : codigo;
            var configuracoes = Configuracoes.Carregar();
            configuracoes.Idioma = _idiomaEscolhido;
            configuracoes.Salvar();

            Textos.DefinirIdioma(_idiomaEscolhido ?? Textos.IdiomaDoWindows());
            App.DefinirIdiomaDoWinUI(Textos.IdiomaAtual);
            AplicarIdioma();
        }

        // Chamado no construtor e a cada troca: os textos que só existem ao abrir (diálogos, menus de
        // contexto, "Sobre", "Salvar como") já são lidos de Textos na hora e não precisam de nada aqui.
        private void AplicarIdioma()
        {
            abbVoltar.Label = Textos.Obter("Voltar");
            abbAvancar.Label = Textos.Obter("Avancar");

            sgiVisualizar.Content = Textos.Obter("Visualizar");
            ToolTipService.SetToolTip(sgiVisualizar, Textos.Obter("VisualizarDica"));
            sgiEditar.Content = Textos.Obter("Editar");
            ToolTipService.SetToolTip(sgiEditar, Textos.Obter("EditarDica"));

            abbSalvar.Label = Textos.Obter("Salvar");
            abbDesfazer.Label = Textos.Obter("Desfazer");
            ToolTipService.SetToolTip(abbDesfazer, Textos.Obter("DesfazerDica"));

            abbIdioma.Label = Textos.Obter("Idioma");
            ToolTipService.SetToolTip(abbIdioma, Textos.Obter("IdiomaDica"));
            rmiIdiomaWindows.Text = Textos.Obter("IdiomaWindows");
            MarcarIdiomaEscolhido();

            abbSobre.Label = Textos.Obter("Sobre");
            ToolTipService.SetToolTip(abbSobre, Textos.Obter("SobreDica"));

            AtualizarNomesRaizes();
            AtualizarBotoesNavegacao();
            AtualizarBotaoSalvar();

            Disparar(AplicarIdiomaNasPaginasAsync());
        }

        private void MarcarIdiomaEscolhido()
        {
            rmiIdiomaWindows.IsChecked = _idiomaEscolhido == null;
            rmiPortugues.IsChecked = _idiomaEscolhido == Textos.Portugues;
            rmiIngles.IsChecked = _idiomaEscolhido == Textos.Ingles;
            rmiEspanhol.IsChecked = _idiomaEscolhido == Textos.Espanhol;
        }

        private void AtualizarNomesRaizes()
        {
            foreach (var no in trvPastas.RootNodes)
            {
                if (no.Content is ItemArvore { ChaveNome: { } chave } item)
                    item.Nome = Textos.Obter(chave);
            }
        }

        private async Task AplicarIdiomaNasPaginasAsync()
        {
            await AplicarIdiomaNaPreviaAsync();
            await ExecutarNoEditorAsync($"document.documentElement.lang = {Js(Textos.IdiomaAtual)};");
        }

        // Também chamado no NavigationCompleted do visualizador.
        private async Task AplicarIdiomaNaPreviaAsync()
        {
            if (!_previewPronto)
                return;

            var textos = new Dictionary<string, string>
            {
                ["lang"] = Textos.IdiomaAtual,
                ["titulo"] = Textos.Obter("PreviaTitulo"),
                ["tag"] = Textos.Obter("PreviaTag"),
                ["tema"] = Textos.Obter("PreviaTema"),
                ["temaDica"] = Textos.Obter("PreviaTemaDica"),
                ["imprimir"] = Textos.Obter("PreviaImprimir"),
                ["copiar"] = Textos.Obter("PreviaCopiar"),
                ["novo"] = Textos.Obter("PreviaNovo"),
                ["solteTitulo"] = Textos.Obter("PreviaSolteTitulo"),
                ["solteTexto"] = Textos.Obter("PreviaSolteTexto"),
                ["escolher"] = Textos.Obter("PreviaEscolher"),
                ["colar"] = Textos.Obter("PreviaColar"),
                ["rodape"] = Textos.Obter("PreviaRodape"),
                ["solteAqui"] = Textos.Obter("PreviaSolteAqui")
            };
            await wvwPreview.ExecuteScriptAsync($"window.aplicarIdioma && aplicarIdioma({Js(textos)});");
        }
    }
}
