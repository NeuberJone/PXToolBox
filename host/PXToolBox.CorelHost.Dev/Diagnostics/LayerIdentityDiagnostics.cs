using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Corel.Interop.VGCore;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBoxDev.Corel;

namespace PXToolBoxDev.Diagnostics
{
    /// <summary>
    /// Diagnóstico DEV da premissa da restauração de camadas (dev.47): o Corel entrega o MESMO
    /// objeto de sessão para o mesmo documento/camada? Usa exatamente o caminho de produção —
    /// adaptadores Corel/* (Identity, AllLayers, OpenDocuments, ActiveSelection → Shape.Layer)
    /// e <see cref="LayerVisibilityService.IsSameSessionObject"/> — e roda o serviço real numa
    /// instância separada, sem tocar o registro do painel.
    ///
    /// <para>Só age num documento temporário criado aqui (salvo/reaberto em %TEMP% e apagado no
    /// fim). Dos documentos do usuário só lê a lista de abertos e reativa o que estava ativo.
    /// Nunca lê/grava Properties. Nunca chama ReleaseComObject: os RCWs são os mesmos que o
    /// resto do add-in usa — referências são só largadas.</para>
    ///
    /// <para>O resultado vale para esta máquina, versão do Corel e sessão — não é garantia da
    /// API. [OBS] marca comportamentos sem resposta "certa": o serviço já trata os dois casos
    /// com segurança.</para>
    /// </summary>
    internal sealed class LayerIdentityDiagnostics
    {
        public const string Explanation =
            "Este diagnóstico vai, no CorelDRAW:\n\n" +
            "• criar um documento temporário NOVO — seus documentos não são lidos nem alterados (só a lista de abertos é consultada);\n" +
            "• criar camadas e retângulos nele, renomear, reordenar, excluir/recriar camadas e usar Desfazer;\n" +
            "• ocultar e restaurar camadas DELE com o mesmo serviço do botão \"Mostrar últimas camadas ocultadas\" (instância separada — o registro do painel não é tocado);\n" +
            "• salvar esse documento em um arquivo temporário (%TEMP%), fechá-lo, reabri-lo, fechá-lo de novo e apagar o arquivo;\n" +
            "• reativar o documento que estava ativo.\n\n" +
            "Nada é gravado em Properties. O Corel troca de documento durante alguns segundos.\n\n" +
            "Executar agora?";

        private const string Prefix = "PX_DIAG_";

        private readonly Application _app;
        private readonly StringBuilder _report = new StringBuilder();
        private int _failures;

        private object? _watchedDocument;
        private bool _closeEventFired;
        private bool _closeEventSameObject;
        private LayerVisibilityService? _closeService;

        private LayerIdentityDiagnostics(Application app)
        {
            _app = app;
        }

        public static string Run(Application app, string toolVersion) =>
            new LayerIdentityDiagnostics(app).Execute(toolVersion);

        private string Execute(string toolVersion)
        {
            Line($"Diagnóstico de identidade de sessão — PX Tool Box v{toolVersion}, CorelDRAW {SafeVersion()}, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            var originalActive = _app.ActiveDocument;
            var openBefore = _app.Documents.Count;
            Line($"Documentos abertos antes: {openBefore}");

            Document? doc = null;
            Document? reopened = null;
            var tempFile = Path.Combine(Path.GetTempPath(), $"px-layer-identity-{Guid.NewGuid():N}.cdr");
            var subscribed = false;
            try
            {
                doc = _app.CreateDocument();
                var docId = Doc(doc).Identity;

                Section("Documento");
                Expect("ActiveDocument é o mesmo objeto do documento criado", Same(Workspace().ActiveDocument?.Identity, docId));
                Expect("ActiveDocument lido 2 vezes devolve o mesmo objeto", Same(Workspace().ActiveDocument?.Identity, Workspace().ActiveDocument?.Identity));
                Expect("encontrado exatamente 1 vez em Application.Documents", DocMatches(docId) == 1);
                Expect("encontrado exatamente 1 vez em nova leitura de Documents", DocMatches(docId) == 1);

                Section("Camadas");
                var page = doc.ActivePage;
                var layerA = page.CreateLayer(Prefix + "A");
                var layerB = page.CreateLayer(Prefix + "B");
                var layerC = page.CreateLayer(Prefix + "C");
                var shapeA = layerA.CreateRectangle(0, 1, 1, 0);
                layerB.CreateRectangle(2, 1, 3, 0);
                var idA = SelectedLayerIdentity(shapeA);
                Obs("Shape.Layer (via seleção) é o mesmo objeto devolvido por CreateLayer", Same(idA, new CorelLayerAdapter(layerA).Identity));
                Expect("Shape.Layer lido 2 vezes devolve o mesmo objeto", Same(SelectedLayerIdentity(shapeA), idA));
                Expect("camada encontrada exatamente 1 vez em AllLayers", LayerMatches(doc, idA) == 1);

                layerA.Name = Prefix + "A_RENOMEADA";
                Expect("após renomear: ainda encontrada exatamente 1 vez", LayerMatches(doc, idA) == 1);

                var indexBefore = layerA.Index;
                layerA.MoveBelow(layerC);
                Obs($"reordenação mudou o índice ({indexBefore} → {layerA.Index})", layerA.Index != indexBefore);
                Expect("após reordenar: ainda encontrada exatamente 1 vez", LayerMatches(doc, idA) == 1);

                var idC = new CorelLayerAdapter(layerC).Identity;
                layerC.Delete();
                Expect("após excluir: a camada excluída não é mais encontrada", LayerMatches(doc, idC) == 0);
                var recreatedC = page.CreateLayer(Prefix + "C");
                Expect("camada recriada com o mesmo nome NÃO é o objeto antigo", !Same(new CorelLayerAdapter(recreatedC).Identity, idC));
                Expect("após recriar: a identidade antiga continua sem correspondência", LayerMatches(doc, idC) == 0);

                var idB = new CorelLayerAdapter(layerB).Identity;
                layerB.Delete();
                doc.Undo(1);
                var restoredB = Doc(doc).AllLayers.Where(l => l.Name == Prefix + "B").ToList();
                Expect("Desfazer exclusão: camada B voltou (por nome, só para este diagnóstico)", restoredB.Count == 1);
                Obs("Desfazer exclusão: é o MESMO objeto de antes (se não, a restauração diz \"não encontrada\")",
                    restoredB.Count == 1 && Same(restoredB[0].Identity, idB));
                Expect("Desfazer exclusão: identidade antiga achada no máximo 1 vez", LayerMatches(doc, idB) <= 1);

                Section("Serviço real (instância separada do painel)");
                var service = new LayerVisibilityService();
                var hide = Hide(service, doc, shapeA);
                Expect($"ocultar registrou a operação (auto-verificação de identidade passou) — \"{hide.Message}\"", hide.IsSuccess && service.LastHidden != null);
                var restore = Restore(service, doc);
                Expect($"restaurar mostrou a camada — \"{restore.Message}\"", restore.IsSuccess && layerA.Visible);

                Hide(service, doc, shapeA);
                doc.Undo(1);
                Obs("Desfazer a ocultação deixou a camada visível", layerA.Visible);
                var afterUndo = Restore(service, doc);
                Expect($"restaurar depois do Desfazer não altera nada indevido — \"{afterUndo.Message}\"", layerA.Visible && service.LastHidden == null);

                Section("Descarte das referências do registro");
                var replaced = HideAndTrack(service, doc, shapeA);
                var shapeRecreatedC = recreatedC.CreateRectangle(4, 1, 5, 0);
                Hide(service, doc, shapeRecreatedC);
                Expect("nova ocultação substitui o registro e o anterior fica livre para coleta", IsCollected(replaced));

                var released = Track(service);
                service.ReleaseSessionState("Indisponível: painel encerrado (diagnóstico).");
                Expect("ReleaseSessionState (mesmo método usado ao fechar o painel) solta o registro", service.LastHidden == null && IsCollected(released));
                Line("  Limitação: o fechamento real do painel (evento Unloaded) não é simulado aqui — validar fechando o docker (roteiro 4i).");

                Section("Fechar e reabrir");
                layerA.Visible = true;
                recreatedC.Visible = true;
                var closing = HideAndTrack(service, doc, shapeA);
                _watchedDocument = docId;
                _closeService = service;
                _app.DocumentClose += OnDocumentClose;
                subscribed = true;
                Doc(doc).SaveAsCdr(tempFile);
                doc.Dirty = false;
                doc.Close();
                doc = null;
                Expect("evento DocumentClose recebido para o documento temporário", _closeEventFired);
                Expect("o argumento do DocumentClose é o mesmo objeto do registro", _closeEventSameObject);
                Expect("registro descartado ao fechar o documento de origem", service.LastHidden == null);
                Expect("documento fechado não é mais encontrado em Documents", DocMatches(docId) == 0);
                Expect("registro descartado ao fechar fica livre para coleta", IsCollected(closing));

                reopened = _app.OpenDocument(tempFile);
                Expect("arquivo reaberto é OUTRO objeto (a operação não é recuperada)", !Same(Doc(reopened).Identity, docId));
                Expect("camada do arquivo reaberto não corresponde à identidade antiga", LayerMatches(reopened, idA) == 0);
                Expect("serviço continua sem registro depois de reabrir", service.LastHidden == null);
            }
            catch (Exception ex)
            {
                _failures++;
                Line($"[ERRO] interrompido: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (subscribed)
                {
                    try { _app.DocumentClose -= OnDocumentClose; } catch { /* app encerrado */ }
                }
                _watchedDocument = null;
                _closeService = null;
                CloseQuietly(doc);
                CloseQuietly(reopened);
                try { File.Delete(tempFile); } catch { /* removido ou inacessível — informado abaixo */ }
                try { originalActive?.Activate(); } catch { /* documento original fechado pelo usuário */ }

                Section("Limpeza");
                Line($"Documentos abertos depois: {SafeCount()} (antes: {openBefore})");
                Line($"Arquivo temporário apagado: {(File.Exists(tempFile) ? "NÃO — " + tempFile : "sim")}");
            }

            Line("");
            Line(_failures == 0
                ? "Resultado: nenhuma falha nesta execução. Vale para esta máquina/versão/sessão — não é garantia universal da API."
                : $"Resultado: {_failures} falha(s). Não use a restauração como confiável nesta instalação até investigar.");
            return _report.ToString();
        }

        // Mesmo caminho do handler do painel (ControlUI.OnCorelDocumentClose).
        // Mesmo caminho do handler do painel (ControlUI.OnCorelDocumentClose). Só o documento
        // temporário é fechado enquanto inscrito.
        private void OnDocumentClose(Document closed)
        {
            if (_watchedDocument == null) return;
            _closeEventFired = true;
            _closeEventSameObject |= LayerVisibilityService.IsSameSessionObject(closed, _watchedDocument);
            _closeService?.OnDocumentClosed(closed);
        }

        private OperationResult Hide(LayerVisibilityService service, Document doc, Shape shape)
        {
            shape.CreateSelection();
            var ws = Workspace();
            doc.BeginCommandGroup("PX diagnóstico — ocultar");
            try
            {
                return service.HideLayersOfSelection(Doc(doc), ws.ActiveSelection, ws.OpenDocuments);
            }
            finally
            {
                doc.EndCommandGroup();
            }
        }

        private OperationResult Restore(LayerVisibilityService service, Document doc)
        {
            var ws = Workspace();
            doc.BeginCommandGroup("PX diagnóstico — restaurar");
            try
            {
                return service.RestoreLastHiddenLayers(ws.ActiveDocument, ws.OpenDocuments);
            }
            finally
            {
                doc.EndCommandGroup();
            }
        }

        private WeakReference? HideAndTrack(LayerVisibilityService service, Document doc, Shape shape)
        {
            var result = Hide(service, doc, shape);
            Expect($"ocultação registrada — \"{result.Message}\"", service.LastHidden != null);
            return Track(service);
        }

        // Fora do método principal para nenhuma variável local manter o registro vivo.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference? Track(LayerVisibilityService service) =>
            service.LastHidden == null ? null : new WeakReference(service.LastHidden);

        // Só GC.Collect: o registro é um objeto gerenciado sem finalizador. Sem
        // WaitForPendingFinalizers — esperar finalizadores de RCW na thread STA do Corel não é
        // necessário aqui e não deve bloquear o host. Sem registro rastreado = não comprovado.
        private static bool IsCollected(WeakReference? reference)
        {
            if (reference == null) return false;
            GC.Collect();
            GC.Collect();
            return !reference.IsAlive;
        }

        private object? SelectedLayerIdentity(Shape shape)
        {
            shape.CreateSelection();
            var shapes = Workspace().ActiveSelection.Shapes;
            return shapes.Count == 1 ? shapes[0].Layer?.Identity : null;
        }

        private CorelWorkspaceAdapter Workspace() => new CorelWorkspaceAdapter(_app);

        private static ICorelDocument Doc(Document doc) => new CorelDocumentAdapter(doc);

        private int DocMatches(object identity) =>
            LayerVisibilityService.CountIdentityMatches(Workspace().OpenDocuments.Select(d => d.Identity), identity);

        private static int LayerMatches(Document doc, object? identity) =>
            identity == null ? 0 : LayerVisibilityService.CountIdentityMatches(Doc(doc).AllLayers.Select(l => l.Identity), identity);

        private static bool Same(object? a, object? b) =>
            a != null && b != null && LayerVisibilityService.IsSameSessionObject(a, b);

        private static void CloseQuietly(Document? doc)
        {
            if (doc == null) return;
            try { doc.Dirty = false; doc.Close(); } catch { /* já fechado */ }
        }

        private string SafeVersion()
        {
            try { return _app.Version; } catch { return "?"; }
        }

        private string SafeCount()
        {
            try { return _app.Documents.Count.ToString(); } catch { return "?"; }
        }

        private void Section(string title) => Line($"— {title}");

        private void Expect(string description, bool ok)
        {
            if (!ok) _failures++;
            Line($"  [{(ok ? "OK" : "FALHOU")}] {description}");
        }

        private void Obs(string description, bool observed) =>
            Line($"  [OBS] {description}: {(observed ? "sim" : "não")}");

        private void Line(string text) => _report.Append(text).Append(Environment.NewLine);
    }
}
