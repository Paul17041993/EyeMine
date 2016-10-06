using System;
using System.ComponentModel;
using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using JuliusSweetland.OptiKey.Enums;
using JuliusSweetland.OptiKey.Extensions;
using JuliusSweetland.OptiKey.Models;
using JuliusSweetland.OptiKey.Properties;
using JuliusSweetland.OptiKey.UI.Utilities;
using JuliusSweetland.OptiKey.UI.ViewModels;

namespace JuliusSweetland.OptiKey.UI.Controls
{
    public class KeyPressKey : KeyBase, INotifyPropertyChanged
    {
        #region Ctor

        private KeyValue kv;

        public KeyPressKey() : base()
        {
            kv = null;
        }

        #endregion

        #region Properties

        public static readonly DependencyProperty KeyProperty =
            DependencyProperty.Register("Key", typeof(string), typeof(KeyPressKey), 
                                        new PropertyMetadata(default(string), OnKeyChanged));

        public static readonly DependencyProperty TypeProperty =
            DependencyProperty.Register("Type", typeof(KeyValuePress.KeyPressType), typeof(KeyPressKey),
                                        new PropertyMetadata(default(KeyValuePress.KeyPressType), OnTypeChanged));

        public static readonly DependencyProperty DelayProperty =
                    DependencyProperty.Register("Delay", typeof(int), typeof(KeyPressKey),
                                                new PropertyMetadata(0, OnTypeChanged));

        public int Delay
        {
            get { return (int)GetValue(DelayProperty); }
            set { SetValue(DelayProperty, value); }
        }

        public string Key
        {
            get { return (string)GetValue(KeyProperty); }
            set { SetValue(KeyProperty, value); }
        }

        public KeyValuePress.KeyPressType Type
        {
            get { return (KeyValuePress.KeyPressType)GetValue(TypeProperty); }
            set { SetValue(TypeProperty, value); }
        }

        public override KeyValue Value
        {
            get { return kv; }
            set { kv = value; }
        }

        private static void OnTypeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var senderAsKey = sender as KeyPressKey;
            if (senderAsKey != null)
            {
                KeyValuePress.KeyPressType? newType = e.NewValue as KeyValuePress.KeyPressType?;
                if (newType.HasValue)
                {
                    KeyValuePress oldKv = senderAsKey.kv == null ? new KeyValuePress() : senderAsKey.kv as KeyValuePress;

                    // copy, adding new value
                    senderAsKey.Value = new KeyValuePress(oldKv.Key, newType.Value, oldKv.DurationMs);
                }
            }
        }

        private static void OnDelayChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var senderAsKey = sender as KeyPressKey;
            if (senderAsKey != null)
            {
                int? newDuration = e.NewValue as int?;
                if (newDuration.HasValue)
                {
                    KeyValuePress oldKv = senderAsKey.kv == null ? new KeyValuePress() : senderAsKey.kv as KeyValuePress;

                    // copy, adding new value
                    senderAsKey.Value = new KeyValuePress(oldKv.Key, oldKv.Type, newDuration.Value);
                }
            }
        }

        private static void OnKeyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var senderAsKey = sender as KeyPressKey;
            if (senderAsKey != null)
            {
                string newKey = e.NewValue as string;
                if (newKey != null)
                {
                    KeyValuePress oldKv = senderAsKey.kv == null ? new KeyValuePress() : senderAsKey.kv as KeyValuePress;

                    // copy, adding new value
                    senderAsKey.Value = new KeyValuePress(newKey, oldKv.Type, oldKv.DurationMs);
                }
            }
        }

        #endregion
        
    }
}
