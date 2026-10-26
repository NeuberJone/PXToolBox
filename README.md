# PX Tool Box

Conjunto modular de ferramentas para CorelDRAW. Nomes públicos: **PX Tool Box** (produção,
futura) e **PX Tool Box DEV** (esta entrega). Nome técnico/repositório: `PXToolBox`.

Esta é a primeira base DEV: **Core + Saídas**, compilável, instalável e testável dentro do
CorelDRAW 2026 (v27). Não inclui ListForge, Nexor nem integração de Moldes — ver
[`ROADMAP.md`](ROADMAP.md).

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

## Estrutura

```
src/PXToolBox.Core           núcleo independente do Corel (convenções de nome, medidas, resultado)
src/PXToolBox.Outputs        lógica das ferramentas de Saídas, via abstrações (sem COM)
host/PXToolBox.CorelHost.Dev add-on DEV: adaptadores COM reais + UI WPF hospedada pelo Corel
tests/PXToolBox.Tests        testes unitários (netstandard2.0/net8, sem CorelDRAW)
installer/                   script do Inno Setup do instalador DEV
scripts/                     implementação real dos scripts de release
docs/                        arquitetura, roadmap, smoke test, etc.
```

## Compilar e testar

```powershell
dotnet build PXToolBox.slnx -c Release
dotnet test tests\PXToolBox.Tests\PXToolBox.Tests.csproj
```

Os dois comandos acima **foram executados nesta máquina** como parte desta entrega: build sem
erros, 41/41 testes aprovados (ver `docs/mvp-smoke-test.md` para o registro completo e o que
ainda falta validar dentro do CorelDRAW real).

## Instalar a build DEV

- **Instalador oficial** (gerado por `build-release.ps1`): execute
  `dist\dev\<versão>\installer\PXToolBox-DEV-Setup-<versão>.exe` como administrador, com o
  CorelDRAW fechado.
- **Atalho local de desenvolvimento** (sem gerar instalador): `scripts\install-dev-local.ps1`.
- **Remover**: desinstalador do pacote (Painel de Controle) ou
  `scripts\uninstall-dev-local.ps1`.

Depois de instalar, abra (ou reinicie) o CorelDRAW 2026. Veja
[`docs/mvp-smoke-test.md`](docs/mvp-smoke-test.md) para o roteiro de verificação real dentro do
Corel.

## Scripts de release (botões do Stream Deck)

Ver [`docs/stream-deck-buttons.md`](docs/stream-deck-buttons.md) para os sete comandos exatos.

## Documentação

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — decisões de arquitetura e do ecossistema.
- [`ROADMAP.md`](ROADMAP.md) — o que está nesta entrega e o que vem depois.
- [`docs/migrated-tools.md`](docs/migrated-tools.md) — de onde vem cada ferramenta portada, o
  que mudou e o que ficou bloqueado.
- [`docs/mvp-smoke-test.md`](docs/mvp-smoke-test.md) — roteiro de teste real no CorelDRAW.
- [`docs/stream-deck-buttons.md`](docs/stream-deck-buttons.md) — os sete botões.
- [`docs/r2-and-github-releases.md`](docs/r2-and-github-releases.md) — publicação.
- [`AGENTS.md`](AGENTS.md) — decisões permanentes do projeto.

## Limitações conhecidas desta entrega

Ver a seção correspondente em `docs/migrated-tools.md` e `ROADMAP.md`. Resumo:

- "Agrupar Interseção" está implementado, mas a partir do candidato mais plausível encontrado
  numa revisão de macros — **nem essa revisão confirma** que é o algoritmo certo para esse
  botão. Ver `docs/migrated-tools.md`.
- O add-on não foi validado interativamente dentro do CorelDRAW nesta entrega (só build +
  testes automatizados) — o smoke test real é o próximo passo concreto, inclusive para as
  ferramentas baseadas na revisão candidata de macros (ela mesma se declara "não compilada
  nem executada no Corel").
- Produção (Stable) não está implementada; os scripts recusam explicitamente gerar/publicar
  um pacote rotulado como Stable.
