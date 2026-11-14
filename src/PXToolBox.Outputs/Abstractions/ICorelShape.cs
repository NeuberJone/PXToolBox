using System.Collections.Generic;

namespace PXToolBox.Outputs.Abstractions
{
    /// <summary>
    /// Abstração de um "shape" do CorelDRAW, isolando a lógica de Saídas do COM/VGCore.
    /// A implementação real (CorelHost) traduz cada membro para chamadas de
    /// Corel.Interop.VGCore.Shape; os testes usam uma implementação em memória (fake).
    /// </summary>
    public interface ICorelShape
    {
        string Name { get; set; }

        ShapeKind Kind { get; }

        /// <summary>Visibilidade do próprio objeto (independe da visibilidade da camada).</summary>
        bool Visible { get; set; }

        bool Locked { get; set; }

        double Width { get; set; }

        double Height { get; set; }

        double PositionX { get; set; }

        double PositionY { get; set; }

        /// <summary>Centro geométrico — ao contrário de PositionX/Y, não depende do ReferencePoint do documento.</summary>
        double CenterX { get; }

        double CenterY { get; }

        /// <summary>Consulta uma propriedade persistente do Shape pelo nome e ID.</summary>
        bool HasProperty(string name, int id);

        /// <summary>Lê o valor bruto de uma propriedade persistente do Shape.</summary>
        object? GetProperty(string name, int id);

        /// <summary>Cria ou atualiza uma propriedade persistente do Shape.</summary>
        void SetProperty(string name, int id, object value);

        /// <summary>Remove todas as entradas cujo nome seja exatamente o informado.</summary>
        void DeleteProperties(string name);

        /// <summary>Remove só a entrada (nome, ID) — as outras com o mesmo nome ficam
        /// (Properties.Delete(Name, ID) no VGCore). Nada acontece se ela não existir.</summary>
        void DeleteProperty(string name, int id);

        /// <summary>Shape.StaticID: identificador do objeto no documento (não muda ao agrupar,
        /// mover ou renomear; objeto novo ganha um novo).</summary>
        int StaticId { get; }

        /// <summary>True se o objeto está dentro de um grupo (Shape.ParentGroup != null).</summary>
        bool IsInsideGroup { get; }

        /// <summary>Agrupa ESTE objeto sozinho e devolve o grupo criado — o objeto original vira o
        /// único filho dele. (Shape.Group() de um item falha no Corel 2025; o adaptador agrupa com um
        /// auxiliar e apaga o auxiliar.)</summary>
        ICorelShape GroupAlone();

        /// <summary>Traz para a frente na camada (Shape.OrderToFront).</summary>
        void OrderToFront();

        /// <summary>Torna este objeto a seleção atual, sozinho (Shape.CreateSelection).</summary>
        void SelectAlone();

        /// <summary>
        /// Aplica uma string de estilo opaca (equivalente a Shape.Style.StringAssign no VGCore).
        /// Usada só por GridPreparationService para portar Utilidades.AplicaContornoGrid — não
        /// é uma API de estilo de propósito geral.
        /// </summary>
        void ApplyStyleString(string style);

        /// <summary>
        /// Filhos diretos de um GRUPO (Shape.Shapes no VGCore real). Vazio para formas simples —
        /// e, confirmado por inspeção real do assembly Corel.Interop.VGCore (reflexão sobre
        /// IVGShape/IVGPowerClip, ver CorelShapeAdapter), também vazio para o conteúdo recortado
        /// de um PowerClip: essa é uma coleção DIFERENTE (<see cref="PowerClipShapes"/>), nunca
        /// incluída aqui. Uma versão anterior deste comentário dizia "grupo ou PowerClip" — essa
        /// suposição nunca foi confirmada e, pela API real, está provavelmente errada; corrigida
        /// nesta rodada junto com a correção de CodeToggleService.ExpandNoAndNuCodesRecursively,
        /// que não descia em PowerClips por causa exatamente dessa suposição.
        /// </summary>
        IReadOnlyList<ICorelShape> Shapes { get; }

        /// <summary>
        /// True quando este Shape é o contêiner de um PowerClip (Shape.PowerClip != null no
        /// VGCore real). A superfície da API (nomes/tipos reais de Shape.PowerClip/
        /// PowerClip.Shapes) foi confirmada por reflexão sobre o assembly
        /// Corel.Interop.VGCore.dll instalado nesta máquina — mas o comportamento em tempo de
        /// execução (se PowerClip retorna null ou lança para um Shape comum, por exemplo) ainda
        /// não foi confirmado dentro do CorelDRAW real.
        /// </summary>
        bool HasPowerClipContents { get; }

        /// <summary>
        /// Conteúdo recortado por este PowerClip (Shape.PowerClip.Shapes no VGCore real) —
        /// coleção DIFERENTE de <see cref="Shapes"/> (filhos de grupo). Vazio quando
        /// <see cref="HasPowerClipContents"/> é false.
        /// </summary>
        IReadOnlyList<ICorelShape> PowerClipShapes { get; }

        /// <summary>Camada que contém este objeto (Shape.Layer no VGCore real, confirmado por
        /// reflexão sobre Corel.Interop.VGCore). Null se o Corel não informar nenhuma.</summary>
        ICorelLayer? Layer { get; }

        void Delete();

        void MoveToLayer(ICorelLayer layer);

        // ---- Resetar ângulo (RotationResetService) ----

        /// <summary>Shape.RotationAngle, em graus.</summary>
        double RotationAngle { get; }

        /// <summary>Shape.RotationAngle = 0. Sozinho, gira o objeto de volta — quem preserva a
        /// aparência é <see cref="WriteCurveGeometry"/> logo depois (ver RotationResetService).</summary>
        void ClearRotation();

        /// <summary>Nós e segmentos da curva (Shape.Curve), em coordenadas de página. Null quando
        /// não é curva ou alguma leitura é inconclusiva (segmento de tipo desconhecido) — nunca
        /// uma geometria parcial.</summary>
        CurveGeometry? ReadCurveGeometry();

        /// <summary>Regrava nós e, só nos segmentos curvos, pontos de controle, no mesmo índice
        /// lido. Devolve quantas escritas falharam (0 = todas).</summary>
        int WriteCurveGeometry(CurveGeometry geometry);

        /// <summary>Texto artístico (Shape.Text.IsArtisticText); false para qualquer outro tipo.</summary>
        bool IsArtisticText { get; }

        /// <summary>Shape.ConvertToCurves: o mesmo objeto passa a ser curva.</summary>
        void ConvertToCurves();

        /// <summary>Cópia no mesmo lugar (Shape.Duplicate(0, 0)), logo à frente do original.</summary>
        ICorelShape Duplicate();

        /// <summary>Todas as entradas de Shape.Properties.</summary>
        IReadOnlyList<ShapePropertyEntry> ReadAllProperties();

        /// <summary>Shape.OrderIsInFrontOf.</summary>
        bool IsInFrontOf(ICorelShape other);

        /// <summary>Shape.OrderFrontOf: logo à frente de <paramref name="other"/>.</summary>
        void OrderFrontOf(ICorelShape other);

        /// <summary>Shape.OrderToBack.</summary>
        void OrderToBack();

        /// <summary>Desfaz o grupo (Shape.UngroupEx) e devolve os objetos que eram filhos.</summary>
        IReadOnlyList<ICorelShape> Ungroup();

        // ---- Definir objetos (MoldDefineService) ----

        /// <summary>Shape.Outline: liga o contorno e aplica cor, espessura, cantos e estilo contínuo.</summary>
        void SetOutline(ShapeOutline outline);

        /// <summary>Shape.Fill.ApplyUniformFill com a cor RGB.</summary>
        void SetUniformFill(int red, int green, int blue);

        // ---- Duplicar peça (MoldDuplicationService) ----

        /// <summary>Shape.Flip(cdrFlipHorizontal): espelho horizontal (o centro pode se deslocar).</summary>
        void FlipHorizontally();

        // ---- Dividir em partes (SliceCutService) ----

        /// <summary>Shape.AddToPowerClip(container, cdrFalse): este objeto vai para dentro do PowerClip
        /// de <paramref name="container"/> sem ser centralizado (fica onde estava).</summary>
        void PlaceInsidePowerClip(ICorelShape container);

        /// <summary>Shape.Outline.SetNoOutline.</summary>
        void RemoveOutline();

        // ---- Encaixe retangular (NestingService) ----

        /// <summary>Shape.Rotate(graus): gira em volta do centro de rotação (o centro, salvo se o usuário
        /// o moveu); positivo = anti-horário. Largura/altura passam a ser as da caixa girada.</summary>
        void Rotate(double degrees);

        /// <summary>Contorno como o Corel desenha o objeto (Shape.DisplayCurve.GetCurveInfo: serve para
        /// curva, retângulo, elipse, polígono e contêiner de PowerClip). Null se o Corel não der um.</summary>
        IReadOnlyList<OutlineElement>? ReadOutline();
    }
}
