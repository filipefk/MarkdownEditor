# Visualizador e editor de Markdown

Aplicação Windows Forms (.NET 10) para navegar pelas pastas do computador e visualizar arquivos Markdown, JSON, XML e TXT com formatação legível.

> **Estado atual:** somente **visualização**. As rotinas de **edição** do Markdown serão implementadas em uma etapa futura.

## Funcionalidades

- **Árvore de pastas** à esquerda, começando por Área de Trabalho, Documentos, Downloads e as unidades do computador. As pastas são carregadas sob demanda ao expandir e só são exibidos os arquivos `.md`, `.markdown`, `.json`, `.xml` e `.txt` (pastas ocultas e de sistema ficam de fora).
- **Visualizador** à direita: ao selecionar um arquivo na árvore ele é renderizado no painel HTML.
  - Markdown com suporte a GFM e front matter YAML (exibido como tabela).
  - JSON e XML reformatados com realce de sintaxe.
- Recursos do próprio visualizador, que continuam disponíveis:
  - Arrastar e soltar um arquivo do Explorer no painel.
  - Colar texto (Ctrl+V) para visualizar sem precisar de arquivo.
  - Botão "Escolher arquivo" / "Abrir outro arquivo".
  - Tema claro/escuro (a escolha fica salva).
  - Copiar o conteúdo e Exportar/Imprimir (inclusive para PDF).
- **Abrir arquivo por parâmetro**: o programa aceita o caminho de um arquivo na linha de comando, do mesmo jeito que o Windows faz no duplo clique, exibe o arquivo e o seleciona na árvore:

  ```bash
  MarkdownEditor.exe "C:\pasta\arquivo.md"
  ```

## Abrir arquivos .md com duplo clique

O programa não altera o Registro do Windows. Para usá-lo no duplo clique, faça a associação manualmente uma vez:

1. Clique com o botão direito em um arquivo `.md` > **Abrir com** > **Escolher outro aplicativo**.
2. Clique em **Procurar outro aplicativo neste PC** (ou "Escolher um aplicativo no PC") e selecione o `MarkdownEditor.exe`.
3. Marque **Sempre** para que os próximos duplo cliques abram direto no programa.

## Requisitos

- Windows 10/11.
- .NET 10 Desktop Runtime.
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (já incluso no Windows 11).

## Como executar

Abra `MarkdownEditor.slnx` no Visual Studio e execute o projeto `MarkdownEditor`, ou pela linha de comando:

```bash
dotnet run --project MarkdownEditor
```

## Estrutura

```
MarkdownEditor.slnx
Markdown.ico                       Ícone do programa
MarkdownEditor/
  Program.cs                       Ponto de entrada; recebe o arquivo passado como argumento
  frmEditor.cs / .Designer.cs      Tela principal: árvore de pastas + visualizador WebView2
  wwwroot/
    preview-md-json-xml.html       Visualizador HTML (cópia do projeto preview-html)
```

## Como funciona o visualizador

O painel da direita é um controle **WebView2** que carrega `wwwroot/preview-md-json-xml.html`, uma cópia do projeto `preview-html` (página única, 100% offline, com `marked.js` embutido). A página é servida pelo endereço virtual `https://preview.local/`, o que habilita a área de transferência e mantém o tema salvo entre execuções.

Quando um arquivo é selecionado na árvore, o programa lê o conteúdo e chama a função `renderContent(texto, nome)` da própria página, sem nenhuma alteração no HTML.

## Próximos passos

- Edição de arquivos Markdown.
