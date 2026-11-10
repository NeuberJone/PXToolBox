using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;

namespace PXToolBoxDev.Favorites
{
    /// <summary>
    /// Atalho de favorito ligado ao botão ORIGINAL da seção: o clique dispara o próprio evento
    /// Click do original (os mesmos handlers, nenhuma regra duplicada) e habilitação, estado
    /// (<see cref="FrameworkElement.Tag"/>, usado pelo Tamanho Padrão) e dica vêm do original por
    /// binding — o atalho nunca tem estado próprio. O atalho é um ícone; o nome da ferramenta
    /// aparece na dica (primeira linha, em negrito) e no nome de acessibilidade.
    /// </summary>
    public static class FavoriteShortcut
    {
        public static Button Create(Button original, string label, object? content, Style? style)
        {
            var shortcut = new Button
            {
                Content = content ?? new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap },
            };
            if (style != null)
            {
                shortcut.Style = style;
            }

            shortcut.SetBinding(UIElement.IsEnabledProperty, new Binding(nameof(Button.IsEnabled)) { Source = original });
            shortcut.SetBinding(FrameworkElement.TagProperty, new Binding(nameof(Button.Tag)) { Source = original });
            shortcut.SetBinding(AutomationProperties.HelpTextProperty, new Binding { Path = new PropertyPath(AutomationProperties.HelpTextProperty), Source = original });
            shortcut.ToolTip = CreateToolTip(original, label);
            ToolTipService.SetShowOnDisabled(shortcut, true);
            AutomationProperties.SetName(shortcut, label);
            shortcut.Click += (_, _) => Invoke(original);
            return shortcut;
        }

        /// <summary>Nome da ferramenta + a dica atual do original (que pode mudar: estado do
        /// Tamanho Padrão, motivo de "Mostrar últimas camadas ocultadas" estar desabilitado).</summary>
        private static ToolTip CreateToolTip(Button original, string label)
        {
            var originalTip = new Run();
            originalTip.SetBinding(Run.TextProperty, new Binding(nameof(Button.ToolTip)) { Source = original, Mode = BindingMode.OneWay });

            var text = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 320 };
            text.Inlines.Add(new Bold(new Run(label)));
            text.Inlines.Add(new LineBreak());
            text.Inlines.Add(originalTip);
            return new ToolTip { Content = text };
        }

        /// <summary>Texto completo da dica de um atalho criado por <see cref="Create"/>.</summary>
        public static string ToolTipText(Button shortcut) =>
            (shortcut.ToolTip as ToolTip)?.Content is TextBlock text
                ? new TextRange(text.ContentStart, text.ContentEnd).Text
                : shortcut.ToolTip?.ToString() ?? "";

        /// <summary>Executa a ação do original exatamente como um clique nele; não faz nada se
        /// o original estiver desabilitado.</summary>
        public static bool Invoke(Button original)
        {
            if (!original.IsEnabled)
            {
                return false;
            }

            original.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, original));
            return true;
        }
    }
}
