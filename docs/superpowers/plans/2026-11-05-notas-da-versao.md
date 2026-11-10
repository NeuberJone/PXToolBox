# Notas da versão — plano de implementação

**Objetivo:** botão “Notas da versão” no rodapé do painel, abrindo uma janela WPF com as mudanças da versão em execução separadas em Implementado, Alterado e Removido.

**Desenho:** catálogo editorial embutido no assembly DEV, consultado pela versão exata (a mesma do rodapé). Sem rede nem dependência de documentos abertos. Versões locais/desconhecidas mostram ausência de notas explicitamente, sem assumir as notas de outra versão. A primeira entrada é `0.1.0-dev.59`, próxima versão livre; descreve somente esta entrega. Janela escura, redimensionável, com rolagem, quebra de linha e Fechar/Esc.

**Alternativas:** usar ROADMAP/Git em execução exigiria arquivos externos e atribuiria mudanças de várias versões à atual; buscar notas online criaria uma dependência desnecessária. Catálogo embutido garante que cada instalador leva suas próprias notas.

**Restrições:** preservar todas as edições anteriores; trabalhar na branch `feat/notas-da-versao`; sem commit/push/publicação. Produção continua sem pacote Stable. Preservar versões em `dist`. Somente smoke test dentro do CorelDRAW valida o add-on no ambiente real.

## Tarefas

- [x] Catálogo e testes: `ReleaseNotes/ReleaseNotesCatalog.cs`, `ReleaseNotes/release-notes.json`, recurso embutido no csproj e `ReleaseNotesTests.cs`. API `ReleaseNotesCatalog.ForVersion(string)` retorna `Version`, `Notice`, `Implemented`, `Changed`, `Removed`. Cobrir versão exata, desconhecida e local; nunca usar fallback para outra versão. Usar Newtonsoft.Json já disponível no host.
- [x] Interface: `ReleaseNotesWindow.xaml` e code-behind na mesma pasta; botão no rodapé de `ControlUI.xaml` com AutomationId `app.release-notes`, handler abre janela modal usando `ResolveAppVersion()`.
- [x] Documentar como consultar/manter as notas e roteiro de smoke test. Rodar testes completos e build; gerar instalador DEV `.59` sem publicar, se pré-requisitos disponíveis. Revisar diff com agente independente.

**Verificação:** testes do catálogo e compilação XAML real, suíte completa net8.0/net48, renderização fora do Corel se viável; smoke test real declarado pendente se não executado.

**Commit sugerido:** `feat: adiciona notas da versão ao painel`.

**Resultado:** compilação Debug/Release e suíte completa aprovadas (138 net8.0 + 448 net48).
Instalador `.59` gerado e validação do pacote aprovada. Renderização da janela e inspeção da
árvore visual passaram com o assembly empacotado. Revisão estática independente sem findings;
defeito de contraste/quebra no template identificado na renderização, reproduzido por
verificação que falhou e corrigido com propriedades explícitas. Smoke test no Corel pendente.
