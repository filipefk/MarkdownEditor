# Visualizador e editor de Markdown

Aplicação **WinUI 3** (Windows App SDK, .NET 10), com o visual Fluent Design do Windows 11, para navegar pelas pastas do computador, visualizar arquivos Markdown, JSON, XML e TXT com formatação legível e editá-los.

## Funcionalidades

- **Árvore de pastas** à esquerda, começando por Área de Trabalho, Documentos, Downloads e as unidades do computador. As pastas são carregadas sob demanda ao expandir e só são exibidos os arquivos `.md`, `.markdown`, `.json`, `.xml` e `.txt` (pastas ocultas e de sistema ficam de fora).
  - Menu de contexto (botão direito) com **Abrir local do arquivo** / **Abrir pasta**, **Copiar caminho completo**, **Copiar nome** e, nas pastas, **Atualizar** (relê o conteúdo mantendo o que já estava expandido). As mesmas opções (exceto Atualizar) aparecem no menu de contexto do visualizador.
  - Botão de painel na barra de título esconde/mostra a árvore, guardando a largura (ajustável arrastando a divisória).
- **Visualizador** à direita: ao selecionar um arquivo na árvore ele é renderizado no painel HTML.
  - Markdown com suporte a GFM e front matter YAML (exibido como tabela).
  - JSON e XML reformatados com realce de sintaxe.
- Recursos do próprio visualizador, que continuam disponíveis:
  - Arrastar e soltar um arquivo do Explorer no painel (na prévia ou no editor); ele é aberto e selecionado na árvore.
  - Colar texto (Ctrl+V) para visualizar sem precisar de arquivo.
  - Botão "Escolher arquivo" / "Abrir outro arquivo".
  - Tema claro/escuro (a escolha fica salva).
  - Copiar o conteúdo e Exportar/Imprimir (inclusive para PDF).
- **Edição**: o seletor **Visualizar** / **Editar** da barra de comandos alterna entre a prévia e o editor de código **Monaco** (o mesmo do VS Code) com realce de sintaxe para Markdown, JSON e XML, números de linha, busca (Ctrl+F) e as cores do tema claro/escuro.
  - **Salvar** (ou **Ctrl+S**) grava o arquivo na mesma codificação em que foi lido, primeiro num temporário e só depois trocando pelo original (uma falha no meio não corrompe o arquivo). Texto colado ou novo abre o **Salvar como**, e o arquivo salvo passa a aparecer selecionado na árvore.
  - **Desfazer alterações** volta ao conteúdo original do arquivo (Ctrl+Z ainda recupera as edições).
  - O título da janela ganha `*` quando há alterações não salvas, e ao voltar para **Visualizar** a prévia mostra o texto editado.
  - Antes de trocar de arquivo, navegar no histórico, abrir um link, colar/soltar outro conteúdo ou fechar o programa, pergunta se deseja salvar as alterações pendentes.
- **Atualização automática**: se o arquivo exibido for alterado por outro programa, ele é recarregado sozinho (mantendo a rolagem); se for excluído, sai da árvore e a visualização é fechada. Havendo alterações não salvas no editor, o programa pergunta antes de descartá-las.
- **Fluent Design nativo** (WinUI 3): fundo Mica, barra de título própria (`TitleBar`), barra de comandos (`CommandBar`), árvore (`TreeView`) com os ícones do Explorer, menus e diálogos do sistema (`ContentDialog`) e o seletor de arquivos do Windows no **Salvar como**.
- **Tema da janela** acompanha o tema claro/escuro do visualizador (Mica, barra de título, árvore, barra de comandos, menus, diálogos e editor).
- **Voltar e Avançar** entre os últimos arquivos (e textos colados) exibidos, como num navegador — guarda até 10 itens em cada direção:
  - Botões **Voltar** e **Avançar** na barra de comandos (o tooltip mostra o nome do destino).
  - Atalhos **Alt+←** / **Alt+→** e os botões laterais do mouse.
- **Abrir arquivo por parâmetro**: o programa aceita o caminho de um arquivo na linha de comando, do mesmo jeito que o Windows faz no duplo clique, exibe o arquivo e o seleciona na árvore:

  ```bash
  MarkdownEditor.exe "C:\pasta\arquivo.md"
  ```

## Abrir arquivos .md com duplo clique

Ao usar o [instalador](#instalador), o programa já aparece no **Abrir com** dos arquivos `.md`, `.markdown`, `.json`, `.xml` e `.txt`, e a opção **"Abrir arquivos .md e .markdown com o Markdown Editor por padrão"** grava a associação no Registro.

**É normal o Windows perguntar qual aplicativo usar no primeiro duplo clique** depois da instalação, mesmo com essa opção marcada. No Windows 10/11 nenhum programa pode se tornar o padrão sozinho: a escolha final é sempre do usuário. Basta selecionar o **Markdown Editor** e marcar **Sempre**; a partir daí o duplo clique abre direto no programa.

O programa em si não altera o Registro do Windows. Sem o instalador, faça a associação manualmente uma vez:

1. Clique com o botão direito em um arquivo `.md` > **Abrir com** > **Escolher outro aplicativo**.
2. Clique em **Procurar outro aplicativo neste PC** (ou "Escolher um aplicativo no PC") e selecione o `MarkdownEditor.exe`.
3. Marque **Sempre** para que os próximos duplo cliques abram direto no programa.

## Requisitos

- Windows 10 versão 1809 ou posterior / Windows 11 (x64).
- .NET 10 Runtime.
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (já incluso no Windows 11).
- O Windows App SDK vai junto do programa (self-contained); não é preciso instalar o Windows App Runtime.

## Como executar

Abra `MarkdownEditor.slnx` no Visual Studio (com a carga de trabalho de desenvolvimento para Windows / WinUI) e execute o projeto `MarkdownEditor`, ou pela linha de comando:

```bash
dotnet run --project MarkdownEditor
```

## Instalador

Instalador para Windows gerado com [Inno Setup](https://jrsoftware.org/isinfo.php). Instala em `C:\Program Files\MarkdownEditor`, cria atalho no menu Iniciar (e, opcionalmente, na área de trabalho), registra o programa no **Abrir com** e oferece a associação padrão de `.md` e `.markdown`. A desinstalação remove tudo isso.

O computador de destino precisa ter o **.NET 10 Runtime** instalado. Se ele (ou o WebView2 Runtime) não for encontrado, o instalador avisa e oferece abrir a página de download.

Para gerar (requer o `iscc` do Inno Setup no PATH):

1. Atualize a versão em `MarkdownEditor\Instalador\Instalador.iss` (`AppVersion` e `OutputBaseFilename`) e em `MarkdownEditor\MarkdownEditor.csproj` (`AssemblyVersion`, `FileVersion` e `InformationalVersion`) e `MarkdownEditor\app.manifest` (`assemblyIdentity version`).
2. Execute `Release.cmd` na raiz: publica o projeto em Release (x64) em `MarkdownEditor\bin\Publicar`, apaga o instalador anterior e gera `MarkdownEditor\Instalador\MarkdownEditor-X.Y.Z.exe`.

## Estrutura

```
MarkdownEditor.slnx
Markdown.ico                       Ícone do programa
Release.cmd                        Compila em Release e gera o instalador
MarkdownEditor/
  App.xaml / .cs                   Aplicação; recebe o arquivo passado como argumento e trata erros não tratados
  app.manifest                     DPI por monitor e versões do Windows suportadas
  JanelaPrincipal.xaml / .cs       Janela principal (layout Fluent, constantes, campos, atalhos); o restante fica nos parciais:
  JanelaPrincipal.Arvore.cs        Árvore de pastas, menu de contexto e esconder/mostrar árvore
  JanelaPrincipal.Preview.cs       Visualizador WebView2, mensagens do HTML e links
  JanelaPrincipal.Editor.cs        Editor Monaco, salvar e confirmação de descarte
  JanelaPrincipal.Monitor.cs       Atualização automática do arquivo exibido
  JanelaPrincipal.Historico.cs     Voltar/Avançar
  JanelaPrincipal.Tema.cs          Tema claro/escuro
  JanelaPrincipal.Dialogos.cs      Mensagens e perguntas (ContentDialog)
  ItemArvore.cs                    Item de cada nó da árvore
  IconesShell.cs                   Ícones do Explorer para pastas, unidades e arquivos
  NativeMethods.cs                 Chamadas à API do Windows (P/Invoke) usadas pelos ícones
  Instalador/
    Instalador.iss                 Script do Inno Setup
  wwwroot/
    preview-md-json-xml.html       Visualizador HTML (cópia do projeto preview-html)
    editor.html                    Página do editor (Monaco)
    monaco/                        Monaco Editor 0.52.2 (só Markdown, JSON e XML)
```

## Como funciona o visualizador

O painel da direita é um controle **WebView2** que carrega `wwwroot/preview-md-json-xml.html`, uma cópia do projeto `preview-html` (página única, 100% offline, com `marked.js` embutido). A página é servida pelo endereço virtual `https://preview.local/`, o que habilita a área de transferência e mantém o tema salvo entre execuções.

Quando um arquivo é selecionado na árvore, o programa lê o conteúdo e chama a função `renderContent(texto, nome)` da própria página, sem nenhuma alteração no HTML.

O modo **Editar** usa um segundo WebView2 com `wwwroot/editor.html`, que hospeda o [Monaco Editor](https://microsoft.github.io/monaco-editor/) (copiado localmente, também 100% offline). O programa conversa com ele chamando funções da página (`carregar`, `obterConteudo`, `marcarSalvo`...) e recebendo avisos quando o texto muda.
### Segurança

O conteúdo exibido (inclusive HTML embutido no Markdown) é tratado como não confiável, seguindo as [recomendações da Microsoft para WebView2](https://learn.microsoft.com/microsoft-edge/webview2/concepts/security):

- Só são aceitas mensagens vindas da página do visualizador.
- Links `http`, `https` e `mailto` abrem no aplicativo padrão; links para arquivos suportados abrem no próprio programa; **qualquer outro arquivo nunca é executado** — ele só é mostrado selecionado no Explorer. Links para caminhos de rede (`\servidor\...`) são recusados.
- DevTools ficam desligados na versão Release.
