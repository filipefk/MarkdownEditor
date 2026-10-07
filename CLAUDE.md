# CLAUDE.md — MarkdownEditor

## O que é

Aplicação Windows Forms (`net10.0-windows`) para visualizar arquivos `.md`, `.markdown`, `.json`, `.xml` e `.txt`. Nesta etapa é **somente visualização**; as rotinas de **edição** do Markdown virão depois (ainda não existe nenhum código de edição).

Solução: `MarkdownEditor.slnx` com um único projeto, `MarkdownEditor/MarkdownEditor.csproj`.

## Arquitetura

- **`Program.cs`**: `Main(string[] args)` pega o primeiro argumento que seja um arquivo existente e passa para `new frmEditor(arquivoInicial)`. É assim que funciona o duplo clique no Windows (o Windows passa o caminho como `%1`). O programa **não** grava associação de arquivos no Registro — isso foi decidido explicitamente; a associação é feita manualmente pelo usuário via "Abrir com".
- **`frmEditor`**: `SplitContainer spcPrincipal` com:
  - `Panel1`: `TreeView trvPastas` com `ImageList imlIcones`.
  - `Panel2`: `WebView2 wvwPreview` (pacote NuGet `Microsoft.Web.WebView2`).
- **Árvore** (`frmEditor.cs`):
  - Raízes: Área de Trabalho, Documentos, Downloads e `DriveInfo.GetDrives()` (somente `IsReady`).
  - `Tag` de cada nó é um `record NoArvore(string Caminho, bool EhArquivo)`.
  - Carregamento lazy: nó de pasta nasce com um filho fictício (`"..."`, sem `Tag`), substituído no `BeforeExpand` por subpastas + arquivos filtrados por `ExtensoesSuportadas`. Pastas ocultas/sistema são ignoradas; `UnauthorizedAccessException`/`IOException` resultam em nó vazio.
  - Ícones: `SystemIcons.GetStockIcon` para pastas/unidades e `Icon.ExtractAssociatedIcon` com cache por extensão para arquivos.
  - `SelecionarNaArvore(caminho)` expande a árvore até o arquivo recebido por parâmetro.
- **Visualizador**:
  - `wwwroot/preview-md-json-xml.html` é uma **cópia** do projeto `D:\Fontes\preview-html` (não é link). Copiado para a saída via `<Content Include="wwwroot\**" CopyToOutputDirectory="PreserveNewest" />`.
  - Servido por `SetVirtualHostNameToFolderMapping("preview.local", ...)` em `https://preview.local/preview-md-json-xml.html` (contexto seguro para `navigator.clipboard` e `localStorage` persistente).
  - Dados do WebView2 em `%LocalAppData%\MarkdownEditor\WebView2`.
  - Para exibir um arquivo, o C# chama a função global do HTML: `renderContent(texto, nome)` via `ExecuteScriptAsync`, com os argumentos serializados por `JsonSerializer.Serialize`.
  - Enquanto a página não terminou de carregar (`NavigationCompleted`), o arquivo fica em `_arquivoPendente` e é exibido em seguida.
  - Arrastar/soltar, colar (Ctrl+V), tema, copiar e imprimir são recursos nativos do HTML e funcionam sem código C#.

## Convenções

- Textos de interface, nomes de variáveis, métodos e controles em português (pt-BR).
- Prefixos de controles: `frm` (form), `trv` (TreeView), `spc` (SplitContainer), `iml` (ImageList), `wvw` (WebView2).
- Não adicionar comentários XML `<summary>` em métodos novos.
- Não alterar o HTML em `wwwroot` para integrar com o C#; preferir chamar as funções já existentes. Se o HTML precisar mudar, avaliar se a mudança deve ir também para o projeto `preview-html` original.
- Não há testes automatizados; a verificação é feita executando o programa.
