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
    public class LinkKey : KeyBase, INotifyPropertyChanged
    {
        #region Ctor

        private KeyValue kv;

        public LinkKey() : base()
        {
            kv = null;
        }

        #endregion

        #region Properties

        public static readonly DependencyProperty LinkProperty =
            DependencyProperty.Register("Link", typeof(string), typeof(LinkKey), 
                                        new PropertyMetadata(default(string), OnLinkChanged));

        public string Link
        {
            get { return (string)GetValue(LinkProperty); }
            set { SetValue(LinkProperty, value); }
        }

        public override KeyValue Value
        {
            get { return kv; }
            set { kv = value; }
        }

        private static void OnLinkChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var senderAsKey = sender as LinkKey;
            if (senderAsKey != null)
            {
                var value = e.NewValue as string;
                senderAsKey.Value = new KeyValueLink(value);
            }
        }

        #endregion
        
    }
}
