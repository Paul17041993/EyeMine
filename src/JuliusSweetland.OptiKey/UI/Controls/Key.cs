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
    public class Key : KeyBase
    {
        #region Ctor

        public Key() : base()
        { }

        #endregion

        #region Properties

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(KeyValue), typeof(Key), new PropertyMetadata(default(KeyValue)));

        public override KeyValue Value
        {
            get { return (KeyValue)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        #endregion
        
    }
}
