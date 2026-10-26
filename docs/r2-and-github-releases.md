# Configuração — GitHub Releases e R2

## GitHub Releases

Requer **GitHub CLI** (`gh`) instalado e autenticado (`gh auth login`). O repositório é
identificado automaticamente a partir do remoto Git da pasta atual (`gh repo view`) — este
repositório ainda não tem um remoto configurado, então `create-github-release.ps1` falha até
que `git remote add origin ...` seja configurado e a tag correspondente exista no remoto.

Convenção de tags:
- DEV: `dev-v<versão>` (ex.: `dev-v0.1.0-dev.1`)
- Stable (futuro): `v<versão>` (ex.: `v1.0.0`)

Releases do canal Dev são sempre criadas como **pré-release**.

## R2

**A URL pública do ListForge é uma referência histórica e não é o destino do PXToolBox.**
Nenhum bucket, URL ou credencial foi inventado nesta entrega — a infraestrutura real ainda
precisa ser criada/configurada pelo usuário.

### Convenção de destino

`ReleaseBaseUrl` (passado para `build-release.ps1`) é a **raiz pública do produto**, sem
canal nem versão. Os scripts sempre completam como:

```
<ReleaseBaseUrl>/dev/releases/<versão>/<arquivo>
<ReleaseBaseUrl>/dev/latest.json

<ReleaseBaseUrl>/stable/releases/<versão>/<arquivo>   (futuro)
<ReleaseBaseUrl>/stable/latest.json                   (futuro)
```

No bucket R2, as chaves correspondentes (sem o host/domínio) seguem o prefixo fixo
`pxtoolbox/`:

```
pxtoolbox/dev/releases/<versão>/PXToolBox-DEV-Setup-<versão>.exe
pxtoolbox/dev/releases/<versão>/SHA256SUMS.txt
pxtoolbox/dev/releases/<versão>/release-manifest.json
pxtoolbox/dev/latest.json
```

### Variáveis de ambiente exigidas por `publish-r2-release.ps1`

Nunca cravadas no repositório, em manifestos, em pacotes ou em logs:

| Variável | Significado |
|---|---|
| `PXTOOLBOX_R2_ACCOUNT_ID` | ID da conta Cloudflare (usado para montar o endpoint `https://<id>.r2.cloudflarestorage.com`) |
| `PXTOOLBOX_R2_ACCESS_KEY_ID` | Chave de acesso do token R2 (compatível com S3) |
| `PXTOOLBOX_R2_SECRET_ACCESS_KEY` | Segredo do token R2 |
| `PXTOOLBOX_R2_BUCKET` | Nome do bucket |
| `PXTOOLBOX_R2_PUBLIC_BASE_URL` | Raiz pública (ex.: `https://assets.exemplo.com/pxtoolbox`) — usada só para montar as URLs do manifesto/`latest.json`, não para autenticação |

Requer **AWS CLI** (`aws`) instalado — R2 é compatível com a API S3.

### Comportamento de `publish-r2-release.ps1 -Publish`

1. Valida que os artefatos locais existem (mesmos de `test-release.ps1`).
2. Recusa publicar se já existir um objeto no destino para esta versão (nunca sobrescreve
   silenciosamente).
3. Publica os arquivos versionados (`releases/<versão>/...`) primeiro.
4. Confere o upload (`head-object` de cada arquivo).
5. Só então atualiza `latest.json` do canal.
6. Para em qualquer falha, sem anunciar "publicação concluída" — e sem atualizar
   `latest.json` se os arquivos versionados não tiverem sido confirmados.

Não remove versões anteriores — não existe flag de remoção automática neste comando.

### O que ainda falta configurar (real, não simulado)

- Criar o bucket R2 do PXToolBox (separado do bucket/prefixo do ListForge).
- Gerar um token de acesso R2 com permissão de escrita só nesse bucket.
- Definir o domínio público (Custom Domain do R2, ou outro CDN na frente do bucket) e usá-lo
  como `PXTOOLBOX_R2_PUBLIC_BASE_URL`.
- Decidir onde essas variáveis de ambiente ficam persistidas na máquina que roda os botões do
  Stream Deck (ex.: variáveis de ambiente de usuário no Windows) — fora do escopo desta
  entrega, mas necessário antes do primeiro uso real do Botão 7.
