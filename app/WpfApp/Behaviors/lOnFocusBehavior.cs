using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LugonTestbed.Behaviors
{
    /// <summary>
    /// Attached behavior to select all text in a TextBox on keyboard focus (Tab), and on first mouse click.
    /// Use from XAML styles without code-behind handlers.
    /// </summary>
    public static class SelectAllOnFocusBehavior
    {
        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached(
                "Enabled",
                typeof(bool),
                typeof(SelectAllOnFocusBehavior),
                new PropertyMetadata(false, OnEnabledChanged));

        public static void SetEnabled(DependencyObject obj, bool value) => obj.SetValue(EnabledProperty, value);
        public static bool GetEnabled(DependencyObject obj) => (bool)obj.GetValue(EnabledProperty);

        private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox tb)
            {
                if ((bool)e.NewValue)
                {
                    tb.GotKeyboardFocus += Tb_GotKeyboardFocus;
                    tb.PreviewMouseLeftButtonDown += Tb_PreviewMouseLeftButtonDown;
                }
                else
                {
                    tb.GotKeyboardFocus -= Tb_GotKeyboardFocus;
                    tb.PreviewMouseLeftButtonDown -= Tb_PreviewMouseLeftButtonDown;
                }
            }
        }

        private static void Tb_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                // Defer to ensure focus is actually in the box
                tb.Dispatcher.InvokeAsync(tb.SelectAll);
            }
        }

        // This makes the first click focus the box *and* select all, instead of placing the caret.
        private static void Tb_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox tb && !tb.IsKeyboardFocusWithin)
            {
                e.Handled = true; // stop the caret placement
                tb.Focus();       // focus will trigger GotKeyboardFocus handler, which SelectAll’s
            }
        }
    }
}

