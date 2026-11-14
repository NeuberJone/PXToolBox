using System.Collections.Generic;
using Corel.Interop.VGCore;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBoxDev.Corel
{
    internal sealed class CorelShapeAdapter : ICorelShape
    {
        internal Shape Native { get; }

        public CorelShapeAdapter(Shape native)
        {
            Native = native;
        }

        public string Name
        {
            get => Native.Name ?? string.Empty;
            set => Native.Name = value;
        }

        public ShapeKind Kind => MapKind(Native.Type);

        public bool Visible
        {
            get => Native.Visible;
            set => Native.Visible = value;
        }

        public bool Locked
        {
            get => Native.Locked;
            set => Native.Locked = value;
        }

        public double Width
        {
            get => Native.SizeWidth;
            set => Native.SetSize(value, Native.SizeHeight);
        }

        public double Height
        {
            get => Native.SizeHeight;
            set => Native.SetSize(Native.SizeWidth, value);
        }

        public double PositionX
        {
            get => Native.PositionX;
            set => Native.Move(value - Native.PositionX, 0);
        }

        public double PositionY
        {
            get => Native.PositionY;
            set => Native.Move(0, value - Native.PositionY);
        }

        public double CenterX => Native.CenterX;

        public double CenterY => Native.CenterY;

        public bool HasProperty(string name, int id) => Native.Properties.Exists(name, id);

        public object? GetProperty(string name, int id) => Native.Properties[name, id];

        public void SetProperty(string name, int id, object value) => Native.Properties[name, id] = value;

        // CORRIGIDO (revisão): a rodada anterior trocou isto por Native.Properties.DeleteAll(name),
        // só porque a reflexão sobre Corel.Interop.VGCore.dll confirmou a ASSINATURA
        // `DeleteAll(String Name)` — mas assinatura não é o mesmo que comportamento. Nunca foi
        // verificado, em tempo de execução, se "Name" de fato FILTRA (remove só as entradas com
        // esse nome) ou se o parâmetro é ignorado/mal nomeado e a chamada apaga TODAS as
        // propriedades do Shape (alinhamento, objeto base, mão livre, esticar, qualquer outra
        // marcação SISBolt) — sem Corel real pra confirmar, essa suposição era arriscada demais
        // pra manter. Revertido pra exclusão seletiva por (nome, ID) via Delete(string, int)
        // (confirmado por reflexão com a MESMA ambiguidade de assinatura-vs-comportamento, mas
        // aqui o risco é mínimo: Delete recebe o ID exato já lido por Description, então só pode
        // atingir a entrada que a enumeração encontrou — nunca "todas"). Enumeração agora 1-based
        // (corrigido na mesma rodada que o inspector — ver ShapePropertiesInspectorService):
        // índice 0 sempre falha com "objeto não encontrado" nas inspeções reais já feitas, e o
        // laço 0..Count-1 desperdiçava essa tentativa sem nunca alcançar a última entrada real.
        public void DeleteProperties(string name)
        {
            var matches = new List<(string Name, int Id)>();
            var properties = Native.Properties;
            for (var index = 1; index <= properties.Count; index++)
            {
                var propertyName = string.Empty;
                var propertyId = 0;
                properties.Description(index, out propertyName, out propertyId);
                if (propertyName == name)
                {
                    matches.Add((propertyName, propertyId));
                }
            }

            foreach (var match in matches)
            {
                properties.Delete(match.Name, match.Id);
            }
        }

        public void DeleteProperty(string name, int id)
        {
            if (Native.Properties.Exists(name, id))
            {
                Native.Properties.Delete(name, id);
            }
        }

        public int StaticId => Native.StaticID;

        public bool IsInsideGroup => Native.ParentGroup != null;

        // No Corel 2025 (v27), Shape.Group() de um objeto só dá E_FAIL, e ShapeRange/Selection.Group() de um item
        // devolve o próprio objeto, sem grupo (conferido no Corel, 2026-11-11, smoke 4aw). Funciona: agrupar com um
        // auxiliar do mesmo tamanho e apagá-lo — fica um grupo com o original como único filho, no tamanho dele.
        public ICorelShape GroupAlone()
        {
            var helper = Native.Layer.CreateRectangle2(Native.LeftX, Native.BottomY, Native.SizeWidth, Native.SizeHeight);
            Shape group;
            try
            {
                var range = Native.Application.CreateShapeRange();
                range.Add(Native);
                range.Add(helper);
                group = GroupOnTheirLayer(range);
            }
            finally
            {
                helper.Delete();
            }

            return new CorelShapeAdapter(group);
        }

        // Quando os objetos fazem parte da seleção atual, o Corel 2025 cria o grupo na camada do último objeto
        // selecionado, não na dos objetos agrupados (conferido no Corel, 2026-11-11, smoke 4ax: o bloco do Tactel foi
        // parar na camada Dry fit). Devolve o grupo à camada dos objetos.
        private static void KeepOnLayer(Shape group, Layer layer)
        {
            if (!SameLayer(group.Layer, layer)) group.MoveToLayer(layer);
        }

        private static bool SameLayer(Layer a, Layer b) => a.Index == b.Index && a.Page.Index == b.Page.Index;

        /// <summary>ShapeRange.Group() que mantém o grupo na camada dos objetos, se estiverem todos numa só.</summary>
        internal static Shape GroupOnTheirLayer(ShapeRange range)
        {
            var layer = range[1].Layer;
            var oneLayer = true;
            for (var i = 2; i <= range.Count && oneLayer; i++) oneLayer = SameLayer(range[i].Layer, layer);
            var group = range.Group();
            if (oneLayer) KeepOnLayer(group, layer);
            return group;
        }

        public void OrderToFront() => Native.OrderToFront();

        public void SelectAlone() => Native.CreateSelection();

        public IReadOnlyList<ICorelShape> Shapes => CorelCollections.ToShapeList(Native.Shapes);

        // Shape.PowerClip (IVGShape) e PowerClip.Shapes (IVGPowerClip) são uma API própria,
        // separada de Shape.Shapes — confirmado por reflexão sobre Corel.Interop.VGCore.dll
        // (ver ICorelShape.HasPowerClipContents). Native.Shapes NUNCA inclui o conteúdo
        // recortado de um PowerClip.
        public bool HasPowerClipContents => Native.PowerClip != null;

        public IReadOnlyList<ICorelShape> PowerClipShapes =>
            Native.PowerClip != null ? CorelCollections.ToShapeList(Native.PowerClip.Shapes) : System.Array.Empty<ICorelShape>();

        public void Delete() => Native.Delete();

        public ICorelLayer? Layer => Native.Layer == null ? null : new CorelLayerAdapter(Native.Layer);

        public void MoveToLayer(ICorelLayer layer) => Native.MoveToLayer(((CorelLayerAdapter)layer).Native);

        public void ApplyStyleString(string style) => Native.Style.StringAssign(style);

        // ---- Resetar ângulo: mesmas chamadas VGCore do "Zerar ângulo" do MoldMotor ----

        public double RotationAngle => Native.RotationAngle;

        public void ClearRotation() => Native.RotationAngle = 0;

        public CurveGeometry? ReadCurveGeometry()
        {
            if (Native.Type != cdrShapeType.cdrCurveShape) return null;
            var curve = Native.Curve;
            if (curve == null) return null;

            var nodes = new List<CurvePoint>();
            var nativeNodes = curve.Nodes;
            for (var i = 1; i <= nativeNodes.Count; i++)
            {
                nativeNodes[i].GetPosition(out var x, out var y);
                nodes.Add(new CurvePoint(x, y));
            }

            var segments = new List<CurveSegment>();
            var nativeSegments = curve.Segments;
            for (var i = 1; i <= nativeSegments.Count; i++)
            {
                var segment = nativeSegments[i];
                switch (segment.Type)
                {
                    case cdrSegmentType.cdrLineSegment:
                        segments.Add(CurveSegment.Line());
                        break;
                    case cdrSegmentType.cdrCurveSegment:
                        segments.Add(CurveSegment.Curve(
                            new CurvePoint(segment.StartingControlPointX, segment.StartingControlPointY),
                            new CurvePoint(segment.EndingControlPointX, segment.EndingControlPointY)));
                        break;
                    default:
                        // Tipo desconhecido: leitura inconclusiva, nunca "tratar como reto".
                        return null;
                }
            }

            return new CurveGeometry(curve.SubPaths.Count, nodes, segments);
        }

        public int WriteCurveGeometry(CurveGeometry geometry)
        {
            var failures = 0;
            var curve = Native.Curve;
            var nativeNodes = curve.Nodes;
            for (var i = 1; i <= nativeNodes.Count && i <= geometry.Nodes.Count; i++)
            {
                try
                {
                    nativeNodes[i].SetPosition(geometry.Nodes[i - 1].X, geometry.Nodes[i - 1].Y);
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    failures++;
                }
            }

            var nativeSegments = curve.Segments;
            for (var i = 1; i <= nativeSegments.Count && i <= geometry.Segments.Count; i++)
            {
                var saved = geometry.Segments[i - 1];
                if (!saved.IsCurve) continue;
                try
                {
                    var segment = nativeSegments[i];
                    segment.StartingControlPointX = saved.StartControl.X;
                    segment.StartingControlPointY = saved.StartControl.Y;
                    segment.EndingControlPointX = saved.EndControl.X;
                    segment.EndingControlPointY = saved.EndControl.Y;
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    failures++;
                }
            }

            return failures;
        }

        public bool IsArtisticText => Native.Type == cdrShapeType.cdrTextShape && Native.Text.IsArtisticText;

        public void ConvertToCurves() => Native.ConvertToCurves();

        public ICorelShape Duplicate() => new CorelShapeAdapter(Native.Duplicate(0, 0));

        public IReadOnlyList<ShapePropertyEntry> ReadAllProperties()
        {
            var entries = new List<ShapePropertyEntry>();
            var properties = Native.Properties;
            // 1-based, como em DeleteProperties.
            for (var index = 1; index <= properties.Count; index++)
            {
                properties.Description(index, out var name, out var id);
                entries.Add(new ShapePropertyEntry(name, id, properties[name, id]));
            }

            return entries;
        }

        public bool IsInFrontOf(ICorelShape other) => Native.OrderIsInFrontOf(((CorelShapeAdapter)other).Native);

        public void OrderFrontOf(ICorelShape other) => Native.OrderFrontOf(((CorelShapeAdapter)other).Native);

        public void OrderToBack() => Native.OrderToBack();

        public IReadOnlyList<ICorelShape> Ungroup() => CorelCollections.ToShapeList(Native.UngroupEx());

        /// <summary>Outline.Width é na unidade do documento (o MoldMotor converte do mesmo jeito).
        /// OutlineStyles[0] é o contínuo garantido pela API — a cópia de uma peça tracejada sai
        /// contínua.</summary>
        public void SetOutline(ShapeOutline outline)
        {
            var app = Native.Application;
            var outlineNative = Native.Outline;
            outlineNative.Type = cdrOutlineType.cdrOutline;
            outlineNative.Width = app.ConvertUnits(outline.WidthMillimeters, cdrUnit.cdrMillimeter, app.ActiveDocument.Unit);
            outlineNative.Color.RGBAssign(outline.Red, outline.Green, outline.Blue);
            if (outline.Dash == ShapeOutlineDash.Solid)
            {
                outlineNative.Style = app.OutlineStyles[0];
            }
            else
            {
                // Estilo novo a cada aplicação (os itens 1+ de OutlineStyles são do usuário): um par
                // traço/vão em múltiplos da espessura, como o MoldMotor.
                object[] lengths = outline.Dash == ShapeOutlineDash.Dashed ? new object[] { 8.0, 4.0 } : new object[] { 1.0, 3.0 };
                outlineNative.Style = app.CreateOutlineStyle(1, ref lengths);
            }

            outlineNative.Justification = outline.Inside
                ? cdrOutlineJustification.cdrOutlineJustificationInside
                : cdrOutlineJustification.cdrOutlineJustificationMiddle;
            outlineNative.LineJoin = outline.RoundCorners ? cdrOutlineLineJoin.cdrOutlineRoundLineJoin : cdrOutlineLineJoin.cdrOutlineMiterLineJoin;
            outlineNative.LineCaps = outline.RoundCorners ? cdrOutlineLineCaps.cdrOutlineRoundLineCaps : cdrOutlineLineCaps.cdrOutlineButtLineCaps;
            outlineNative.BehindFill = outline.BehindFill;
            outlineNative.ScaleWithShape = outline.ScaleWithShape;
        }

        public void SetUniformFill(int red, int green, int blue) =>
            Native.Fill.ApplyUniformFill(Native.Application.CreateRGBColor(red, green, blue));

        public void FlipHorizontally() => Native.Flip(cdrFlipAxes.cdrFlipHorizontal);

        public void PlaceInsidePowerClip(ICorelShape container) =>
            Native.AddToPowerClip(((CorelShapeAdapter)container).Native, cdrTriState.cdrFalse);

        public void RemoveOutline() => Native.Outline.SetNoOutline();

        public void Rotate(double degrees) => Native.Rotate(degrees);

        public IReadOnlyList<OutlineElement>? ReadOutline()
        {
            // Uma chamada COM para o contorno inteiro. Assinaturas conferidas por reflexão
            // (IVGShape.DisplayCurve, IVGCurve.GetCurveInfo : CurveElement[]); o comportamento real
            // (null para grupo/texto/bitmap?) só no smoke test — qualquer falha vira "sem contorno".
            try
            {
                var curve = Native.DisplayCurve;
                if (curve == null) return null;
                var elements = new List<OutlineElement>();
                foreach (var element in curve.GetCurveInfo())
                {
                    var kind = element.ElementType switch
                    {
                        cdrCurveElementType.cdrElementStart => OutlineElementKind.Start,
                        cdrCurveElementType.cdrElementLine => OutlineElementKind.Line,
                        cdrCurveElementType.cdrElementControl => OutlineElementKind.Control,
                        cdrCurveElementType.cdrElementCurve => OutlineElementKind.Curve,
                        _ => (OutlineElementKind?)null,
                    };
                    if (kind == null) return null;
                    elements.Add(new OutlineElement(kind.Value, element.PositionX, element.PositionY));
                }

                return elements;
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// cdrShapeType (CorelDRAW 2026 / VGCore v27) não tem mais um valor distinto para
        /// PowerClip — por isso ShapeKind.PowerClip nunca é retornado aqui. Validar no smoke
        /// test real se PowerClips aparecem como cdrGroupShape ou outro tipo.
        /// </summary>
        internal static ShapeKind MapKind(cdrShapeType type) => type switch
        {
            cdrShapeType.cdrRectangleShape => ShapeKind.Rectangle,
            cdrShapeType.cdrEllipseShape => ShapeKind.Ellipse,
            cdrShapeType.cdrCurveShape => ShapeKind.Curve,
            cdrShapeType.cdrTextShape => ShapeKind.Text,
            cdrShapeType.cdrGroupShape => ShapeKind.Group,
            cdrShapeType.cdrBitmapShape => ShapeKind.Bitmap,
            cdrShapeType.cdrPolygonShape => ShapeKind.Polygon,
            cdrShapeType.cdrBlendGroupShape => ShapeKind.Blend,
            cdrShapeType.cdrSymbolShape => ShapeKind.Symbol,
            _ => ShapeKind.Other
        };
    }
}
