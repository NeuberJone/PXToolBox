using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PXToolBoxDev.Favorites
{
    /// <summary>
    /// Reordenar os atalhos dos Favoritos arrastando com o botão esquerdo. Sem OLE
    /// (<c>DragDrop.DoDragDrop</c>): o arraste nunca sai do painel nem chega ao documento do
    /// CorelDRAW — é só captura do mouse no próprio painel. Clique curto continua sendo clique
    /// (o arraste só começa depois da distância mínima do sistema); soltar fora dos Favoritos
    /// cancela. O item é achado pela posição (não pelo evento do botão), então atalhos
    /// desabilitados também podem ser arrastados. Enquanto arrasta, uma barra mostra onde ele
    /// vai entrar.
    /// </summary>
    public sealed class FavoriteDragReorder
    {
        private readonly Panel _items;
        private readonly FrameworkElement _indicator;
        private readonly Func<int, int, bool> _move;
        private int _pressedIndex = -1;
        private Point _pressedAt;
        private bool _dragging;
        private UIElement? _dragged;

        /// <param name="items">Painel com os atalhos, na ordem dos favoritos visíveis.</param>
        /// <param name="indicator">Barra de inserção, num Canvas sobreposto a
        /// <paramref name="items"/> (mesmas coordenadas).</param>
        /// <param name="move">(posição arrastada, inserir antes de) — ver
        /// <see cref="PanelFavoritesList.MoveTo"/>.</param>
        public FavoriteDragReorder(Panel items, FrameworkElement indicator, Func<int, int, bool> move)
        {
            _items = items;
            _indicator = indicator;
            _move = move;
            _indicator.Visibility = Visibility.Collapsed;
            // Fundo para receber o mouse entre os ícones e sobre atalhos desabilitados.
            if (_items.Background == null)
            {
                _items.Background = Brushes.Transparent;
            }

            _items.PreviewMouseLeftButtonDown += OnPressed;
            _items.PreviewMouseMove += OnMoved;
            _items.PreviewMouseLeftButtonUp += OnReleased;
            // LostMouseCapture é um evento roteado que SOBE: quando o arraste começa e o painel
            // toma a captura, o botão pressionado perde a dele e o aviso chega aqui também. Só
            // cancela quando quem perdeu a captura foi o próprio painel (soltou fora da janela,
            // Alt+Tab…). Sem esta checagem o arraste era cancelado no instante em que começava
            // (dev.66–dev.70: arrastar não funcionava em ícones habilitados).
            _items.LostMouseCapture += (_, e) =>
            {
                if (ReferenceEquals(e.OriginalSource, _items)) Stop();
            };
        }

        public bool IsDragging => _dragging;

        private void OnPressed(object sender, MouseButtonEventArgs e) => Press(e.GetPosition(_items));

        private void OnMoved(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                if (!_dragging) _pressedIndex = -1;
                return;
            }

            e.Handled = Drag(e.GetPosition(_items));
        }

        private void OnReleased(object sender, MouseButtonEventArgs e) => e.Handled = Release(e.GetPosition(_items));

        // Press/Drag/Release recebem a posição já no sistema do painel (os eventos só convertem):
        // assim o mesmo caminho é exercitado fora do Corel, sem mouse de verdade.

        /// <summary>Botão esquerdo pressionado em <paramref name="position"/>.</summary>
        public void Press(Point position)
        {
            _pressedAt = position;
            _pressedIndex = IndexAt(ItemBounds(), position);
        }

        /// <summary>Mouse movido com o botão pressionado. Verdadeiro se é (ou virou) arraste.</summary>
        public bool Drag(Point position)
        {
            if (_pressedIndex < 0)
            {
                return false;
            }

            if (!_dragging)
            {
                if (Math.Abs(position.X - _pressedAt.X) < SystemParameters.MinimumHorizontalDragDistance
                    && Math.Abs(position.Y - _pressedAt.Y) < SystemParameters.MinimumVerticalDragDistance)
                {
                    return false;
                }

                // Tirar a captura do botão cancela o clique dele.
                _dragging = true;
                _dragged = _items.Children[_pressedIndex];
                _dragged.Opacity = 0.45;
                _items.Cursor = Cursors.SizeAll;
                _items.CaptureMouse();
            }

            ShowIndicator(position);
            return true;
        }

        /// <summary>Botão solto. Verdadeiro se encerrou um arraste (o clique não acontece).</summary>
        public bool Release(Point position)
        {
            if (!_dragging)
            {
                _pressedIndex = -1;
                return false;
            }

            var from = _pressedIndex;
            var inside = IsInside(position);
            var bounds = ItemBounds();
            Stop();
            if (inside)
            {
                _move(from, InsertionIndex(bounds, position));
            }

            return true;
        }

        private void Stop()
        {
            if (!_dragging)
            {
                return;
            }

            _dragging = false;
            _pressedIndex = -1;
            if (_dragged != null)
            {
                _dragged.Opacity = 1;
                _dragged = null;
            }

            _items.ClearValue(FrameworkElement.CursorProperty);
            _indicator.Visibility = Visibility.Collapsed;
            if (_items.IsMouseCaptured)
            {
                _items.ReleaseMouseCapture();
            }
        }

        private void ShowIndicator(Point position)
        {
            var bounds = ItemBounds();
            if (!IsInside(position) || bounds.Count == 0)
            {
                _indicator.Visibility = Visibility.Collapsed;
                return;
            }

            var bar = IndicatorBounds(bounds, InsertionIndex(bounds, position), BarThickness);
            Canvas.SetLeft(_indicator, bar.X);
            Canvas.SetTop(_indicator, bar.Y);
            _indicator.Width = bar.Width;
            _indicator.Height = bar.Height;
            _indicator.Visibility = Visibility.Visible;
        }

        // Um pouco de folga em volta: soltar "logo depois do último" na mesma linha conta.
        private bool IsInside(Point position) =>
            position.X >= -8 && position.Y >= -8 && position.X <= _items.ActualWidth + 8 && position.Y <= _items.ActualHeight + 8;

        private List<Rect> ItemBounds() =>
            _items.Children.OfType<FrameworkElement>()
                .Select(child => child.TransformToAncestor(_items).TransformBounds(new Rect(child.RenderSize)))
                .ToList();

        /// <summary>Item sob o ponto, ou -1.</summary>
        public static int IndexAt(IReadOnlyList<Rect> items, Point point)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].Contains(point)) return i;
            }

            return -1;
        }

        /// <summary>Espessura da barra de inserção (vertical entre ícones, horizontal entre
        /// botões com nome).</summary>
        public const double BarThickness = 2;

        /// <summary>Lista de uma coluna (Favoritos com nome): dois ou mais itens, todos na mesma
        /// coluna e cada um na sua linha.</summary>
        public static bool IsSingleColumn(IReadOnlyList<Rect> items) =>
            items.Count >= 2
            && items.All(r => Math.Abs(r.Left - items[0].Left) < 0.5)
            && items.Select(r => Math.Round(r.Top)).Distinct().Count() == items.Count;

        /// <summary>Onde o item entra se soltar em <paramref name="point"/>: na linha mais
        /// próxima (os ícones quebram linha), antes do primeiro item cujo centro fica à direita do
        /// ponto; depois do último da linha se nenhum. Numa coluna só (botões com nome), antes do
        /// primeiro item cujo centro fica ABAIXO do ponto. 0..Count.</summary>
        public static int InsertionIndex(IReadOnlyList<Rect> items, Point point)
        {
            if (items.Count == 0) return 0;

            if (IsSingleColumn(items))
            {
                for (var i = 0; i < items.Count; i++)
                {
                    if (point.Y < items[i].Top + items[i].Height / 2) return i;
                }

                return items.Count;
            }

            double VerticalDistance(Rect r) =>
                point.Y < r.Top ? r.Top - point.Y : point.Y > r.Bottom ? point.Y - r.Bottom : 0;

            var nearest = items.Min(VerticalDistance);
            var row = Enumerable.Range(0, items.Count).Where(i => VerticalDistance(items[i]) - nearest < 0.5).ToList();
            foreach (var i in row)
            {
                if (point.X < items[i].Left + items[i].Width / 2) return i;
            }

            return row.Last() + 1;
        }

        /// <summary>Barra vertical (largura <paramref name="width"/>) no espaço antes do item
        /// <paramref name="insertBefore"/>, ou logo depois do último. Numa coluna só, barra
        /// horizontal (altura <paramref name="width"/>) acima do item ou abaixo do último.</summary>
        public static Rect IndicatorBounds(IReadOnlyList<Rect> items, int insertBefore, double width)
        {
            if (items.Count == 0) return Rect.Empty;

            if (IsSingleColumn(items))
            {
                if (insertBefore < items.Count)
                {
                    var above = items[insertBefore];
                    return new Rect(above.Left, Math.Max(0, above.Top - width - 1), above.Width, width);
                }

                var bottom = items[items.Count - 1];
                return new Rect(bottom.Left, bottom.Bottom + 1, bottom.Width, width);
            }

            if (insertBefore < items.Count)
            {
                var r = items[insertBefore];
                return new Rect(Math.Max(0, r.Left - width - 1), r.Top, width, r.Height);
            }

            var last = items[items.Count - 1];
            return new Rect(last.Right + 1, last.Top, width, last.Height);
        }
    }
}
