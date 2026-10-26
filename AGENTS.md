# AGENTS.md — decisões permanentes do PX Tool Box

Este arquivo registra decisões que não devem ser revisitadas a cada tarefa. Para contexto e
detalhes, ver `docs/ARCHITECTURE.md`, `ROADMAP.md` e `docs/migrated-tools.md`.

## Produção vs. DEV

- Um repositório, código compartilhado, **duas distribuições com identidade própria**:
  pastas de instalação, nomes de assembly, GUIDs de docker, configuração, logs e canal de
  atualização são sempre exclusivos de cada distribuição (ver `docs/ARCHITECTURE.md`, seção
  2). Recursos exclusivos do DEV são excluídos do pacote de Produção — nunca só escondidos na
  interface.
- Não existe branch DEV permanente. Use branches de tarefa (ex.: `feat/<escopo>`).
- `build-release.ps1 -Channel Stable` deve continuar recusando gerar um pacote enquanto o
  empacotamento de produção não estiver de fato implementado — nunca rotular um pacote DEV
  como Stable para "aparentar suporte".

## Responsabilidades do ecossistema

- **Nexor**: pedidos, planejamento, filas, produção, registros e status. Dono do status
  operacional mesmo depois de integrações futuras.
- **ListForge**: leitura/interpretação/normalização/validação/processamento de listas; saída
  SISBolt/JSON. O standalone continua existindo permanentemente, independente do
  CorelDRAW/PX.
- **PX Tool Box**: execução no CorelDRAW — objetos, camadas, montagem, preparação, Grid e
  saída.
- Comunicação entre produtos usa contratos versionados — nunca referências às classes
  internas de outro produto. Não criar infraestrutura genérica de integração antes de existir
  uma necessidade concreta.

## MoldMotor

- Permanece independente. Nunca compartilha identidade, licença, updater, configuração,
  GUIDs ou diretórios de instalação com o PX Tool Box. O nome "MoldMotor" não aparece na UI
  do módulo integrado do PX.

## Builds e versões

- Builds/versões anteriores são sempre preservadas em disco
  (`dist\<canal>\<versão>\`) — `-Force` em `build-release.ps1` só pode recriar a pasta da
  versão/canal pedida, nunca outra.
- Nenhum script de release publica, cria tags/releases ou envia para o remoto sem a flag de
  ação explícita (`-Create`, `-Publish`) ou sem ser um dos botões operados manualmente pelo
  usuário (commit/push, criação/push de tag).

## Prompts e entregas de desenvolvimento

- Toda entrega e todo prompt de desenvolvimento futuro deve incluir: sugestão de branch
  adequada ao escopo e sugestão de mensagem de commit (ou mensagens por responsabilidade, se
  o trabalho tiver múltiplos commits).
- **Nunca afirme que um teste, build ou instalador foi executado/gerado se não foi.**
  Diferencie sempre: revisão estática de código, compilação real, testes automatizados reais,
  e smoke test real dentro do CorelDRAW — são quatro coisas diferentes, e só a última valida
  o add-on no ambiente real.
