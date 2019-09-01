using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;

namespace TileShop.WPF.Keybinding
{
    /// <summary>
    /// KeyTrigger to support firing methods from keyboard events
    /// </summary>
    /// <remarks>
    /// Implementation from https://github.com/Caliburn-Micro/Caliburn.Micro/blob/master/samples/scenarios/Scenario.KeyBinding/Input/KeyTrigger.cs
    /// </remarks>
    public class KeyTrigger : TriggerBase<UIElement>
    {
        public static readonly DependencyProperty KeyProperty =
            DependencyProperty.Register(nameof(Key), typeof(Key), typeof(KeyTrigger), null);

        public static readonly DependencyProperty ModifiersProperty =
            DependencyProperty.Register(nameof(Modifiers), typeof(ModifierKeys), typeof(KeyTrigger), null);

        public Key Key
        {
            get { return (Key)GetValue(KeyProperty); }
            set { SetValue(KeyProperty, value); }
        }

        public ModifierKeys Modifiers
        {
            get { return (ModifierKeys)GetValue(ModifiersProperty); }
            set { SetValue(ModifiersProperty, value); }
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.PreviewKeyDown += OnAssociatedObjectKeyDown;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            AssociatedObject.PreviewKeyDown -= OnAssociatedObjectKeyDown;
        }

        private void OnAssociatedObjectKeyDown(object sender, KeyEventArgs e)
        {
            var key = (e.Key == Key.System) ? e.SystemKey : e.Key;
            if ((key == Key) && (Keyboard.Modifiers == GetActualModifiers(e.Key, Modifiers)))
            {
                InvokeActions(e);
            }
        }

        static ModifierKeys GetActualModifiers(Key key, ModifierKeys modifiers)
        {
            switch (key)
            {
                case Key.LeftCtrl:
                case Key.RightCtrl:
                    modifiers |= ModifierKeys.Control;
                    return modifiers;

                case Key.LeftAlt:
                case Key.RightAlt:
                    modifiers |= ModifierKeys.Alt;
                    return modifiers;

                case Key.LeftShift:
                case Key.RightShift:
                    modifiers |= ModifierKeys.Shift;
                    break;
            }

            return modifiers;
        }
    }
}
