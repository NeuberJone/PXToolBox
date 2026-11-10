# Validação de aceitação — ListForge Integrado, build 0.1.0-dev.26

Instalador: `dist\dev\0.1.0-dev.26\installer\PXToolBox-DEV-Setup-0.1.0-dev.26.exe` (ver
`dist\dev\0.1.0-dev.26\SHA256SUMS.txt`/`release-manifest.json`). Nenhuma alteração de código
nesta tarefa — só preparação de material de teste e deste roteiro (ver `AGENTS.md`: nunca gerar
uma versão nova sem alteração correspondente).

## Separação obrigatória de três coisas diferentes (não confundir)

1. **Implementação** — já documentada em `docs/listforge-classic-parity.md`, `README.md` e
   `ROADMAP.md`. Este documento não repete nem reavalia esse conteúdo.
2. **Testes automatizados** — já executados de verdade nesta máquina (232/232 na perna `net48`,
   54/54 na `net8.0` do PXToolBox; 282/282 no ListForge Standalone) — ver histórico de
   `ROADMAP.md`. Provam que o código compila e que a lógica de extração/processamento se
   comporta como esperado em isolamento, **nunca** que a janela funciona de verdade dentro do
   CorelDRAW.
3. **Validação no Corel (este documento)** — **nada abaixo foi executado ainda.** Este arquivo
   só prepara o material e o roteiro. Cada item só pode ser marcado **Aprovado** depois de
   alguém (usuário humano, com o CorelDRAW real aberto) executar o passo e confirmar o
   resultado. Nunca marque um item como aprovado só porque "deveria funcionar" ou porque os
   testes automatizados passaram — são coisas diferentes.

## Nota sobre o travamento intermitente dos testes `net48` (causa não determinada)

Durante a geração das últimas builds, o passo de testes (`net48`) de `build-release.ps1` travou
de forma intermitente, sem terminar nem falhar, em pelo menos três execuções desta sessão.
Em cada ocorrência, processos `testhost.net48.exe`/`dotnet.exe` relacionados ao comando de teste
foram identificados (nome, linha de comando, processo pai e horário confirmados antes de agir) e
encerrados para permitir uma nova tentativa, que então terminou normalmente.

**Isso não é prova suficiente de que a causa seja só processo órfão ou ambiente** — em pelo
menos uma ocorrência, a trava aconteceu com o ambiente já limpo (nenhum outro processo
concorrente identificado antes do travamento), e em nenhuma ocorrência foi capturado um dump de
thread ou outra evidência direta do que o processo estava fazendo no momento da trava. **A causa
real permanece não determinada.** Não reproduz de forma determinística num teste isolado (a
mesma suíte, executada sozinha e repetidamente, completou em segundos todas as vezes em que foi
tentada fora do script de build) — mas isso também não é suficiente para afirmar que o script ou
o ambiente, isoladamente, sejam a causa. Registrado aqui para não ser esquecido nem
reapresentado como "resolvido" em uma entrega futura sem evidência nova.

## Nota sobre OCR

Importação de imagem via OCR **não está integrada** ao modo Clássico do ListForge Integrado
nesta build. É uma dependência opcional (Tesseract, binários nativos + arquivos de idioma) ainda
não incorporada — isto não significa necessariamente que ela exigiria instalação automática
quando/se for integrada; a forma de distribuição (bundled, instalador separado, download sob
demanda etc.) é uma decisão de produto ainda não tomada, não uma conclusão técnica já fechada.

## Material de teste

Todos os arquivos abaixo têm o MESMO conteúdo lógico (4 pessoas), gerado com as mesmas
bibliotecas reais de produção (`DocumentFormat.OpenXml`/`ClosedXML`) e verificado por extração +
processamento reais antes de entrar neste pacote (ver "Resultado esperado" abaixo) — **isto é
teste automatizado de preparação do material, não validação no Corel**.

| Arquivo | Caminho completo | Conteúdo |
|---|---|---|
| Texto | `docs\acceptance\listforge-integrated-0.1.0-dev.26\lista-amostra.txt` | 4 linhas, texto puro |
| PDF com texto | `docs\acceptance\listforge-integrated-0.1.0-dev.26\lista-amostra.pdf` | 4 páginas, uma pessoa por página, fonte Helvetica/WinAnsiEncoding |
| Word | `docs\acceptance\listforge-integrated-0.1.0-dev.26\lista-amostra.docx` | 4 parágrafos |
| Excel | `docs\acceptance\listforge-integrated-0.1.0-dev.26\lista-amostra.xlsx` | 4 linhas, 3 colunas (Nome/Número/Tipo) |
| PDF sem texto | `docs\acceptance\listforge-integrated-0.1.0-dev.26\lista-amostra-pdf-sem-texto.pdf` | 1 página, sem nenhum operador de texto — para testar a mensagem de limitação |

Conteúdo lógico (idêntico nos 4 primeiros arquivos):

```
João Silva,07,G
Maria Oliveira,10,BLM
José Costa,08,M
Ana Souza,09,P
```

**Resultado esperado ao processar** (verificado de ponta a ponta com o núcleo real
`ListForge.Core`/`ListForge.Services`, separador `,`, maiúsculas/ordenação "Original", antes
desta tarefa ser entregue — ver nota de metodologia ao final):

- Status: sucesso, sem nenhum aviso/erro de validação, 4 linhas reconhecidas.
- Saída organizada idêntica à entrada (nomes com acento preservados: "João", "José"; números com
  zero à esquerda preservados como texto: "07", "08", "09", nunca convertidos para "7"/"8"/"9").
- No JSON gerado: 4 pedidos, campo `Number` igual à string original (`"07"`/`"10"`/`"08"`/
  `"09"`), nomes com acentos preservados.
- **Importando o PDF especificamente**: como cada pessoa está numa página separada, o texto
  extraído tem uma linha em branco entre cada pessoa (junção de páginas do núcleo compartilhado,
  comportamento real do `FileImporter.ReadPdfText`, não um defeito) — confirme que isso NÃO
  impede o processamento (o validador ignora linhas vazias).

### Nota de metodologia (como o "resultado esperado" foi obtido)

Os 4 arquivos de amostra foram gerados por um programa C# de uso único (não faz parte do
repositório, descartado após gerar os arquivos) que: (1) escreveu cada formato usando as MESMAS
bibliotecas de produção (`DocumentFormat.OpenXml.WordprocessingDocument`, `ClosedXML.XLWorkbook`,
e um gerador mínimo de PDF válido para o caso do `.pdf`, já que `PdfPig` só lê PDF, não escreve);
(2) leu cada arquivo de volta com o MESMO código real (`ListForge.Core.FileImporter.
ReadTextFile/ReadPdfText/ReadDocxText/ReadExcelText`); (3) processou o texto extraído com o MESMO
`ProcessingWorkflowService` real usado pelos dois produtos. Os 4 formatos produziram o MESMO
resultado de processamento, sem avisos. Isto é uma verificação automatizada de que os ARQUIVOS DE
AMOSTRA são válidos e representativos — **não substitui nenhum item do roteiro abaixo**, que
precisa ser executado de verdade dentro do ListForge Integrado, no CorelDRAW real.

---

## Roteiro no Corel

Convenção de cada item: **Esperado** descreve o que deve acontecer. **Resultado**: preencha com
`Aprovado` / `Falhou` / `Não testado` — nunca deixe como `Aprovado` sem ter executado o passo de
verdade.

### A. Abrir/reabrir, digitar e atalhos sem acionar ferramentas do Corel

| # | Passo | Esperado | Resultado | Observações |
|---|---|---|---|---|
| A1 | Abrir o CorelDRAW 2026, painel PX Tool Box DEV → "Listas" → "Abrir ListForge Integrado" | Janela "ListForge Integrado — Clássico" abre normalmente, sem o CorelDRAW fechar/travar | | |
| A2 | Clicar no campo "Entrada / edição" e digitar `JOÃO,07,G` (com acento e zero à esquerda) | Texto digitado aparece exatamente como digitado, sem disparar nenhum atalho/ferramenta do Corel (nenhuma paleta/ferramenta muda) | | |
| A3 | Usar Ctrl+A, Ctrl+C, Ctrl+V, Ctrl+X, Ctrl+Z dentro do campo de entrada | Cada atalho funciona como edição de texto padrão (selecionar tudo/copiar/colar/recortar/desfazer), nunca aciona uma ferramenta do Corel | | |
| A4 | Verificar contraste dos textos/botões no tema atual do Corel (claro e escuro, se houver os dois instalados) | Todo texto legível, sem texto preto sobre fundo escuro nem o contrário | | |
| A5 | Navegar pelos campos só com Tab/Shift+Tab | Foco visível se move entre os controles em ordem lógica | | |
| A6 | Fechar a janela e reabrir pelo mesmo botão | Reabre normalmente, sem crash, sem janela duplicada presa | | |

### B. Importar cada formato e conferir o conteúdo

Use os arquivos da tabela de material de teste, acima.

| # | Passo | Esperado | Resultado | Observações |
|---|---|---|---|---|
| B1 | "Abrir Lista...", selecionar `lista-amostra.txt` | Conteúdo das 4 linhas aparece no campo de entrada, status "Lista aberta: ...", sem diálogo de revisão (texto puro não precisa) | | |
| B2 | "Abrir Lista...", selecionar `lista-amostra.pdf` | Conteúdo extraído aparece no campo de entrada (4 nomes, com linhas em branco entre eles — ver nota acima), mensagem informando para revisar antes de processar | | |
| B3 | Comparar o texto do passo B2 com o mesmo PDF aberto no ListForge Standalone | Texto correspondente (mesmos nomes/números, mesma pontuação) | | |
| B4 | "Abrir Lista...", selecionar `lista-amostra.docx` | Conteúdo extraído (4 linhas, sem linha em branco entre elas) aparece no campo de entrada, com mensagem de revisão | | |
| B5 | "Abrir Lista...", selecionar `lista-amostra.xlsx` | Conteúdo extraído (4 linhas) aparece no campo de entrada, com mensagem de revisão | | |
| B6 | Em B2/B4/B5, confirmar visualmente que "João"/"José" aparecem com acento (nunca "Jo?o"/"Jos?" ou similar) e que os números aparecem como "07"/"08"/"09"/"10" (nunca "7"/"8"/"9"/"10" sem zero) | Acentos e zeros à esquerda preservados nos 3 formatos | | |
| B7 | Digitar algo no campo de entrada (deixando alterações não salvas) e importar outro arquivo da tabela | Pergunta de confirmação antes de substituir; responder "Não" preserva o texto digitado; importar de novo e responder "Sim" substitui normalmente | | |
| B8 | "Abrir Lista...", selecionar `lista-amostra-pdf-sem-texto.pdf` | Mensagem explicando que não foi possível extrair texto do PDF (sugestão de usar imagem/OCR), sem apresentar uma importação vazia como sucesso, entrada atual preservada | | |
| B9 | "Abrir Lista...", escolher "Todos os arquivos" e tentar selecionar uma imagem (.png/.jpg) qualquer | Mensagem específica informando que importação via OCR não está disponível nesta distribuição, sem travar | | |
| B10 | Tentar importar um arquivo corrompido (ex.: renomear um `.txt` qualquer para `.docx`) | Mensagem de erro clara, sem travar o Corel, entrada atual preservada | | |

### C. Processar, revisar avisos e comparar entrada/saída

| # | Passo | Esperado | Resultado | Observações |
|---|---|---|---|---|
| C1 | Com `lista-amostra.txt` carregado, clicar "Processar" (com prévia) | Prévia mostra 4 registros válidos, 0 inválidos, 0 avisos | | |
| C2 | Confirmar a prévia | Saída organizada gerada igual à entrada; resumo de tamanhos exibido | | |
| C3 | Alterar a entrada para incluir uma linha claramente inválida (ex.: `SEM_TAMANHO_NENHUM`) e processar | Lista de avisos/erros mostra a linha problemática; clicar nela navega até a linha correspondente no campo de entrada | | |
| C4 | Desfazer a alteração (voltar ao conteúdo original) e processar de novo | Volta ao resultado de C2, sem avisos | | |
| C5 | Clicar "Comparar entrada/saída" | Janela de comparação abre mostrando as 4 linhas como correspondentes (sem diferenças), já que a saída é igual à entrada | | |
| C6 | Alterar a entrada sem reprocessar e clicar "Comparar entrada/saída" de novo | Aviso de que a entrada mudou desde o último processamento, recusando comparar silenciosamente | | |

### D. Editar saída/JSON, aplicar e exportar

| # | Passo | Esperado | Resultado | Observações |
|---|---|---|---|---|
| D1 | Em "Configurações...", marcar "Permitir edição da saída e do JSON nesta sessão" | Caixas de saída/JSON tornam-se editáveis | | |
| D2 | Editar a saída organizada (ex.: adicionar uma linha `PEDRO,11,GG`) e clicar "Aplicar alterações" | Reprocessa com sucesso, indicador "Resultado editado manualmente" aparece | | |
| D3 | Clicar "Gerar JSON" e abrir o arquivo gerado | JSON contém a pessoa adicionada em D2, números com zero à esquerda preservados (`"07"` etc.) | | |
| D4 | Clicar "Descartar alterações" após uma nova edição não aplicada | Volta ao último resultado válido, indicador de edição manual desaparece | | |
| D5 | Clicar "Salvar saída" e abrir o `.txt` gerado | Conteúdo igual ao exibido na caixa de saída no momento do clique | | |

### E. Salvar entrada duas vezes e conferir backup

| # | Passo | Esperado | Resultado | Observações |
|---|---|---|---|---|
| E1 | Com `lista-amostra.txt` aberto, editar o conteúdo e clicar "Salvar Entrada" | Salva sem backup (primeiro salvamento após abrir, sem alteração anterior em disco diferente — ver regra exata em `docs/listforge-classic-parity.md`) | | |
| E2 | Editar de novo e clicar "Salvar Entrada" outra vez | Um backup do conteúdo ANTERIOR aparece em "Abrir pasta de backups" | | |
| E3 | Clicar "Salvar Entrada" sem alterar nada | Nenhum backup novo é criado | | |
| E4 | Abrir o backup gerado em E2 e comparar com o conteúdo que existia antes de E2 | Conteúdo idêntico, nunca truncado | | |

### F. Conferir histórico e abrir pasta da saída

| # | Passo | Esperado | Resultado | Observações |
|---|---|---|---|---|
| F1 | Depois de D3 (Gerar JSON) e D5 (Salvar saída), clicar "Histórico..." | Dois registros novos aparecem no topo (um para o `.json`, um para o `.txt`), com data/hora atuais e contagem de linhas | | |
| F2 | Selecionar o registro do `.json` e clicar "Abrir pasta da saída" | Explorer abre com o arquivo já selecionado | | |
| F3 | Clicar apenas em "Processar" (sem exportar nada) | Nenhum registro novo aparece no histórico (histórico só registra exportação de arquivo, nunca só processamento) | | |
| F4 | Clicar "Limpar histórico" | Pergunta de confirmação; confirmar remove todos os registros, mas NÃO apaga os arquivos `.txt`/`.json` já salvos em disco | | |

### G. Aplicar perfil e tamanho personalizado — persistência e invalidação de resultado

| # | Passo | Esperado | Resultado | Observações |
|---|---|---|---|---|
| G1 | Criar um novo perfil de trabalho com um separador diferente (ex.: `;`) | Perfil criado, campos da janela atualizados para refletir o novo separador | | |
| G2 | Fechar e reabrir o ListForge Integrado | Perfil criado em G1 continua na lista, ainda selecionado se era o ativo | | |
| G3 | Abrir "Tamanhos customizados...", adicionar um tamanho novo a um grupo (ex.: "99" no grupo Masculino) e salvar | Tela fecha sem erro; `_sizeConfig` atualizado | | |
| G4 | Processar uma lista usando o tamanho "99" adicionado em G3 | Reconhecido como válido (sem aviso de tamanho inválido) | | |
| G5 | Depois de processar com sucesso, editar a entrada (ex.: adicionar uma linha) sem reprocessar | Aviso visível de que o resultado está desatualizado em relação à entrada atual | | |
| G6 | Com o aviso de G5 visível, clicar "Copiar saída" ou "Salvar saída" | Confirmação pedindo se deseja usar um resultado desatualizado, em vez de copiar/salvar silenciosamente | | |

### H. Gerar pacote de suporte e conferir conteúdo

| # | Passo | Esperado | Resultado | Observações |
|---|---|---|---|---|
| H1 | No painel Diagnóstico, clicar "Gerar pacote de suporte..." | Mensagem lista o conteúdo (versões/ambiente, resumo seguro de configurações, logs recentes) antes de perguntar o destino | | |
| H2 | Confirmar e escolher uma pasta de destino | Um `.zip` é gerado nessa pasta | | |
| H3 | Abrir o `.zip` gerado | Contém só `support-info.txt`, `settings-summary.txt` e uma pasta `logs/` — **nunca** o conteúdo das listas importadas/digitadas nesta sessão (nomes "João Silva" etc. não devem aparecer em nenhum arquivo do pacote) | | |
| H4 | Abrir `settings-summary.txt` | Não contém nome de perfil, pasta ou nome de lista configurados | | |
| H5 | Abrir os logs incluídos | Não contém o conteúdo de nenhuma lista importada/digitada nesta sessão, nem o caminho completo de arquivos de cliente reais (só o nome/tipo da operação) | | |

---

## Resumo para registrar depois da execução

Preencha ao final (sem marcar nada como aprovado sem execução real):

- Data da execução:
- Versão do CorelDRAW usada:
- Total de itens Aprovado / Falhou / Não testado:
- Itens que falharam (com detalhe do que aconteceu):
- Qualquer observação nova sobre o travamento dos testes `net48` (se reproduzido de novo fora
  desta sessão, registrar aqui com detalhes — nunca descartar como "mesma causa de antes" sem
  evidência).
