#if NET48
// Definições de coleção xUnit para classes de teste que, comprovadamente (não presumidamente),
// disputam o MESMO recurso compartilhado do processo — ver docs/ARCHITECTURE.md, seção da
// investigação de falhas intermitentes da suíte (branch fix/test-suite-isolation).
//
// Por padrão, o xUnit roda cada CLASSE de teste como sua própria coleção, e coleções diferentes
// rodam em PARALELO entre si (dentro do mesmo processo testhost). Classes que mutam o mesmo
// estado estático de produção, ou leem/escrevem o mesmo arquivo/pasta real fora de um diretório
// de teste isolado, não são seguras para rodar em paralelo entre si — mesmo que cada teste,
// isoladamente, passe sempre. Agrupar essas classes com [Collection] faz o xUnit executá-las
// sequencialmente ENTRE SI, preservando paralelismo total com todas as outras ~20 classes de
// teste do projeto, que não compartilham nenhum desses recursos.
using Xunit;

namespace PXToolBox.Tests
{
    /// <summary>
    /// <see cref="PXToolBoxDev.Integrations.PxLogger"/> expõe um único campo estático de
    /// processo (<c>_logDirectoryOverride</c>, mutado por <c>SetLogDirectoryForTesting</c>) sem
    /// nenhuma sincronização — é um seam só para teste, nunca dimensionado para uso concorrente
    /// (em produção o PX Tool Box é um único add-on de thread única dentro do Corel). Rodando
    /// <see cref="PxLoggerTests"/> e <see cref="SupportPackageServiceTests"/> (duas classes,
    /// logo duas coleções, logo paralelas por padrão) ao mesmo tempo, o override de uma pode
    /// apontar o logger pro diretório temporário isolado de OUTRA no meio da execução — exatamente
    /// o padrão por trás do DirectoryNotFoundException observado ao apagar
    /// "pxtoolbox-sp-logs-&lt;guid&gt;" (a pasta nunca chegou a ser criada porque, quando a
    /// escrita aconteceu, o override já tinha sido trocado por outro teste) e das contagens
    /// erradas de retenção por tamanho em <see cref="PxLoggerTests.Retention_WhenTotalExceedsMaxBytes_RemovesOldestFilesFirst_KeepsNewest"/>.
    ///
    /// <para><b>2026-11:</b> esta coleção própria deixou de existir. As classes que constroem
    /// <c>ClassicWorkspaceWindow</c> também passaram a redirecionar o log (antes escreviam
    /// "ListForge Integrado iniciado" no log REAL a cada execução da suíte) e já estavam na coleção
    /// "PXToolBox Dev shared filesystem resources" pelo seam da configuração; como uma classe só
    /// pertence a uma coleção, <see cref="PxLoggerTests"/> e <see cref="SupportPackageServiceTests"/>
    /// foram para essa mesma coleção — todas as classes que mutam qualquer um dos dois seams
    /// estáticos rodam em sequência entre si.</para>
    /// </summary>

    /// <summary>
    /// <b>Revisão desta coleção (ver docs/ARCHITECTURE.md, seção 31 — auditoria pós-correção):</b>
    /// a justificativa ORIGINAL desta coleção — "todas tocam o mesmo diretório real
    /// <c>%LocalAppData%\PXToolBox\Dev\config\</c>" — ficou obsoleta: <see
    /// cref="ListForgeIntegratedSettingsStoreTests"/> e <see cref="ListForgeProcessingHistoryServiceTests"/>
    /// agora usam <see cref="PXToolBoxDev.Integrations.ListForgeIntegratedSettingsStore.SetConfigDirectoryForTesting"/>
    /// para apontar para um diretório TEMPORÁRIO isolado por instância de teste — nunca mais o
    /// caminho real, nem mesmo quando o PX Tool Box DEV estiver aberto de verdade na mesma
    /// máquina (risco que a simples serialização entre classes nunca cobria).
    ///
    /// <para>A disputa que JUSTIFICA esta coleção agora é outra, introduzida pelo próprio seam de
    /// teste acima: <c>ListForgeIntegratedSettingsStore._configDirectoryOverride</c> é um único
    /// campo estático de processo, sem sincronização — exatamente a mesma categoria de risco já
    /// documentada para <see cref="PXToolBoxDev.Integrations.PxLogger"/> acima. <see
    /// cref="ListForgeIntegratedSettingsStoreTests"/> MUTA esse campo (em cada teste, no
    /// construtor isolado); <see
    /// cref="ListForgeProcessingHistoryServiceTests.DefaultPath_IsUnderPxToolBoxDevConfig_NeverTheStandaloneAppDir"/>
    /// LÊ esse campo indiretamente via <c>ListForgeProcessingHistoryService.DefaultPath</c> →
    /// <c>ListForgeIntegratedSettingsStore.ConfigDirectoryForDisplay</c>, esperando o valor
    /// PADRÃO (não sobrescrito). Rodando as duas classes em paralelo (coleções diferentes por
    /// padrão), o segundo teste pode observar o diretório temporário que o primeiro acabou de
    /// configurar, em vez do caminho real esperado — falha de asserção, não IOException.</para>
    ///
    /// <para>Também mutam este campo (2026-11), pelo mesmo isolamento: <see
    /// cref="PanelFavoritesStoreTests"/>, <see cref="PanelSectionLayoutStoreTests"/> e as três
    /// classes que constroem <c>ClassicWorkspaceWindow</c> — <see cref="ListForgeIntegratedTests"/>,
    /// <see cref="ListForgePxModeTests"/> e <see cref="ManualOutputJsonEditingTests"/>. Antes,
    /// essas três gravavam no arquivo REAL de <c>%LocalAppData%</c> a cada execução da suíte (ex.:
    /// trocar de modo salva <c>PreferredWorkspaceModeByProfileId</c>).</para>
    ///
    /// <para><see cref="AtomicFileWriterTests"/> foi removida desta coleção: todos os seus testes
    /// já usam um diretório temporário próprio por teste (nunca o caminho real nem o override
    /// acima) e o lock estático de <see cref="PXToolBoxDev.Integrations.AtomicFileWriter"/>
    /// (<c>Sync</c>) já se auto-sincroniza — contenção nesse lock serializa escritas
    /// corretamente por si só, não é uma condição de corrida que precise de uma coleção xUnit
    /// por cima. O teste de retentativa sensível a tempo (orçamento de ~260ms) já foi corrigido
    /// trocando o <c>Timer</c> (despachado pelo ThreadPool, vulnerável a starvation sob carga
    /// paralela de QUALQUER outra coleção da suíte) por uma <c>Thread</c> dedicada, agendada pelo
    /// SO — correção que não depende de isolar esta classe das demais.</para>
    /// </summary>
    [CollectionDefinition("PXToolBox Dev shared filesystem resources")]
    public sealed class SharedFilesystemCollection { }

    /// <summary>
    /// <see cref="PXToolBoxDev.Integrations.ListForgeConfigStore"/> expõe um único campo
    /// estático de processo (<c>_filePathOverride</c>, mutado por
    /// <c>SetFilePathForTesting</c>) sem nenhuma sincronização — mesmo padrão/risco do seam de <see cref="PXToolBoxDev.Integrations.PxLogger"/> acima. Dois classes DIFERENTES o mutam: <see
    /// cref="ListForgeConfigStoreTests"/> (seus dois testes) e <see cref="NexorConfigStoreTests"/>
    /// (seu teste <c>NexorConfig_IsIndependentFromListForgeConfig_SeparateFiles</c>, que
    /// exercita <see cref="PXToolBoxDev.Integrations.ListForgeConfigStore"/> de propósito, para
    /// comprovar que configurar o Nexor não afeta o ListForge). Rodando as duas classes em
    /// paralelo, o override de uma apontaria <c>ListForgeConfigStore</c> para o diretório
    /// temporário da OUTRA no meio da execução. <see cref="NexorConfigStoreTests"/> também muta
    /// <c>NexorConfigStore._filePathOverride</c>, mas nenhuma outra classe lê ou grava esse campo
    /// específico (confirmado por busca em todo tests/PXToolBox.Tests), então esse campo em si
    /// não exige coleção — só o campo de <c>ListForgeConfigStore</c>, compartilhado entre as duas
    /// classes abaixo, exige.
    /// </summary>
    [CollectionDefinition("ListForge/Nexor launcher config-path static test seam")]
    public sealed class LauncherConfigPathCollection { }

    /// <summary>
    /// Testes que criam controles/bindings WPF por CÓDIGO (não por XAML) numa thread STA
    /// própria. Deadlock real observado (hang dump de 04/11/2026): <c>Binding</c> criado em
    /// código disparou o inicializador estático de <c>TextBox</c> → <c>ScrollViewer</c>, que
    /// esperava o lock de <c>WpfSharedBamlSchemaContext</c>; ao mesmo tempo outra classe
    /// (ShapeInspectorWindowTests) segurava esse lock carregando XAML e esperava o mesmo
    /// inicializador — ordem de locks invertida entre threads. As demais classes WPF da suíte só
    /// passam pelo carregamento de XAML (mesma ordem), por isso não se travam entre si. Esta
    /// coleção roda sozinha, depois das paralelas. Não afeta produção: dentro do Corel todo o
    /// WPF do PX roda numa única thread.
    /// </summary>
    [CollectionDefinition("WPF created in code — no parallel WPF initialization", DisableParallelization = true)]
    public sealed class WpfCodeCreatedControlsCollection { }
}
#endif
