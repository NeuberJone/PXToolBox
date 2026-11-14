using System.Collections.Generic;
using System.Linq;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Tests.Fakes
{
    /// <summary>Implementação em memória de ICorelShape, usada para testar a lógica de Saídas sem COM.</summary>
    public sealed class FakeShape : ICorelShape
    {
        public string Name { get; set; } = string.Empty;

        public ShapeKind Kind { get; set; } = ShapeKind.Rectangle;

        public bool Visible { get; set; } = true;

        public bool Locked { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        // PositionX/Y e CenterX/Y são acoplados porque descrevem o MESMO shape: mover a posição
        // move o centro junto, igual no Corel real — necessário para que um teste com fakes
        // consiga detectar corretamente o efeito de GridPreparationService.ResizePageToFitAndCenter
        // (que translada via PositionX/Y mas mede a centralização via CenterX/Y).
        private double _positionX;
        public double PositionX
        {
            get => _positionX;
            set
            {
                var delta = value - _positionX;
                CenterX += delta;
                _positionX = value;
                // Grupo: os filhos vão junto, como no Corel.
                foreach (FakeShape child in ShapesList) child.PositionX += delta;
            }
        }

        private double _positionY;
        public double PositionY
        {
            get => _positionY;
            set
            {
                var delta = value - _positionY;
                CenterY += delta;
                _positionY = value;
                foreach (FakeShape child in ShapesList) child.PositionY += delta;
            }
        }

        public double CenterX { get; set; }

        public double CenterY { get; set; }

        private readonly Dictionary<(string Name, int Id), object?> _properties = new();

        /// <summary>Simula uma leitura de Properties que falha (ex.: Shape excluído/inválido)
        /// — usado só para testar que o chamador trata isso como Unknown, nunca como Absent.</summary>
        public bool ThrowOnPropertyAccess { get; set; }

        public bool HasProperty(string name, int id)
        {
            if (ThrowOnPropertyAccess) throw new System.InvalidOperationException("leitura simulada com falha");
            return _properties.ContainsKey((name, id));
        }

        public object? GetProperty(string name, int id) =>
            _properties.TryGetValue((name, id), out var value) ? value : null;

        /// <summary>Simula a escrita de um ID específico falhando (ex.: segunda propriedade
        /// de um par, pra testar que a primeira já escrita é revertida em vez de ficar como
        /// marcação parcial) — -1 (padrão) nunca corresponde a um ID real, então nunca falha
        /// sem ser pedido.</summary>
        public int ThrowOnSetPropertyId { get; set; } = -1;

        public void SetProperty(string name, int id, object value)
        {
            if (id == ThrowOnSetPropertyId) throw new System.InvalidOperationException("escrita simulada com falha");
            _properties[(name, id)] = value;
        }

        public void DeleteProperties(string name)
        {
            foreach (var key in _properties.Keys.Where(key => key.Name == name).ToList())
            {
                _properties.Remove(key);
            }
        }

        public void DeleteProperty(string name, int id) => _properties.Remove((name, id));

        private static int _nextStaticId = 1000;

        public int StaticId { get; set; } = System.Threading.Interlocked.Increment(ref _nextStaticId);

        /// <summary>Grupo que contém este objeto (simula Shape.ParentGroup).</summary>
        public FakeShape? ParentGroup { get; set; }

        public bool IsInsideGroup => ParentGroup != null;

        /// <summary>Simula Shape.Group() de um item só: grupo novo na mesma camada, com este
        /// objeto como único filho. <see cref="GroupAloneResult"/> permite simular um Corel que
        /// devolve outra estrutura.</summary>
        public System.Func<FakeShape, FakeShape>? GroupAloneResult { get; set; }

        public bool ThrowOnGroup { get; set; }

        public ICorelShape GroupAlone()
        {
            if (ThrowOnGroup) throw new System.InvalidOperationException("agrupar simulado com falha");
            if (GroupAloneResult != null) return GroupAloneResult(this);

            var group = new FakeShape { Kind = ShapeKind.Group, CurrentLayer = CurrentLayer };
            var index = CurrentLayer?.ShapesList.IndexOf(this) ?? -1;
            if (index >= 0) CurrentLayer!.ShapesList[index] = group;
            group.ShapesList.Add(this);
            ParentGroup = group;
            return group;
        }

        public int OrderToFrontCount { get; private set; }

        public void OrderToFront()
        {
            OrderToFrontCount++;
            var list = Container;
            if (list != null && list.Remove(this)) list.Insert(0, this);
        }

        /// <summary>Lista que contém este objeto: a do grupo pai, ou a da camada. Índice 0 = frente.</summary>
        public List<ICorelShape>? Container => ParentGroup?.ShapesList ?? CurrentLayer?.ShapesList;

        public bool SelectedAlone { get; private set; }

        public void SelectAlone() => SelectedAlone = true;

        public string? LastAppliedStyle { get; private set; }

        public void ApplyStyleString(string style) => LastAppliedStyle = style;

        public List<ICorelShape> ShapesList { get; } = new List<ICorelShape>();

        public IReadOnlyList<ICorelShape> Shapes => ShapesList;

        /// <summary>Simula Shape.PowerClip.Shapes — coleção separada de ShapesList/Shapes (ver
        /// ICorelShape.PowerClipShapes). Vazia nos testes que não envolvem PowerClip.</summary>
        public List<ICorelShape> PowerClipShapesList { get; } = new List<ICorelShape>();

        public bool HasPowerClipContents { get; set; }

        public IReadOnlyList<ICorelShape> PowerClipShapes => PowerClipShapesList;

        public bool IsDeleted { get; private set; }

        /// <summary>Camada atual (usada só pelo fake, para MoveToLayer funcionar sem precisar de um "dono" externo).</summary>
        public FakeLayer? CurrentLayer { get; set; }

        public ICorelLayer? Layer => CurrentLayer;

        public void Delete()
        {
            IsDeleted = true;
            if (Container == null || !Container.Remove(this)) CurrentLayer?.ShapesList.Remove(this);
        }

        public void MoveToLayer(ICorelLayer layer)
        {
            // Como no Corel: um objeto dentro de um grupo sai dele.
            if (ParentGroup != null)
            {
                ParentGroup.ShapesList.Remove(this);
                ParentGroup = null;
            }

            CurrentLayer?.ShapesList.Remove(this);
            var target = (FakeLayer)layer;
            target.ShapesList.Add(this);
            CurrentLayer = target;
        }

        // ---- Resetar ângulo ----
        // Modelo: a curva tem uma geometria (nós em coordenadas de página). Zerar a rotação, como no
        // Corel, muda a aparência (aqui: desloca os nós e o centro); regravar a geometria lida
        // antes devolve a aparência.

        public double RotationAngle { get; set; }

        public CurveGeometry? Geometry { get; set; }

        private const double ClearRotationShift = 5;

        public void ClearRotation()
        {
            RotationAngle = 0;
            if (Geometry != null)
            {
                Geometry = new CurveGeometry(Geometry.SubPathCount,
                    Geometry.Nodes.Select(n => new CurvePoint(n.X + ClearRotationShift, n.Y)).ToList(), Geometry.Segments);
            }

            CenterX += ClearRotationShift;
        }

        /// <summary>Número de escritas que falham em <see cref="WriteCurveGeometry"/> (0 = nenhuma).</summary>
        public int FailingGeometryWrites { get; set; }

        /// <summary>Simula uma leitura de curva inconclusiva (segmento de tipo desconhecido).</summary>
        public bool UnreadableCurve { get; set; }

        public CurveGeometry? ReadCurveGeometry() => Kind == ShapeKind.Curve && !UnreadableCurve ? Geometry : null;

        public int WriteCurveGeometry(CurveGeometry geometry)
        {
            if (FailingGeometryWrites > 0) return FailingGeometryWrites;
            if (Geometry != null && Geometry.Nodes.Count > 0 && geometry.Nodes.Count > 0)
            {
                CenterX -= Geometry.Nodes[0].X - geometry.Nodes[0].X;
            }

            Geometry = geometry;
            return 0;
        }

        public bool IsArtisticText { get; set; }

        /// <summary>Simula ConvertToCurves que não faz nada (relato de campo do MoldMotor).</summary>
        public bool ConvertToCurvesDoesNothing { get; set; }

        /// <summary>Simula ConvertToCurves que perde as Properties do objeto.</summary>
        public bool ConvertToCurvesDropsProperties { get; set; }

        public void ConvertToCurves()
        {
            if (ConvertToCurvesDoesNothing) return;
            Kind = ShapeKind.Curve;
            Geometry ??= Square(CenterX, CenterY);
            if (ConvertToCurvesDropsProperties) _properties.Clear();
        }

        public static CurveGeometry Square(double x, double y) => new CurveGeometry(1,
            new[] { new CurvePoint(x, y), new CurvePoint(x + 1, y), new CurvePoint(x + 1, y + 1), new CurvePoint(x, y + 1) },
            new[] { CurveSegment.Line(), CurveSegment.Curve(new CurvePoint(x + 2, y), new CurvePoint(x + 2, y + 1)), CurveSegment.Line(), CurveSegment.Line() });

        public List<FakeShape> Duplicates { get; } = new List<FakeShape>();

        public ICorelShape Duplicate()
        {
            var copy = new FakeShape
            {
                Name = Name, Kind = Kind, Width = Width, Height = Height, CenterX = CenterX, CenterY = CenterY,
                RotationAngle = RotationAngle, Geometry = Geometry, CurrentLayer = CurrentLayer, ParentGroup = ParentGroup,
            };
            foreach (var entry in _properties) copy._properties[entry.Key] = entry.Value;
            // Grupo: cópia dos filhos também, como no Corel.
            foreach (FakeShape child in ShapesList)
            {
                var childCopy = (FakeShape)child.CopyDetached();
                childCopy.ParentGroup = copy;
                childCopy.CurrentLayer = CurrentLayer;
                copy.ShapesList.Add(childCopy);
            }

            var list = Container;
            if (list != null) list.Insert(System.Math.Max(0, list.IndexOf(this)), copy);
            Duplicates.Add(copy);
            return copy;
        }

        private ICorelShape CopyDetached()
        {
            var copy = new FakeShape { Name = Name, Kind = Kind, Width = Width, Height = Height, CenterX = CenterX, CenterY = CenterY, RotationAngle = RotationAngle, Geometry = Geometry };
            foreach (var entry in _properties) copy._properties[entry.Key] = entry.Value;
            foreach (FakeShape child in ShapesList)
            {
                var childCopy = (FakeShape)child.CopyDetached();
                childCopy.ParentGroup = copy;
                copy.ShapesList.Add(childCopy);
            }

            return copy;
        }

        public IReadOnlyList<ShapePropertyEntry> ReadAllProperties() =>
            _properties.Select(p => new ShapePropertyEntry(p.Key.Name, p.Key.Id, p.Value)).ToList();

        public bool IsInFrontOf(ICorelShape other)
        {
            var list = Container;
            var mine = list?.IndexOf(this) ?? -1;
            var theirs = list?.IndexOf(other) ?? -1;
            return mine >= 0 && theirs >= 0 && mine < theirs;
        }

        public void OrderFrontOf(ICorelShape other)
        {
            var list = Container;
            if (list == null || !list.Remove(this)) return;
            var index = list.IndexOf(other);
            list.Insert(index < 0 ? 0 : index, this);
        }

        public void OrderToBack()
        {
            var list = Container;
            if (list != null && list.Remove(this)) list.Add(this);
        }

        public IReadOnlyList<ICorelShape> Ungroup()
        {
            var list = Container;
            var index = list?.IndexOf(this) ?? -1;
            var children = ShapesList.Cast<FakeShape>().ToList();
            foreach (var child in children)
            {
                child.ParentGroup = ParentGroup;
                child.CurrentLayer = CurrentLayer;
                // Como no Corel, cada filho passa a carregar a rotação que vinha do grupo.
                child.RotationAngle += RotationAngle;
            }

            ShapesList.Clear();
            if (list != null && index >= 0)
            {
                list.RemoveAt(index);
                list.InsertRange(index, children);
            }

            IsDeleted = true;
            return children;
        }

        // ---- Definir objetos ----

        public ShapeOutline? Outline { get; private set; }

        public void SetOutline(ShapeOutline outline) => Outline = outline;

        public (int Red, int Green, int Blue)? Fill { get; private set; }

        public void SetUniformFill(int red, int green, int blue) => Fill = (red, green, blue);

        /// <summary>Quantas vezes foi espelhado (ímpar = espelhado).</summary>
        public int Flips { get; private set; }

        /// <summary>Simula um Corel que desloca o centro ao espelhar.</summary>
        public double FlipShiftX { get; set; }

        public void FlipHorizontally()
        {
            Flips++;
            CenterX += FlipShiftX;
        }

        // ---- Dividir em partes ----

        /// <summary>Como no Corel: sai da camada/grupo e vai para o conteúdo do PowerClip do contêiner,
        /// e acompanha o contêiner quando ele se move.</summary>
        public void PlaceInsidePowerClip(ICorelShape container)
        {
            var target = (FakeShape)container;
            Container?.Remove(this);
            ParentGroup = null;
            CurrentLayer = null;
            target.PowerClipShapesList.Add(this);
            target.HasPowerClipContents = true;
        }

        public bool OutlineRemoved { get; private set; }

        public void RemoveOutline() => OutlineRemoved = true;

        /// <summary>O que ReadOutline devolve (null = o Corel não deu contorno).</summary>
        public IReadOnlyList<OutlineElement>? OutlineElements { get; set; }

        public IReadOnlyList<OutlineElement>? ReadOutline() => OutlineElements;

        /// <summary>Como no Corel, em volta do centro: 90°/270° trocam largura e altura da caixa.</summary>
        public void Rotate(double degrees)
        {
            RotationAngle += degrees;
            if (System.Math.Abs(System.Math.IEEERemainder(degrees, 180)) > 1e-9) (Width, Height) = (Height, Width);
        }

        /// <summary>Conteúdo do texto artístico criado por FakeLayer.CreateArtisticText.</summary>
        public string? Text { get; set; }

        /// <summary>Traços da curva criada por FakeLayer.CreateLines.</summary>
        public IReadOnlyList<CurveLine> Lines { get; set; } = new List<CurveLine>();

        /// <summary>Vértices do polígono criado por FakeLayer.CreatePolygon (antes de mover).</summary>
        public IReadOnlyList<(double X, double Y)> PolygonPoints { get; set; } = new List<(double X, double Y)>();

        public string? FontFamily { get; set; }

        public double FontSizePoints { get; set; }
    }

    /// <summary>Simula Application.CreateShapeRange: Group() cria um grupo novo no lugar do primeiro
    /// objeto; Select() guarda a seleção.</summary>
    public sealed class FakeArrangement : IShapeArrangement
    {
        public List<ICorelShape> Selected { get; } = new List<ICorelShape>();

        public ICorelShape Group(IReadOnlyList<ICorelShape> shapes)
        {
            var first = (FakeShape)shapes[0];
            var list = first.Container;
            var index = list?.IndexOf(first) ?? -1;
            var group = new FakeShape { Kind = ShapeKind.Group, CurrentLayer = first.CurrentLayer, ParentGroup = first.ParentGroup };
            foreach (FakeShape shape in shapes)
            {
                shape.Container?.Remove(shape);
                shape.ParentGroup = group;
                group.ShapesList.Add(shape);
            }

            if (list != null) list.Insert(System.Math.Min(System.Math.Max(index, 0), list.Count), group);
            return group;
        }

        public void Select(IReadOnlyList<ICorelShape> shapes)
        {
            Selected.Clear();
            Selected.AddRange(shapes);
        }
    }
}
