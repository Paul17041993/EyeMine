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
    public class BehaviourKey : KeyBase, INotifyPropertyChanged
    {
        #region Ctor

        private KeyValue kv;

        public BehaviourKey() : base()
        {
            kv = null;
        }

        #endregion

        #region Properties

        // It's a bit annoying that we need a default for the FunctionKeys enum
        public static readonly DependencyProperty FunctionKeyProperty =
            DependencyProperty.Register("FunctionKey", typeof(FunctionKeys), typeof(BehaviourKey), 
                                        new PropertyMetadata(default(FunctionKeys), OnFunctionKeyChanged));

        public FunctionKeys FunctionKey
        {
            get { return (FunctionKeys)GetValue(FunctionKeyProperty); }
            set { SetValue(FunctionKeyProperty, value); }
        }

        public override KeyValue Value
        {
            get { return kv; }
            set { kv = value; }
        }

        private static void OnFunctionKeyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var senderAsKey = sender as BehaviourKey;
            if (senderAsKey != null)
            {
                FunctionKeys? value = e.NewValue as FunctionKeys?;
                if (value.HasValue)
                {
                    senderAsKey.kv = new KeyValue(value.Value);
                }
            }
        }

        #endregion
        
    }
}
