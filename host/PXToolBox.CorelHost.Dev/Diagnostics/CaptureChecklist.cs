using System;
using System.Collections.Generic;
using System.Linq;

namespace PXToolBoxDev.Diagnostics
{
    /// <summary>
    /// Roteiro das capturas antes/depois que ainda faltam para as ferramentas de montagem (ver
    /// docs/sisbolt-tools-investigation.md, coluna "Captura pendente"). Cada item diz o que
    /// preparar na página e o que fazer na ferramenta original entre "Capturar estado (antes)" e
    /// "Comparar com a captura (depois)". Só texto: nada aqui lê ou altera o documento.
    ///
    /// Os Ids são estáveis: viram o começo do nome do arquivo salvo (<see cref="CaptureReportStore"/>)
    /// e é por eles que o roteiro marca o que já foi feito. Texto visível nunca cita o nome do
    /// produto de origem (regra do usuário) — "a ferramenta original".
    /// </summary>
    public static class CaptureChecklist
    {
        public sealed class Item
        {
            public Item(string id, string tool, string variant, string prepare, string action)
            {
                Id = id;
                Tool = tool;
                Variant = variant;
                Prepare = prepare;
                Action = action;
            }

            public string Id { get; }
            public string Tool { get; }
            public string Variant { get; }

            /// <summary>O que deixar na página e selecionado ANTES de "Capturar estado (antes)".</summary>
            public string Prepare { get; }

            /// <summary>O que fazer na ferramenta original entre as duas capturas.</summary>
            public string Action { get; }

            /// <summary>Vai no campo "Ferramenta testada" do relatório.</summary>
            public string Label => $"{Tool} — {Variant}";
        }

        private const string Rect = "Um retângulo de 50 × 30 mm";

        // Âncoras do Alinhamento: posição na grade 3 × 3 da janela da ferramenta, sem supor o código
        // que cada uma grava (a superior esquerda gravou 0; as outras são o que se quer descobrir).
        private static readonly (string Id, string Name)[] Anchors =
        {
            ("top-center", "superior ao centro"),
            ("top-right", "superior direita"),
            ("middle-left", "meio à esquerda"),
            ("middle-center", "centro"),
            ("middle-right", "meio à direita"),
            ("bottom-left", "inferior esquerda"),
            ("bottom-center", "inferior ao centro"),
            ("bottom-right", "inferior direita"),
        };

        public static IReadOnlyList<Item> All { get; } = Build();

        public static Item? Find(string? id) => All.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.Ordinal));

        private static List<Item> Build()
        {
            var items = new List<Item>
            {
                new Item("stretch.off", "Esticar no Tamanho do Molde", "desligar",
                    $"{Rect} com Esticar no Tamanho do Molde já LIGADO pela ferramenta original. Selecione só ele.",
                    "Desligue o Esticar na ferramenta original."),
                new Item("single-group.other-name", "Criar grupo com item único", "objeto com outro nome",
                    $"{Rect} com o nome TESTE (Gerenciador de objetos → renomear). Selecione só ele.",
                    "Use Criar grupo com item único. O relatório mostra o nome que o grupo recebe."),
                new Item("single-group.rename", "Criar grupo com item único", "renomear o grupo",
                    "O grupo criado no item anterior, selecionado.",
                    "Renomeie o grupo pela própria ferramenta original, se ela tiver essa opção (senão, pelo Gerenciador de objetos), para TAMANHO P. O relatório mostra se o nome também vai para os dados do objeto."),
                new Item("ignore-metrics.off", "Ignorar métricas da variável", "desligar",
                    "Um texto com o nome NO e Ignorar métricas já LIGADO pela ferramenta original. Selecione só ele.",
                    "Desligue o Ignorar métricas na ferramenta original."),
                new Item("ignore-metrics.other-names", "Ignorar métricas da variável", "textos NU, AP e TS",
                    "Três textos, com os nomes NU, AP e TS. Selecione os três.",
                    "Ligue o Ignorar métricas na ferramenta original (se ela só aceitar um por vez, faça um de cada e anote)."),
                new Item("ignore-metrics.not-text", "Ignorar métricas da variável", "objeto que não é texto",
                    $"{Rect} com o nome NO. Selecione só ele.",
                    "Tente ligar o Ignorar métricas na ferramenta original. Anote se ela recusa ou mostra alguma mensagem."),
                new Item("alignment.margins", "Alinhamento", "quatro margens diferentes",
                    $"{Rect}. Selecione só ele.",
                    "Abra o Alinhamento, escolha a âncora superior esquerda e preencha as margens com valores diferentes: superior 1, direita 2, inferior 3, esquerda 4 (anote se os campos tiverem outros nomes). Aplique."),
            };

            foreach (var anchor in Anchors)
            {
                items.Add(new Item($"alignment.anchor.{anchor.Id}", "Alinhamento", $"âncora {anchor.Name}",
                    $"{Rect}, sem alinhamento gravado. Selecione só ele.",
                    $"Abra o Alinhamento, escolha a âncora {anchor.Name}, deixe as margens em branco e aplique."));
            }

            items.AddRange(new[]
            {
                new Item("alignment.clear", "Alinhamento", "Limpar",
                    "O retângulo do item \"quatro margens diferentes\" (com alinhamento gravado). Selecione só ele.",
                    "Abra o Alinhamento e clique em Limpar, sem aplicar mais nada. O relatório mostra se Limpar mexe no objeto ou só no formulário."),
                new Item("alignment.apply-after-clear", "Alinhamento", "Aplicar depois de Limpar",
                    "O mesmo retângulo, logo depois do item \"Limpar\". Selecione só ele.",
                    "Abra o Alinhamento, clique em Limpar e depois em Aplicar."),
                new Item("swap.heights", "Trocar de Posição", "alturas diferentes",
                    "Dois retângulos lado a lado com alturas diferentes (20 × 10 mm e 20 × 40 mm), com as bases desalinhadas. Selecione os dois.",
                    "Use Trocar de Posição. Anote qual foi selecionado primeiro."),
                new Item("powerclip.replicate", "Replicar PowerClip", "destino vazio e destino com conteúdo",
                    "Arquivo descartável: um PowerClip de origem com um texto fora do centro; um destino vazio (retângulo) e um destino que já é PowerClip com outro texto dentro. Selecione na ordem que a ferramenta pede e anote a ordem.",
                    "Use Replicar PowerClip."),
                new Item("powerclip.selection-order", "Replicar PowerClip", "outra ordem de seleção",
                    "Os mesmos objetos do item anterior (desfaça com Ctrl+Z antes), selecionados na ordem inversa.",
                    "Use Replicar PowerClip e anote o que aconteceu (ou a mensagem, se recusar)."),
                new Item("export-format.apply", "Formato de exportação de imagens", "escolher um formato",
                    "A página de teste com pelo menos um objeto. Selecione o que a ferramenta pedir.",
                    "Abra a ferramenta, escolha um formato (anote qual e as opções) e confirme."),
                new Item("export-format.change", "Formato de exportação de imagens", "trocar o formato",
                    "A mesma página, logo depois do item anterior.",
                    "Abra a ferramenta de novo, escolha outro formato (anote qual) e confirme."),
                new Item("h-align.reference-with-guid", "Alinhamento horizontal automático", "referência que já tem identificador",
                    "Uma referência já usada antes em um alinhamento horizontal automático e um objeto novo a alinhar. Selecione os dois.",
                    "Use o Alinhamento horizontal automático com essa mesma referência. O relatório mostra se o identificador é reaproveitado."),
                new Item("h-align.change-reference", "Alinhamento horizontal automático", "trocar a referência",
                    "Um objeto já vinculado a uma referência e uma OUTRA referência. Selecione os dois.",
                    "Use o Alinhamento horizontal automático para vincular o objeto à referência nova."),
                new Item("free-hand.off", "Mão Livre", "desligar",
                    "Um texto com o nome NO e Mão Livre já LIGADO pela ferramenta original. Selecione só ele.",
                    "Desligue a Mão Livre na ferramenta original."),
            });

            return items;
        }
    }
}
