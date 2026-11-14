using System;
using System.Collections.Generic;
using System.Linq;
using PXToolBox.Core.Nesting;

namespace PXToolBoxDev.Nesting
{
    public enum NestingProfileArea
    {
        Page,
        Roll,
        Areas,
    }

    public enum NestingProfileMethod
    {
        Box,
        Contour,
    }

    public enum NestingProfileRotation
    {
        None,
        HalfTurn,
        QuarterTurns,
    }

    /// <summary>
    /// Um perfil salvo do "Encaixar peças": os campos da janela como foram digitados (texto, não número —
    /// aplicar um perfil é o mesmo que digitar, e a validação de sempre vale). As quantidades não entram:
    /// dependem das peças selecionadas. Os valores iniciais são os da janela.
    /// </summary>
    public sealed class NestingProfile
    {
        public const int MaximumProfiles = 30;
        public const int MaximumNameLength = 40;

        public string Name { get; set; } = "";
        public NestingProfileArea Area { get; set; } = NestingProfileArea.Page;
        public string RollWidth { get; set; } = "160";

        /// <summary>Altura de cada folha do rolo; vazio = comprimento livre.</summary>
        public string RollHeight { get; set; } = "";
        public string AreasText { get; set; } = "100 x 150\n80 x 60";
        public NestAreaOrder AreaOrder { get; set; } = NestAreaOrder.AsTyped;
        public bool DrawAreas { get; set; } = true;

        /// <summary>Cada área/folha vira um grupo; as folhas do rolo ficam lado a lado.</summary>
        public bool GroupBlocks { get; set; }

        /// <summary>Espaço entre blocos/áreas no desenho, em cm.</summary>
        public string BlockGap { get; set; } = "2";

        /// <summary>Com "Agrupar por bloco": legenda acima de cada bloco.</summary>
        public bool Legends { get; set; }

        /// <summary>No rolo: cada camada encaixada sozinha, num bloco próprio.</summary>
        public bool SplitByLayer { get; set; }
        public bool ResizePage { get; set; }
        public string Margin { get; set; } = "0,5";
        public string Spacing { get; set; } = "0,5";
        public NestOrigin Origin { get; set; } = NestOrigin.TopLeft;
        public NestingProfileMethod Method { get; set; } = NestingProfileMethod.Box;
        public string ContourCell { get; set; } = "5";
        public NestingProfileRotation Rotation { get; set; } = NestingProfileRotation.None;
        public bool SearchBest { get; set; }
        public string TimeLimit { get; set; } = "30";

        /// <summary>Nome vazio, comprido demais ou repetido (sem diferenciar maiúsculas) e lista grande demais.</summary>
        public static IReadOnlyList<string> Validate(IReadOnlyList<NestingProfile> profiles)
        {
            var problems = new List<string>();
            if (profiles.Count > MaximumProfiles) problems.Add($"Até {MaximumProfiles} perfis: exclua um antes de salvar outro.");
            foreach (var profile in profiles)
            {
                var name = profile.Name?.Trim() ?? "";
                if (name.Length == 0) problems.Add("Perfil sem nome.");
                else if (name.Length > MaximumNameLength) problems.Add($"Nome de perfil com mais de {MaximumNameLength} letras: “{name}”.");
            }

            foreach (var repeated in profiles.GroupBy(p => (p.Name ?? "").Trim(), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1 && g.Key.Length > 0))
                problems.Add($"Perfil repetido: “{repeated.Key}”.");
            return problems;
        }
    }
}
