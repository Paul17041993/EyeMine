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
    public class TextKey : KeyBase, INotifyPropertyChanged
    {
        #region Ctor

        private KeyValue kv;

        public TextKey() : base()
        {
            kv = null;
        }

        #endregion

        #region Properties

        public static readonly DependencyProperty StringProperty =
            DependencyProperty.Register("String", typeof(string), typeof(TextKey), 
                                        new PropertyMetadata(default(string), OnTextChanged));

        public string String
        {
            get { return (string)GetValue(StringProperty); }
            set { SetValue(StringProperty, value); }
        }

        public override KeyValue Value
        {
            get { return kv; }
            set { kv = value; }
        }

        private static void OnTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var senderAsKey = sender as TextKey;
            if (senderAsKey != null)
            {
                var value = e.NewValue as string;
                senderAsKey.kv = new KeyValue(value);
            }
        }

        #endregion
        
    }
}
