# Botões do Stream Deck

Mesma organização de sete botões já usada no ListForge. Execute sempre a partir da raiz do
repositório. `VersãoAtual`, `MensagemDoCommit` e `URL_PUBLICA_R2_PXTOOLBOX` são placeholders —
substitua pelos valores reais antes de rodar.

## Botão 1 — Gerar build DEV

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-release.ps1 -Version VersãoAtual -Channel Dev -ReleaseBaseUrl "URL_PUBLICA_R2_PXTOOLBOX"
```

Restaura, compila (Debug e Release), testa, publica o host e compila o instalador DEV com
Inno Setup. Gera checksums e um manifesto em `dist\dev\<versão>\`. Não publica nada. Recusa
sobrescrever uma versão/canal já existente sem `-Force`.

## Botão 2 — Commit e push da branch atual

```powershell
git add .
git commit -m "MensagemDoCommit"
git push
```

Operado manualmente pelo usuário. Execute os três em sequência; **pare no primeiro erro**.
Este projeto não automatiza nem altera o comportamento global do Git.

## Botão 3 — Refazer build DEV

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-release.ps1 -Version VersãoAtual -Channel Dev -ReleaseBaseUrl "URL_PUBLICA_R2_PXTOOLBOX" -Force
```

Igual ao Botão 1, mas permite refazer **somente** a pasta `dist\dev\<versão>` (nunca outras
versões, nunca fontes, nunca configurações).

## Botão 4 — Validar release DEV

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\test-release.ps1 -Version VersãoAtual -Channel Dev
```

Confere presença dos artefatos obrigatórios, integridade dos checksums, coerência do
manifesto e ausência de termos sensíveis nos arquivos de texto do pacote. Falha (código de
saída ≠ 0) se qualquer coisa estiver ausente ou inconsistente. **Isto é validação de pacote,
não smoke test real no Corel** — ver `docs/mvp-smoke-test.md`.

## Botão 5 — Criar e enviar tag DEV

```powershell
git tag dev-vVersãoAtual
git push origin dev-vVersãoAtual
```

Operado manualmente. O `push` da tag só deve ser executado se a criação da tag (linha
anterior) tiver terminado sem erro.

## Botão 6 — Criar pré-release no GitHub

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\create-github-release.ps1 -Version VersãoAtual -Channel Dev -Create
```

Sem `-Create`: só mostra o plano (repositório, tag exigida, artefatos a anexar) e faz
checagens de leitura — nenhuma mutação remota. Exige que a tag `dev-vVersãoAtual` já exista e
esteja publicada no remoto (rode o Botão 5 antes). Cria sempre como pré-release no canal Dev.
Recusa sobrescrever uma release já existente para a mesma tag.

## Botão 7 — Publicar release DEV no R2

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\publish-r2-release.ps1 -Version VersãoAtual -Channel Dev -Publish
```

Sem `-Publish`: só valida configuração (variáveis de ambiente) e artefatos, e mostra o plano
— nenhum upload. Ver `docs/r2-and-github-releases.md` para a configuração necessária e a
convenção de destino. Publica os arquivos versionados primeiro, confere o upload, e só então
atualiza o ponteiro `latest.json` do canal. Nunca remove versões anteriores
(`-RemovePreviousVersion` não existe neste comando, de propósito).
