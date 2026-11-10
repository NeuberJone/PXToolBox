using System;
using System.Collections.Generic;
using System.Linq;

namespace PXToolBox.Core.Molds
{
    /// <summary>Uma categoria de tamanhos: código da camada (MA, FE, C) e os tamanhos gerados por
    /// prefixo + tamanho base + sufixo (sem prefixo ou sufixo, vale o vazio). Ex.: base PP…XXGG com
    /// prefixo BL → BLPP…BLXXGG.</summary>
    public sealed class MoldSizeCategory
    {
        public MoldSizeCategory(string code, IEnumerable<string> baseSizes, IEnumerable<string>? prefixes = null, IEnumerable<string>? suffixes = null)
        {
            Code = (code ?? string.Empty).Trim().ToUpperInvariant();
            BaseSizes = MoldSettings.NormalizeList(baseSizes);
            Prefixes = MoldSettings.NormalizeList(prefixes);
            Suffixes = MoldSettings.NormalizeList(suffixes);
            Sizes = Combine(BaseSizes);
        }

        public string Code { get; }

        public IReadOnlyList<string> BaseSizes { get; }

        public IReadOnlyList<string> Prefixes { get; }

        public IReadOnlyList<string> Suffixes { get; }

        /// <summary>Tamanhos válidos, na ordem prefixo → base → sufixo, sem repetição.</summary>
        public IReadOnlyList<string> Sizes { get; }

        /// <summary>As grafias de um tamanho base nesta categoria (ex.: G → BLG).</summary>
        public IReadOnlyList<string> Combine(IReadOnlyList<string> bases)
        {
            var prefixes = Prefixes.Count == 0 ? new[] { string.Empty } : Prefixes.ToArray();
            var suffixes = Suffixes.Count == 0 ? new[] { string.Empty } : Suffixes.ToArray();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();
            foreach (var prefix in prefixes)
            foreach (var size in bases)
            foreach (var suffix in suffixes)
            {
                var combined = prefix + size + suffix;
                if (seen.Add(combined)) result.Add(combined);
            }

            return result;
        }

        public bool Contains(string size) => Sizes.Contains((size ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Configurações do Cadastro de moldes, PRÓPRIAS do PX (nunca o arquivo do MoldMotor —
    /// AGENTS.md). Começam pelos valores de fábrica (<see cref="Default"/>) e são editadas em
    /// "Configurações de moldes…". Sem dependência de JSON ou de Corel: o host grava/lê.
    /// </summary>
    public sealed class MoldSettings
    {
        public MoldSettings(MoldSizeCategory male, MoldSizeCategory female, MoldSizeCategory kids, string gridBaseSize,
            IEnumerable<string> laserNames, string tagName, string identifierName, MoldDefineOptions? define = null,
            MoldInnerOutlineOptions? innerOutline = null)
        {
            Define = define ?? MoldDefineOptions.Default;
            InnerOutline = innerOutline ?? MoldInnerOutlineOptions.Default;
            Male = male;
            Female = female;
            Kids = kids;
            GridBaseSize = (gridBaseSize ?? string.Empty).Trim().ToUpperInvariant();
            LaserNames = (laserNames ?? Array.Empty<string>()).Select(n => (n ?? string.Empty).Trim()).Where(n => n.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            TagName = (tagName ?? string.Empty).Trim();
            IdentifierName = (identifierName ?? string.Empty).Trim();
        }

        public MoldSizeCategory Male { get; }

        public MoldSizeCategory Female { get; }

        public MoldSizeCategory Kids { get; }

        public IReadOnlyList<MoldSizeCategory> Categories => new[] { Male, Female, Kids };

        /// <summary>Tamanho base copiado para o Grid no masculino e no feminino (com o prefixo e
        /// sufixo da categoria: G → BLG). No infantil vai o maior tamanho.</summary>
        public string GridBaseSize { get; }

        /// <summary>Nomes do objeto de corte a laser dentro do grupo de tamanho (o primeiro é o usado).</summary>
        public IReadOnlyList<string> LaserNames { get; }

        public string TagName { get; }

        /// <summary>Objeto que identifica o tamanho dentro do grupo (opcional; vazio = nenhum).</summary>
        public string IdentifierName { get; }

        /// <summary>O que "Definir objetos" cria: cópia do laser e TAG.</summary>
        public MoldDefineOptions Define { get; }

        /// <summary>Contorno interno de identificação do tamanho (desligado na fábrica).</summary>
        public MoldInnerOutlineOptions InnerOutline { get; }

        /// <summary>Categoria (MA, FE, C) do tamanho; null se desconhecido.</summary>
        public MoldSizeCategory? CategoryFor(string size) => Categories.FirstOrDefault(c => c.Contains(size));

        /// <summary>Cor do contorno interno do tamanho: a cadastrada, senão a sugerida pela posição dele
        /// na categoria (<see cref="MoldInnerOutlineOptions.Palette"/>). Vazio se o tamanho não existe.</summary>
        public string InnerOutlineColorOf(string size)
        {
            var key = (size ?? string.Empty).Trim().ToUpperInvariant();
            if (InnerOutline.SizeColors.TryGetValue(key, out var color)) return color;
            var category = CategoryFor(key);
            if (category == null) return string.Empty;
            var index = category.Sizes.ToList().FindIndex(s => string.Equals(s, key, StringComparison.OrdinalIgnoreCase));
            return MoldInnerOutlineOptions.Palette[index % MoldInnerOutlineOptions.Palette.Count];
        }

        /// <summary>Estilo de linha do contorno interno da categoria do tamanho.</summary>
        public MoldLineStyle InnerOutlineStyleOf(string size)
        {
            var category = CategoryFor(size);
            return ReferenceEquals(category, Female) ? InnerOutline.FemaleStyle
                : ReferenceEquals(category, Kids) ? InnerOutline.KidsStyle
                : InnerOutline.MaleStyle;
        }

        /// <summary>Todos os tamanhos das três categorias, na ordem masculino → feminino → infantil.</summary>
        public IReadOnlyList<string> AllSizes => Categories.SelectMany(c => c.Sizes).ToList();

        /// <summary>Valores de fábrica — os mesmos tamanhos do MoldMotor; laser "x" (padrão do usuário).</summary>
        public static MoldSettings Default { get; } = new MoldSettings(
            new MoldSizeCategory("MA", new[] { "PP", "P", "M", "G", "GG", "XGG", "XXGG" }),
            new MoldSizeCategory("FE", new[] { "PP", "P", "M", "G", "GG", "XGG", "XXGG" }, new[] { "BL" }),
            new MoldSizeCategory("C", new[] { "2", "4", "6", "8", "10", "12" }, null, new[] { "A" }),
            "G", new[] { "x", "LASER" }, "TAG", "IDENTIFICADOR_TAMANHO");

        /// <summary>Código (MA, FE, C) da categoria do tamanho; vazio se desconhecido.</summary>
        public string CategoryOf(string size) => Categories.FirstOrDefault(c => c.Contains(size))?.Code ?? string.Empty;

        /// <summary>Código canônico para a grafia vinda de um nome de camada; vazio se não for categoria.</summary>
        public string CanonicalCategory(string category) =>
            Categories.FirstOrDefault(c => string.Equals(c.Code, (category ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))?.Code ?? string.Empty;

        /// <summary>Tamanhos aceitos como "o do Grid" na categoria: GridBaseSize com o prefixo/sufixo dela
        /// (masculino/feminino) ou o maior infantil. Vazio se não houver.</summary>
        public IReadOnlyList<string> GridSizesOf(string categoryCode)
        {
            var category = Categories.FirstOrDefault(c => string.Equals(c.Code, categoryCode, StringComparison.OrdinalIgnoreCase));
            if (category == null) return Array.Empty<string>();
            if (ReferenceEquals(category, Kids)) return Kids.Sizes.Count == 0 ? Array.Empty<string>() : new[] { Kids.Sizes[Kids.Sizes.Count - 1] };
            return GridBaseSize.Length == 0 ? Array.Empty<string>() : category.Combine(new[] { GridBaseSize });
        }

        /// <summary>Problemas que impedem salvar (vazio = válido).</summary>
        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();
            foreach (var (category, label) in new[] { (Male, "Masculino"), (Female, "Feminino"), (Kids, "Infantil") })
            {
                if (category.Code.Length == 0) problems.Add($"{label}: informe o código da categoria.");
                else if (category.Code.Contains(".") || category.Code.Contains(" ")) problems.Add($"{label}: o código não pode ter ponto nem espaço.");
                if (category.BaseSizes.Count == 0) problems.Add($"{label}: informe pelo menos um tamanho base.");
            }

            if (Categories.Select(c => c.Code).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Count() < 3)
            {
                problems.Add("Os três códigos de categoria precisam ser diferentes.");
            }

            var repeated = Categories.SelectMany(c => c.Sizes.Select(s => (Size: s, c.Code)))
                .GroupBy(p => p.Size, StringComparer.OrdinalIgnoreCase).Where(g => g.Select(p => p.Code).Distinct().Count() > 1)
                .Select(g => g.Key).ToList();
            if (repeated.Count > 0) problems.Add($"Tamanho em mais de uma categoria: {string.Join(", ", repeated)}.");

            if (LaserNames.Count == 0) problems.Add("Informe pelo menos um nome para o objeto do laser.");
            if (TagName.Length == 0) problems.Add("Informe o nome da TAG.");
            if (GridBaseSize.Length == 0) problems.Add("Informe o tamanho base do Grid.");
            problems.AddRange(Define.Validate());
            problems.AddRange(InnerOutline.Validate());
            if (InnerOutline.Enabled && !Define.CreateLaserCopy)
            {
                problems.Add("Contorno interno: ligue \"Criar a cópia do laser\" — a peça recebe o contorno interno e a cópia, o do laser.");
            }

            var unknown = InnerOutline.SizeColors.Keys.Where(s => CategoryOf(s).Length == 0).ToList();
            if (unknown.Count > 0) problems.Add($"Cor para tamanho que não está nas categorias: {string.Join(", ", unknown)}.");
            return problems;
        }

        /// <summary>Lista digitada (vírgula, ponto e vírgula, espaço ou linha): sem vazios, em
        /// maiúsculas, sem repetição, na ordem digitada.</summary>
        public static IReadOnlyList<string> NormalizeList(IEnumerable<string>? values) =>
            (values ?? Array.Empty<string>())
                .SelectMany(v => (v ?? string.Empty).Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(v => v.Trim().ToUpperInvariant()).Where(v => v.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Onde a TAG fica em relação à caixa da peça (as nove posições do MoldMotor).</summary>
    public enum MoldTagPosition
    {
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        Center,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight,
    }

    /// <summary>
    /// Centro da TAG em relação à caixa da peça — a regra do MoldMotor. Coordenadas do Corel (Y cresce
    /// para cima), tudo na mesma unidade. Esquerda: borda esquerda da TAG na esquerda da peça + margem
    /// esquerda; direita: borda direita na direita da peça − margem direita; topo/base: idem com as
    /// margens superior/inferior; centro: centro da peça deslocado por (esquerda − direita) e
    /// (inferior − superior).
    /// </summary>
    public static class MoldTagPlacement
    {
        public static IReadOnlyList<(MoldTagPosition Position, string Label)> Labels { get; } = new[]
        {
            (MoldTagPosition.TopLeft, "Superior esquerda"),
            (MoldTagPosition.TopCenter, "Superior centro"),
            (MoldTagPosition.TopRight, "Superior direita"),
            (MoldTagPosition.MiddleLeft, "Centro esquerda"),
            (MoldTagPosition.Center, "Centro"),
            (MoldTagPosition.MiddleRight, "Centro direita"),
            (MoldTagPosition.BottomLeft, "Inferior esquerda"),
            (MoldTagPosition.BottomCenter, "Inferior centro"),
            (MoldTagPosition.BottomRight, "Inferior direita"),
        };

        public static string Label(MoldTagPosition position) => Labels.First(l => l.Position == position).Label;

        public static (double X, double Y) ResolveCenter(MoldTagPosition position,
            double pieceLeft, double pieceBottom, double pieceWidth, double pieceHeight, double tagWidth, double tagHeight,
            double leftMargin, double rightMargin, double topMargin, double bottomMargin)
        {
            var left = pieceLeft + leftMargin + tagWidth / 2;
            var right = pieceLeft + pieceWidth - rightMargin - tagWidth / 2;
            var centerX = pieceLeft + pieceWidth / 2 + (leftMargin - rightMargin);
            var top = pieceBottom + pieceHeight - topMargin - tagHeight / 2;
            var bottom = pieceBottom + bottomMargin + tagHeight / 2;
            var centerY = pieceBottom + pieceHeight / 2 + (bottomMargin - topMargin);
            switch (position)
            {
                case MoldTagPosition.TopLeft: return (left, top);
                case MoldTagPosition.TopCenter: return (centerX, top);
                case MoldTagPosition.TopRight: return (right, top);
                case MoldTagPosition.MiddleLeft: return (left, centerY);
                case MoldTagPosition.Center: return (centerX, centerY);
                case MoldTagPosition.MiddleRight: return (right, centerY);
                case MoldTagPosition.BottomLeft: return (left, bottom);
                case MoldTagPosition.BottomRight: return (right, bottom);
                default: return (centerX, bottom);
            }
        }
    }

    /// <summary>
    /// O que "Definir objetos" cria no grupo de tamanho. Fábrica (os padrões do MoldMotor): cópia do
    /// laser com contorno vermelho de 0,2 mm, cantos vivos; TAG com o tamanho em Arial Black 50 pt,
    /// preta com contorno branco de 2 mm, embaixo e no centro da peça, sem margens.
    /// </summary>
    public sealed class MoldDefineOptions
    {
        public const double MaxTagMarginCentimeters = 100;

        public MoldDefineOptions(bool createLaserCopy, string laserColor, double laserWidthMillimeters,
            bool createTag, double tagFontSizePoints, double tagOutlineWidthMillimeters,
            MoldTagPosition tagPosition = MoldTagPosition.BottomCenter,
            double tagLeftMarginCm = 0, double tagRightMarginCm = 0, double tagTopMarginCm = 0, double tagBottomMarginCm = 0)
        {
            TagPosition = tagPosition;
            TagLeftMarginCm = tagLeftMarginCm;
            TagRightMarginCm = tagRightMarginCm;
            TagTopMarginCm = tagTopMarginCm;
            TagBottomMarginCm = tagBottomMarginCm;
            CreateLaserCopy = createLaserCopy;
            LaserColor = (laserColor ?? string.Empty).Trim().ToUpperInvariant();
            if (LaserColor.Length > 0 && LaserColor[0] != '#') LaserColor = "#" + LaserColor;
            LaserWidthMillimeters = laserWidthMillimeters;
            CreateTag = createTag;
            TagFontSizePoints = tagFontSizePoints;
            TagOutlineWidthMillimeters = tagOutlineWidthMillimeters;
        }

        /// <summary>Liga: a peça fica como está e uma cópia exata dela, com o nome do laser, recebe o
        /// contorno do laser. Desliga: a própria peça recebe o contorno do laser.</summary>
        public bool CreateLaserCopy { get; }

        /// <summary>Cor do contorno do laser, "#RRGGBB".</summary>
        public string LaserColor { get; }

        public double LaserWidthMillimeters { get; }

        public bool CreateTag { get; }

        public double TagFontSizePoints { get; }

        /// <summary>Contorno branco do texto da TAG (0 = sem contorno).</summary>
        public double TagOutlineWidthMillimeters { get; }

        public MoldTagPosition TagPosition { get; }

        /// <summary>Margens da TAG em cm (como no MoldMotor) — ver <see cref="MoldTagPlacement"/>.</summary>
        public double TagLeftMarginCm { get; }

        public double TagRightMarginCm { get; }

        public double TagTopMarginCm { get; }

        public double TagBottomMarginCm { get; }

        public static MoldDefineOptions Default { get; } = new MoldDefineOptions(true, "#FF0000", 0.2, true, 50, 2);

        /// <summary>Componentes da cor do laser; false se o texto não for "#RRGGBB".</summary>
        public bool TryGetLaserRgb(out int red, out int green, out int blue) => TryParseRgb(LaserColor, out red, out green, out blue);

        /// <summary>"#RRGGBB" (o # é opcional) → componentes; false se não for esse formato.</summary>
        public static bool TryParseRgb(string? color, out int red, out int green, out int blue)
        {
            red = green = blue = 0;
            var hex = (color ?? string.Empty).Trim().TrimStart('#');
            if (hex.Length != 6 || !int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                return false;
            }

            red = (value >> 16) & 0xFF;
            green = (value >> 8) & 0xFF;
            blue = value & 0xFF;
            return true;
        }

        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();
            if (!TryGetLaserRgb(out _, out _, out _)) problems.Add("Cor do laser: use o formato #RRGGBB (ex.: #FF0000).");
            if (!(LaserWidthMillimeters > 0 && LaserWidthMillimeters <= 10)) problems.Add("Espessura do laser: entre 0 e 10 mm.");
            if (!(TagFontSizePoints > 0 && TagFontSizePoints <= 3000)) problems.Add("Fonte da TAG: entre 1 e 3000 pt.");
            if (!(TagOutlineWidthMillimeters >= 0 && TagOutlineWidthMillimeters <= 50)) problems.Add("Contorno da TAG: entre 0 e 50 mm.");
            if (!Enum.IsDefined(typeof(MoldTagPosition), TagPosition)) problems.Add("Posição da TAG desconhecida.");
            if (new[] { TagLeftMarginCm, TagRightMarginCm, TagTopMarginCm, TagBottomMarginCm }.Any(m => !(m >= 0 && m <= MaxTagMarginCentimeters)))
            {
                problems.Add($"Margens da TAG: entre 0 e {MaxTagMarginCentimeters:0} cm.");
            }
            return problems;
        }
    }

    public enum MoldLineStyle
    {
        Solid,
        Dashed,
        Dotted,
    }

    /// <summary>
    /// Contorno interno de identificação (o "contorno interno" do MoldMotor): com ele ligado, a peça do
    /// grupo de tamanho recebe um contorno por dentro com a cor do tamanho e o estilo de linha da
    /// categoria (fábrica: masculino contínuo, feminino tracejado, infantil pontilhado), e a cópia do
    /// laser continua com o contorno do laser. Cor de cada tamanho: a cadastrada em
    /// <see cref="SizeColors"/>, senão a sugerida pela posição na categoria (<see cref="Palette"/>, a
    /// mesma paleta do MoldMotor).
    /// </summary>
    public sealed class MoldInnerOutlineOptions
    {
        public MoldInnerOutlineOptions(bool enabled, double widthMillimeters, MoldLineStyle maleStyle = MoldLineStyle.Solid,
            MoldLineStyle femaleStyle = MoldLineStyle.Dashed, MoldLineStyle kidsStyle = MoldLineStyle.Dotted,
            IEnumerable<KeyValuePair<string, string>>? sizeColors = null)
        {
            Enabled = enabled;
            WidthMillimeters = widthMillimeters;
            MaleStyle = maleStyle;
            FemaleStyle = femaleStyle;
            KidsStyle = kidsStyle;
            var colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in sizeColors ?? Array.Empty<KeyValuePair<string, string>>())
            {
                var size = (entry.Key ?? string.Empty).Trim().ToUpperInvariant();
                if (size.Length == 0) continue;
                var color = (entry.Value ?? string.Empty).Trim().ToUpperInvariant();
                if (color.Length > 0 && color[0] != '#') color = "#" + color;
                colors[size] = color;
            }

            SizeColors = colors;
        }

        public bool Enabled { get; }

        public double WidthMillimeters { get; }

        public MoldLineStyle MaleStyle { get; }

        public MoldLineStyle FemaleStyle { get; }

        public MoldLineStyle KidsStyle { get; }

        /// <summary>Cores cadastradas, tamanho (maiúsculo) → "#RRGGBB". Tamanho fora daqui: cor sugerida.</summary>
        public IReadOnlyDictionary<string, string> SizeColors { get; }

        public static MoldInnerOutlineOptions Default { get; } = new MoldInnerOutlineOptions(false, 0.5);

        /// <summary>Cores sugeridas, pela posição do tamanho na categoria (paleta do MoldMotor).</summary>
        public static IReadOnlyList<string> Palette { get; } = new[]
        {
            "#E03C3C", "#E07A2C", "#E0C33C", "#6FC33C", "#2CA36B", "#12A0A8",
            "#2C7AE0", "#4B4BD0", "#8B4BD0", "#D04BA8", "#8B5A2C", "#000000",
        };

        public static IReadOnlyList<(MoldLineStyle Style, string Label)> StyleLabels { get; } = new[]
        {
            (MoldLineStyle.Solid, "Contínuo"),
            (MoldLineStyle.Dashed, "Tracejado"),
            (MoldLineStyle.Dotted, "Pontilhado"),
        };

        public static string Label(MoldLineStyle style) => StyleLabels.First(l => l.Style == style).Label;

        /// <summary>Texto digitado ("G #FF0000", "G=#FF0000" ou "G: FF0000", por linha, vírgula ou ponto
        /// e vírgula) → pares. Entrada sem cor fica com a cor vazia, para a validação apontar.</summary>
        public static IReadOnlyList<KeyValuePair<string, string>> ParseColors(string? text) =>
            (text ?? string.Empty).Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim()).Where(e => e.Length > 0)
                .Select(e =>
                {
                    var parts = e.Split(new[] { ' ', '\t', '=', ':' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    return new KeyValuePair<string, string>(parts[0], parts.Length > 1 ? parts[1].Trim(' ', '\t', '=', ':') : string.Empty);
                }).ToList();

        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();
            if (!(WidthMillimeters > 0 && WidthMillimeters <= 10)) problems.Add("Espessura do contorno interno: entre 0 e 10 mm.");
            if (new[] { MaleStyle, FemaleStyle, KidsStyle }.Any(s => !Enum.IsDefined(typeof(MoldLineStyle), s))) problems.Add("Estilo de linha do contorno interno desconhecido.");
            var invalid = SizeColors.Where(c => !MoldDefineOptions.TryParseRgb(c.Value, out _, out _, out _)).Select(c => c.Key).ToList();
            if (invalid.Count > 0) problems.Add($"Cor do contorno interno: use #RRGGBB ({string.Join(", ", invalid)}).");
            return problems;
        }
    }
}
