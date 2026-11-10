# PX Tool Box

Conjunto modular de ferramentas para CorelDRAW. Nomes públicos: **PX Tool Box** (produção,
futura) e **PX Tool Box DEV** (esta entrega). Nome técnico/repositório: `PXToolBox`.

Esta é a primeira base DEV: **Core + Saídas**, compilável, instalável e testável dentro do
CorelDRAW 2026 (v27), com acesso ao **ListForge Standalone** (abre o executável externo, sem
incorporar sua interface) e ao **ListForge Integrado** (janela própria dentro do PX, consumindo
o núcleo de processamento compartilhado do ListForge) nos modos **Clássico** e **PX — em
desenvolvimento** — os dois sobre o mesmo estado de sessão, nunca duas implementações. Acesso ao
**Nexor** (abre o executável externo, sem incorporar sua interface nem compartilhar dados de
pedido — primeira etapa da integração com Produção) também incluído. A incorporação de contexto
de pedido do Nexor e a integração de Moldes não estão incluídas — ver [`ROADMAP.md`](ROADMAP.md).

## Requisitos

- Windows com **CorelDRAW 2026 (v27)** instalado (registro confirmado em
  `HKLM\SOFTWARE\Corel\CorelDRAW\27.0`; pasta `C:\Program Files\Corel\CorelDRAW Graphics Suite\27`).
- **.NET SDK** (testado com 10.0.202) — compila os projetos `netstandard2.0` e `net8.0`.
- **.NET Framework 4.8** (componente do Windows) + **MSBuild** de uma instalação do Visual
  Studio (ou Build Tools) — necessário para compilar `PXToolBox.CorelHost.Dev`, que roda
  hospedado dentro do processo do CorelDRAW (ver [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)
  para o porquê de ser .NET Framework, não .NET 8/10).
- **Inno Setup 6** (`ISCC.exe`) para gerar o instalador DEV.
- **GitHub CLI** (`gh`, autenticado) e **AWS CLI** (`aws`) apenas para os botões de
  publicação (opcional para build/teste local).

Pré-requisitos externos (Corel.Interop.VGCore.dll, Corel.Interop.CorelDRAW.dll) **não são
distribuídos neste repositório** — pertencem à instalação do CorelDRAW e são referenciados via
`HintPath` (ver `host/PXToolBox.CorelHost.Dev/PXToolBox.CorelHost.Dev.csproj`).

- Repositório do **ListForge** clonado como pasta **irmã** deste repositório (ex.:
  `C:\Projetos\ListForge`), na branch
  `refactor/shared-processing-core` ou posterior, com o projeto `ListForge.Core`. Necessário
  para compilar `PXToolBox.CorelHost.Dev` (ListForge Integrado — ver
  [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md), seção 10). Sobrescreva o caminho com
  `-p:ListForgeRepoDir="..."` se o checkout estiver em outro lugar; sem ele, o build falha com
  uma mensagem explicando onde procurá-lo.

## Estrutura

```
src/PXToolBox.Core                         núcleo independente do Corel (convenções de nome, medidas, resultado)
src/PXToolBox.Outputs                      lógica das ferramentas de Saídas, via abstrações (sem COM)
host/PXToolBox.CorelHost.Dev                add-on DEV: adaptadores COM reais + UI WPF hospedada pelo Corel
host/PXToolBox.CorelHost.Dev/Integrations   acesso ao ListForge standalone e ao Nexor (exclusivo do host DEV)
host/PXToolBox.CorelHost.Dev/ListForgeIntegrated  modos Clássico e PX do ListForge Integrado (exclusivo do host DEV)
host/PXToolBox.CorelHost.Dev/Diagnostics    "Inspecionar Shape.Properties" (diagnóstico, exclusivo do host DEV)
host/PXToolBox.CorelHost.Dev/Favorites      seção "Favoritos" do painel: catálogo de Ids estáveis e atalhos
host/PXToolBox.CorelHost.Dev/PanelLayout    "Personalizar painel…": catálogo de seções, layout e janela
tests/PXToolBox.Tests                      testes unitários (net8.0 + net48, sem CorelDRAW nem processos reais)
installer/                                 script do Inno Setup do instalador DEV
scripts/                                   implementação real dos scripts de release (inclui
                                            PxToolBoxVersioning.psm1 — comparação/descoberta de
                                            versões DEV/Stable, ver docs/ARCHITECTURE.md, seção 16)
docs/                                       arquitetura, roadmap, smoke test, etc.
```

## Compilar e testar

```powershell
dotnet build PXToolBox.slnx -c Release
dotnet test tests\PXToolBox.Tests\PXToolBox.Tests.csproj
```

Os dois comandos acima **foram executados nesta máquina** como parte desta entrega: build sem
erros, 54/54 testes aprovados na perna `net8.0` e 299/299 na perna `net48` (que soma os 54 com 10
testes exclusivos do launcher do ListForge Standalone, 168 do ListForge Integrado (teclado,
paridade funcional com o standalone incluindo Perfis de trabalho, Tamanhos customizados, Backup
automático de entrada, Diagnóstico/pacote de suporte, Edição manual da saída/JSON, Histórico de
processamentos, o modo PX, correção de PowerClip no "Remover NO/NU" e a correção da persistência
de perfis — ver `docs/listforge-classic-parity.md` e `docs/ARCHITECTURE.md`, seção 10, iterações
8, 23, 25 e 27), 11 do inspetor "Shape.Properties" (ver `docs/ARCHITECTURE.md`, seção 11), 11 do
launcher do Nexor (ver `docs/ARCHITECTURE.md`, seção 14), 14 das preferências portáteis
(`PortablePreferencesServiceTests` — ver seção 18) e 12 da importação de contexto de pedido
(`OrderContextServiceTests` — ver seções 19–20) — ver
`docs/mvp-smoke-test.md` para o registro completo e o que ainda falta validar dentro do
CorelDRAW real). A perna `net48` agora também referencia o repositório do ListForge
(`ListForge.Core`, pasta irmã deste — ver `docs/ARCHITECTURE.md`, seção 10); sem esse
checkout, o build falha com uma mensagem explicando onde procurá-lo.

## Instalar a build DEV

- **Instalador oficial** (gerado por `build-release.ps1`): execute
  `dist\dev\<versão>\installer\PXToolBox-DEV-Setup-<versão>.exe` como administrador. Não
  precisa fechar o CorelDRAW antes de abrir o instalador: ele mesmo detecta (via WMI) se o
  CorelDRAW está em execução e, se estiver, pede para fechar e verifica de novo em loop —
  Repetir até conseguir continuar, ou Cancelar para desistir (mesmo padrão do instalador do
  MoldMotor). A desinstalação faz a mesma verificação.
- **Atalho local de desenvolvimento** (sem gerar instalador): `scripts\install-dev-local.ps1`
  (este exige o CorelDRAW já fechado antes de rodar — é só uma cópia de arquivo, sem a
  verificação do instalador oficial).
- **Remover**: desinstalador do pacote (Painel de Controle) ou
  `scripts\uninstall-dev-local.ps1`.

Depois de instalar, abra (ou reinicie) o CorelDRAW 2026. O rodapé do painel mostra a versão em
execução (`PX Tool Box DEV • v<versão>`) — lida do próprio assembly, embutida em build time por
`build-release.ps1`; uma build local fora do script (`dotnet build`/`install-dev-local.ps1` sem
`-p:PxToolBoxVersion=...`) mostra `0.0.0-dev.local`, de propósito, para nunca ser confundida com
uma versão realmente publicada. Veja [`docs/mvp-smoke-test.md`](docs/mvp-smoke-test.md) para o
roteiro de verificação real dentro do Corel.

## Notas da versão (DEV)

No início da seção Diagnóstico (no rodapé até a dev.59), **Notas da versão** abre as mudanças da versão em execução, divididas em
**Implementado**, **Alterado** e **Removido**. A janela tem rolagem e fecha pelo botão Fechar ou
Esc. Funciona sem internet e sem documento aberto.

As notas são editoriais e acompanham o assembly: antes de gerar uma versão, adicione sua entrada
com o número exato em
`host/PXToolBox.CorelHost.Dev/ReleaseNotes/release-notes.json`. Registre somente as mudanças daquela
entrega; use listas vazias quando não houver itens em uma categoria. Builds locais
(`0.0.0-dev.local`) e versões sem entrada mostram um aviso explícito, sem assumir as notas de
outra versão. A primeira versão com este recurso é `0.1.0-dev.59`.

## Manual de uso (DEV)

Primeiro botão da seção Diagnóstico: **Manual de uso** abre o manual do painel — assuntos gerais
(Começando, Regras gerais, Favoritos, Personalizar painel, Preferências, Quando algo dá errado) e,
para cada seção, cada ferramenta com para que serve, como usar e observações. O campo
**Filtrar**, no topo, deixa na lista e nas páginas só o que tem as palavras digitadas. Pode ficar
aberto ao lado do Corel.

O texto fica em `host/PXToolBox.CorelHost.Dev/Manual/user-manual.json`. **Ao adicionar, remover
ou alterar uma ferramenta, atualize a entrada dela** (ver `AGENTS.md`); `UserManualTests` falha
se alguma ferramenta do catálogo ficar sem texto.

## Favoritos do painel (DEV)

A seção **Favoritos**, no topo do painel, junta atalhos para as ferramentas que você mais usa.

- **Adicionar/remover:** clique com o botão direito numa ferramenta compatível (ou foque nela e
  use Shift+F10) e escolha "Adicionar aos favoritos" / "Remover dos favoritos".
- **Atalhos em ícone (dev.53):** cada favorito é um botão quadrado com um ícone da ferramenta
  (MA/FE/C mostram a própria sigla; "+" âmbar no canto = cria algo novo). Parando o mouse em cima,
  a dica mostra o nome da ferramenta e a dica atual do botão original.
- **Ordenar/remover nos Favoritos:** botão direito no ícone (ou Shift+F10) → "Mover para a
  esquerda", "Mover para a direita" ou "Remover dos favoritos". O botão original continua na sua
  seção.
- **Comportamento:** o atalho dispara o mesmo clique do botão original e segue o estado dele —
  desabilitado quando o original está, e "Alternar Tamanho Padrão" fica azul quando ativo (azul
  continua significando só "ativo").
- **Compatíveis (dev.56):** todos os 44 botões de ferramenta das seções do painel — Grid,
  Códigos, Camadas, Preparação e saída, Outras ferramentas, Listas, Produção e Diagnóstico.
  Ferramentas que usam campos da seção (Escudo/Bandeira/Faixa: layout e pasta; Renomear: o nome
  digitado; Tipo de peça: as caixas de tipo) usam, pelo atalho, o que estiver nesses campos.
  "Ver relatório" do Diagnóstico mostra o relatório dentro da seção Diagnóstico.
- **Onde fica salvo:** na configuração do PX DEV
  (`%LocalAppData%\PXToolBox\Dev\config\listforge-integrated-settings.json`, campo
  `PanelFavoriteToolIds`), por identificador estável — nunca no documento do Corel. Também entra
  em "Exportar/Importar preferências…" (Configurações do ListForge Integrado).

Detalhes em `docs/ARCHITECTURE.md`, seção 35.

## Personalizar painel (DEV)

"Personalizar painel…", no cabeçalho do docker (sempre disponível), abre a lista das seções do
painel.

- **Exibir/ocultar e ordenar:** marque ou desmarque cada seção; ▲ e ▼ mudam a ordem. Padrão: a
  ordem original, com todas visíveis.
- **Salvar/Cancelar:** nada muda antes de "Salvar"; "Cancelar" (ou Esc, ou fechar) descarta o
  rascunho. As seções são só reordenadas/ocultadas — o docker não é recriado, e campos
  preenchidos e seções abertas continuam como estavam.
- **Restaurar layout padrão:** volta ordem e visibilidade das seções no rascunho (ainda é preciso
  "Salvar"). Não mexe nos favoritos.
- **Ocultar "Favoritos"** não apaga os atalhos; ao exibir de novo, voltam iguais.
- **Onde fica salvo:** no mesmo arquivo dos favoritos (campo `PanelSectionLayout`), por
  identificador estável e global (fora dos perfis de trabalho). Entra em "Exportar/Importar
  preferências…"; um arquivo sem layout (ex.: exportado antes da dev.53) mantém o layout atual.
  Seção nova numa atualização aparece visível no fim da lista.

Detalhes em `docs/ARCHITECTURE.md`, seção 36.

## Scripts de release (botões do Stream Deck)

Ver [`docs/stream-deck-buttons.md`](docs/stream-deck-buttons.md) para os sete comandos exatos.

## Documentação

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — decisões de arquitetura e do ecossistema.
- [`ROADMAP.md`](ROADMAP.md) — o que está nesta entrega e o que vem depois.
- [`docs/migrated-tools.md`](docs/migrated-tools.md) — de onde vem cada ferramenta portada, o
  que mudou e o que ficou bloqueado.
- [`docs/mvp-smoke-test.md`](docs/mvp-smoke-test.md) — roteiro de teste real no CorelDRAW.
- [`docs/acceptance/listforge-integrated-0.1.0-dev.26/`](docs/acceptance/listforge-integrated-0.1.0-dev.26/) —
  material de teste (TXT/PDF/DOCX/XLSX) e roteiro de validação de aceitação do ListForge
  Integrado, com checklist Aprovado/Falhou/Não testado — nenhum item ainda executado no Corel.
- [`docs/stream-deck-buttons.md`](docs/stream-deck-buttons.md) — os sete botões.
- [`docs/r2-and-github-releases.md`](docs/r2-and-github-releases.md) — publicação.
- [`AGENTS.md`](AGENTS.md) — decisões permanentes do projeto.

## Limitações conhecidas desta entrega

Ver a seção correspondente em `docs/migrated-tools.md` e `ROADMAP.md`. Resumo:

- "Agrupar Interseção" foi implementado a partir do candidato mais plausível encontrado numa
  revisão de macros e **confirmado pelo usuário no CorelDRAW real** no cenário testado. Ver
  `docs/migrated-tools.md`.
- Validação dentro do CorelDRAW é parcial: o docker e várias ferramentas já foram confirmados
  pelo usuário no Corel real (ver `ROADMAP.md`, "Smoke test no CorelDRAW — parcial"); os itens
  ainda pendentes estão marcados em `docs/mvp-smoke-test.md`.
- Produção (Stable) não está implementada; os scripts recusam explicitamente gerar/publicar
  um pacote rotulado como Stable.
- **ListForge Standalone** (seção "Listas"): a lógica de decisão (`ListForgeLauncher`/
  `ListForgeConfigStore`) tem 10 testes automatizados reais, sem abrir nenhum processo real.
  Abrir o ListForge de verdade pelo botão do painel, dentro do CorelDRAW real, ainda não foi
  validado — ver `docs/mvp-smoke-test.md`.
- **Nexor** (nova seção "Produção"): primeira etapa da integração — só abrir o executável já
  existente (`NexorLauncher`/`NexorConfigStore`, mesmo padrão de decisão do ListForge
  Standalone, configuração independente/separada), sem incorporar a interface do Nexor nem
  compartilhar dados de pedido. 11 testes automatizados reais, sem abrir nenhum processo real e
  sem ler/alterar `nexor.db`. Distribuição real localizada no ambiente (não suposta): o
  executável que o usuário já usa no dia a dia, fixado na barra de tarefas — ver
  `docs/ARCHITECTURE.md`, seção 12. Abrir o Nexor de verdade pelo botão do painel, dentro do
  CorelDRAW real, ainda não foi validado — ver `docs/mvp-smoke-test.md`.
- **Contexto de produção do Nexor** (nova seção "Produção", botão "Carregar contexto de
  produção…"): primeira recepção manual (arquivo .json), só leitura, só memória — nunca toca
  `nexor.db`, nunca chama o Nexor, nunca altera documento do Corel nem sessão do ListForge.
  Contrato documentado em `docs/integrations/nexor-order-context.md` depois de investigar o
  código real do Nexor: **não existe pedido de cliente formal hoje** (sem número/cliente/
  status) — "pedido informado" é uma etiqueta livre sobre um trabalho de impressão dentro de um
  rolo; o contrato usa o identificador real `(rollId, jobHash)`, confirmado único só DENTRO de
  uma mesma base `printlogs.db` (nunca globalmente). Dois campos opcionais ajudam a
  desambiguar, com garantias testadas e DIFERENTES: `installationId` (por instalação/máquina —
  confirmado, por testes reais, que pode repetir entre bases diferentes da mesma instalação, ou
  duplicar se a configuração for copiada sem a base) e `databaseId` (gravado dentro do próprio
  arquivo `.db` — confirmado que sobrevive a cópia/restauração e é igual para instalações
  diferentes acessando a mesma base compartilhada em rede, por isso mais confiável para "mesma
  base"). Nenhum dos dois é identificação global formal. O texto da interface (PX e Nexor)
  nunca chama isto de "pedido" como identificador comercial nem de "Job" — só os nomes
  técnicos do contrato permanecem, por compatibilidade. Ver `docs/ARCHITECTURE.md`, seções
  19–23. **Exportação do lado do Nexor já implementada** (repositório `ProjetoJocasta` — botão
  "Exportar contexto de produção..." na aba Pedidos; `pedidoManual` nunca inventado, só
  leitura) — compatibilidade real confirmada entre exportador/importador de verdade (três
  vezes, incluindo com `installationId` e `databaseId` juntos), não só por contrato
  documentado. **Carregar colando o caminho** (botão "Colar caminho do contexto…", ao lado do
  seletor): complementa o seletor de arquivo, mesma validação (`OrderContextPathLoader`, ver
  `docs/ARCHITECTURE.md`, seção 24) — trata caminho vazio/pasta/inexistente/ilegível/JSON
  incompatível, aceita aspas externas copiadas do Windows, nunca interpreta o texto como
  comando nem tenta acesso de rede, nunca persiste o caminho. Nenhum smoke test real no Corel
  ainda (nem para o seletor, nem para colar o caminho).
- **Preferências portáteis do PX** (botões "Exportar/Importar preferências…" em
  Configurações): distinto do recurso mais amplo já existente "Exportar/Importar
  configurações…" (que inclui perfis de trabalho inteiros, com caminhos da máquina) — exporta
  só o que é realmente portável (tamanho de fonte, preferência de modo Clássico/PX por NOME de
  perfil, favoritos do painel), nunca caminhos de executáveis, licença ou perfis de trabalho. Importação valida,
  mostra resumo, exige confirmação e salva backup recuperável antes de aplicar. Ver
  `docs/ARCHITECTURE.md`, seção 18.
- **ListForge Integrado**: modos **Clássico** e **PX — em desenvolvimento** implementados
  (janela própria, botão "Abrir ListForge Integrado"), consumindo o núcleo compartilhado
  (`ListForge.Core`, repositório do ListForge) por referência de projeto real. O modo PX é uma
  segunda APRESENTAÇÃO sobre o mesmo estado de sessão do Clássico (reparenting dos mesmos
  controles — nunca duas cópias de entrada/opções/resultado) — ver `docs/ARCHITECTURE.md`,
  seção 10, "Modo PX implementado", para a implementação e as limitações explícitas (sem acesso
  a objetos do documento Corel, sem Nexor/OCR/licenciamento novo; smoke test real no Corel ainda
  pendente). **Teclado confirmado funcionando pelo usuário
  dentro do Corel real** (hook de thread `WH_GETMESSAGE` + foco nativo reforçado — ver
  `docs/ARCHITECTURE.md`, seção 10, iteração 4) — mecanismo preservado sem alteração a partir
  daqui. Contraste da aba selecionada corrigido (iteração 3). **Correção crítica (iteração 8)**:
  o CorelDRAW inteiro fechava (crash real, não só a janela) ao abrir o ListForge Integrado, em
  `0.1.0-dev.14` — causa confirmada (`NullReferenceException` não capturada dentro do
  `InitializeComponent()`, escapando do construtor invocado por reflexão pelo host do Corel) e
  corrigida, com reprodução controlada do bug antes e depois da correção — ver
  `docs/ARCHITECTURE.md`, seção 10, iteração 8. **Confirmado pelo usuário dentro do CorelDRAW
  real** (`0.1.0-dev.15`): a janela abre normalmente, sem o Corel fechar.

  A tarefa passou então a completar a **paridade funcional com o ListForge standalone real**
  (`docs/listforge-classic-parity.md` — inventário de 41 funções contra o código real de
  `C:\Projetos\ListForge`, branch `main`, v2.1.44, não documentação antiga). 1ª rodada: bloqueio
  de resultado desatualizado (Copiar/Salvar/Gerar JSON avisam e pedem confirmação antes de usar
  um resultado que não corresponde mais à entrada atual, em vez de entregá-lo silenciosamente),
  navegação da lista de avisos/erros até a linha correspondente, e Localizar/Substituir movido
  para o núcleo compartilhado (antes duplicado à mão no integrado). **2ª rodada**: Extrair lista
  de link (mesmo `LinkListImportService` do standalone, movido para o núcleo compartilhado),
  Lista avançada / mapeamento de peça por slot JSON (serviço já compartilhado — UI nova, com
  seletor por posição de tamanho ajustado automaticamente conforme a entrada muda), e resumo de
  tamanhos processados. **3ª rodada**: Processar com prévia de confirmação — agora o botão
  "Processar" padrão (antes só havia o modo rápido), mostra contagem de válidos/avisos/inválidos,
  tamanhos e tipos de peça antes de efetivamente processar (`ProcessingPreviewService`, movido
  para o núcleo compartilhado nesta rodada). "Processar rápido (sem prévia)" continua disponível,
  como alternativa secundária — mesma distinção do standalone. **4ª rodada**: Comparar
  entrada/saída — mesmas categorias semânticas (correspondente/reorganizado/transformado/
  alterado/possivelmente ausente/adicionado/incerto), filtro, navegação entre diferenças,
  detalhamento por campo e "Copiar relatório" (`ListComparisonService`, movido para o núcleo
  compartilhado nesta rodada); nunca compara silenciosamente a entrada atual com uma saída
  antiga — usa o mesmo aviso de resultado desatualizado já existente. **5ª rodada**: Salvar
  avançado — salva entrada, saída e JSON juntos, em arquivos soltos ou em um único ZIP
  (`AdvancedSaveService`, movido para o núcleo compartilhado nesta rodada); exige "Lista
  avançada" ativa e um processamento atual, igual ao standalone; nunca sobrescreve (nome
  incrementado automaticamente em caso de colisão, mesmo mecanismo real do standalone) e
  identifica exatamente quais arquivos foram gravados e quais ficaram pendentes se a gravação
  falhar no meio (reforço desta entrega). **6ª rodada**: Configurações do módulo — tela
  "Configurações..." própria, armazenamento em
  `%LocalAppData%\PXToolBox\Dev\config\listforge-integrated-settings.json` (reaproveitando a
  pasta já usada pelo botão "Abrir ListForge Standalone", não uma convenção nova; nunca o
  ListForge Standalone, o PX de produção ou o MoldMotor), esquema versionado, gravação segura
  (nunca deixa arquivo truncado) e configuração corrompida colocada em quarentena em vez de
  sobrescrita silenciosamente. Preferências persistidas: tamanho de fonte do editor, formato
  padrão do Salvar avançado (resolvendo a limitação "só em memória" da rodada anterior), pasta e
  nome padrão de exportação (evita diálogo a cada Salvar saída/Gerar JSON quando os dois estão
  configurados), com Exportar/Importar configurações (.json) e "Abrir pasta de configuração".
  **7ª rodada**: Perfis de trabalho — criar/selecionar/renomear/duplicar/excluir/restaurar,
  mesmas regras do standalone (`WorkProfileService`, **movido** para `ListForge.Core` nesta
  rodada; `AppConfig` do standalone passou a implementar a interface extraída
  `IWorkProfileConfig` para permitir o reuso sem acoplar o núcleo compartilhado ao `AppConfig`
  concreto). Um perfil guarda separador/capitalização/ordenação/lista avançada (editados na
  janela principal) e o formato/pasta/nome padrão do Salvar avançado (editados em
  "Configurações...", que passaram a pertencer ao perfil ATIVO em vez de serem campos únicos no
  topo — esquema 2, migrado explicitamente a partir do esquema 1 sem perder nada já configurado).
  **8ª rodada**: Tamanhos customizados — `SizeConfigWindow` nova, 4 grupos fixos (masculino/
  feminino/infantil/meião) com tamanhos-base/prefixos/sufixos em CSV, resumo e "Restaurar
  tamanhos padrão" próprio, reusando por completo `ListForge.Core.SizeHelper`/`SizeConfig` (já
  compartilhados — nenhum arquivo precisou ser movido desta vez, diferente de Perfis de
  trabalho). Independente de perfil de trabalho — igual ao standalone; vive em
  `ListForgeIntegratedSettings.Sizes`, campo de módulo, aditivo (sem precisar mudar o esquema).
  `_sizeConfig` deixou de ser fixo em `SizeConfig.Default()` — agora controla de fato
  reconhecimento/validação/ordenação/exportação (prova de ponta a ponta: tamanho adicionado é
  reconhecido, tamanho removido é rejeitado, nunca convertido silenciosamente para outro).
  Pacote de suporte e "abrir pasta de logs" continuam ausentes por dependerem de uma
  infraestrutura de log do integrado que não existe. Abrir/salvar lista, remover espaços, limpar
  tudo e copiar saída/JSON (de iterações anteriores) continuam disponíveis. Licenciamento/Trial
  do modo integrado é uma decisão pendente, não alterada nesta tarefa (não bloqueia o
  processamento hoje, mas a interface informa isso explicitamente, só em Diagnóstico). Modo
  **PX** não implementado. **9ª rodada**: Backup automático de "Salvar entrada"/"Salvar como" —
  `ListForgeInputBackupService` nova, mesma assimetria do standalone (backup só quando o
  conteúdo em disco mudou, em "Salvar entrada"; sempre que o destino já existir, em "Salvar
  como"), em `PXToolBox\Dev\ListForge\Backups\`, sem retenção (igual à referência real) e
  "Abrir pasta de backups" no Diagnóstico. **10ª rodada**: conclusão do grupo Diagnóstico e
  Suporte — `PxLogger`/`SupportPackageService` (infraestrutura iniciada numa rodada anterior,
  então ainda incompleta) passaram a registrar também as falhas de Processar/Processar com
  prévia/Comparar entrada e saída/Salvar avançado/Abrir lista/Salvar perfis de trabalho (além de
  startup/shutdown/backup de entrada, já existentes), com retenção de logs (14 dias/20 MB,
  comprovada por teste como nunca afetando a pasta de backups), informações de suporte completas
  com "Copiar informações de suporte", e geração manual do pacote de suporte (`.zip` com
  relatório técnico + resumo seguro de configurações + logs recentes — nunca lista/backup/
  arquivo de produção/segredos). **11ª rodada**: Edição manual da saída/JSON — checkbox "Permitir
  edição... nesta sessão" (nunca persistido, igual ao standalone), "Aplicar alterações"/
  "Descartar alterações" reprocessando via `ProcessingWorkflowService`/
  `ListProcessor.ExtractListTextFromJsonData` (núcleo compartilhado), indicador "Resultado
  editado manualmente" sempre distinto do aviso de entrada desatualizada, reprocessar nunca
  descarta uma edição pendente em silêncio, comparação aceita e sinaliza uma saída editada, e a
  fonte de texto de Copiar/Salvar/Gerar JSON/Salvar avançado corrigida para a mesma regra
  viva-vs-canônica do standalone (protege contra exportar JSON ainda não revalidado). **12ª
  rodada**: Histórico de processamentos — `ListForgeProcessingHistoryService` nova (porta de
  `ProcessingHistoryService`) + `ProcessingHistoryWindow` nova (porta de `HistoryView`), gravando
  em `PXToolBox\Dev\config\listforge-integrated-history.json` (mesma pasta de
  `ListForgeIntegratedSettingsStore`, nunca o `AppDir` do ListForge Standalone). Um registro só é
  criado quando "Salvar saída"/"Gerar JSON"/"Salvar avançado" gravam um arquivo de fato — nunca
  só por processar. A referência real **não tem** exclusão individual de registro nem
  "restaurar" uma lista a partir do histórico (confirmado lendo o código do standalone por
  completo antes de implementar) — esta porta reproduz só "Abrir pasta da saída" e "Limpar
  histórico" (com confirmação), sem inventar as duas que não existem. Limite de 100 registros,
  sem expiração por idade (igual à referência). Excluído do pacote de suporte por padrão.
  **13ª rodada**: Importação de documentos — PDF/Word (.docx)/Excel (.xlsx/.xlsm). O núcleo
  real do standalone (`FileImporter`/`FileImportService`, usando PdfPig/DocumentFormat.OpenXml/
  ClosedXML) foi **movido** para `ListForge.Core` nesta rodada, com os 3 pacotes NuGet
  confirmados compatíveis com netstandard2.0 **e** com o host net48 por build real dos dois
  lados (não suposição). "Abrir Lista..." (antes só `.txt`) passou a aceitar esses formatos, com
  as mesmas regras da referência: lê o arquivo inteiro (Excel: só a primeira planilha — a
  referência não oferece escolha, então esta porta também não inventa uma), mostra o conteúdo
  extraído na própria caixa de entrada (editável) para revisão, nunca processa/exporta por conta
  própria. Imagem/OCR (Tesseract — binários nativos + `tessdata`) permanece **fora** desta
  entrega, de propósito — dependência pesada, decisão já registrada antes e reconfirmada aqui; o
  botão agora reconhece extensões de imagem e informa explicitamente a limitação, em vez de um
  "formato não suportado" genérico. Reforço desta entrega: confirma antes de substituir uma
  entrada com alterações não salvas ao importar um documento (a referência substitui sem
  perguntar) e a extração roda fora da thread de UI.

  **Com a conclusão desta rodada, restam só três lacunas reais** (ver
  `docs/listforge-classic-parity.md`, recontagem linha a linha: Completo 36 · Parcial 2 ·
  Ausente 1 · Exclusivo do standalone 3, total 42): Trial/licenciamento do modo integrado
  (decisão de produto, não bloqueia hoje, 2 entradas na tabela) e Importar arquivo — imagem/OCR,
  dependência opcional ainda não integrada (Tesseract: binários nativos + arquivos de idioma) —
  isto não significa necessariamente que a integração futura exigiria instalação automática; a
  forma de distribuição é uma decisão de produto ainda não tomada. **Enquanto houver essa
  lacuna, e
  nenhum smoke test real no Corel tiver sido feito para as rodadas de Comparar entrada/saída,
  Salvar avançado, Configurações, Perfis de trabalho, Tamanhos customizados, Backup de entrada,
  Diagnóstico/pacote de suporte, Edição manual da saída/JSON, Histórico de processamentos e
  Importação de documentos, a própria janela continua identificada como "Clássico — em
  desenvolvimento"** — só a correção do
  crash de abertura (iteração 8) foi de fato confirmada dentro do CorelDRAW real até agora.
