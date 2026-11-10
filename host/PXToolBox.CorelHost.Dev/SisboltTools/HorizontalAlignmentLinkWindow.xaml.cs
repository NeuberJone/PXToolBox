using System;
using System.Windows;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;

namespace PXToolBoxDev.SisboltTools
{
    /// <summary>Confirma os papéis (a alinhar × referência) antes de gravar o vínculo — ver
    /// HorizontalAlignmentLinkService. Zoom e gravação vêm do painel (COM e Ctrl+Z ficam lá).</summary>
    public partial class HorizontalAlignmentLinkWindow : Window
    {
        private readonly HorizontalAlignmentLinkService _service;
        private readonly Action<ICorelShape> _zoom;
        private readonly Func<ICorelShape, ICorelShape, OperationResult?> _save;

        public HorizontalAlignmentLinkWindow(ICorelShape target, ICorelShape reference, HorizontalAlignmentLinkService service,
            Action<ICorelShape> zoom, Func<ICorelShape, ICorelShape, OperationResult?> save)
        {
            InitializeComponent();
            Target = target;
            Reference = reference;
            _service = service;
            _zoom = zoom;
            _save = save;
            Refresh();
        }

        public ICorelShape Target { get; private set; }
        public ICorelShape Reference { get; private set; }

        /// <summary>Resultado da gravação, quando Salvar foi usado.</summary>
        public OperationResult? Result { get; private set; }

        private void Refresh()
        {
            TargetText.Text = Describe(Target);
            ReferenceText.Text = Describe(Reference);
            var identifier = _service.ExistingIdentifier(Reference);
            var link = _service.ExistingLink(Target);
            if (identifier != null)
            {
                WarningText.Text = $"A referência já tem um identificador ({identifier}). Esse caso ainda não foi mapeado e fica fora desta versão — nada será gravado. Troque os papéis ou escolha outra referência.";
                // Escondido, não só desabilitado: o botão desabilitado padrão do Windows fica
                // claro e parece destacado neste fundo escuro.
                SaveButton.IsEnabled = false;
                SaveButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                WarningText.Text = link == null ? "" : $"O objeto a alinhar já tem um vínculo ({link}); Salvar o substitui.";
                SaveButton.IsEnabled = true;
                SaveButton.Visibility = Visibility.Visible;
            }

            WarningText.Visibility = WarningText.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        internal static string Describe(ICorelShape shape)
        {
            string name;
            try { name = shape.Name; } catch (Exception) { name = ""; }
            return $"{KindLabel(shape.Kind)}{(string.IsNullOrWhiteSpace(name) ? " sem nome" : $" \"{name}\"")} · StaticID {shape.StaticId}";
        }

        private static string KindLabel(ShapeKind kind) => kind switch
        {
            ShapeKind.Rectangle => "Retângulo",
            ShapeKind.Ellipse => "Elipse",
            ShapeKind.Curve => "Curva",
            ShapeKind.Text => "Texto",
            ShapeKind.Group => "Grupo",
            ShapeKind.Bitmap => "Bitmap",
            ShapeKind.Polygon => "Polígono",
            _ => "Objeto",
        };

        private void OnSwapRoles(object sender, RoutedEventArgs e)
        {
            (Target, Reference) = (Reference, Target);
            Refresh();
        }

        private void OnZoomReference(object sender, RoutedEventArgs e) => _zoom(Reference);

        private void OnSave(object sender, RoutedEventArgs e)
        {
            Result = _save(Target, Reference);
            if (Result != null && Result.IsSuccess)
            {
                DialogResult = true;
                return;
            }

            WarningText.Text = Result?.Message ?? "Não foi possível gravar (sem conexão com o CorelDRAW ou sem documento).";
            WarningText.Visibility = Visibility.Visible;
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
