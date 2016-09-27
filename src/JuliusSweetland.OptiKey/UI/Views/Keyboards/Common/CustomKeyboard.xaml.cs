using JuliusSweetland.OptiKey.UI.Controls;
using System.Windows.Controls;
using JuliusSweetland.OptiKey.Models;

namespace JuliusSweetland.OptiKey.UI.Views.Keyboards.Common
{
    /// <summary>
    /// Interaction logic for CustomKeyboard.xaml
    /// </summary>
    public partial class CustomKeyboard : KeyboardView
    {
        public CustomKeyboard() 
        {

            InitializeComponent();

            // TEMP: dynamically add hardcoded stuff
            // TODO: read from an input file.
            int gridWidth = 5;
            int gridHeight = 3;

            for (int i = 0; i < gridHeight; i++) {
                 MainGrid.RowDefinitions.Add(new RowDefinition());
            }

            for (int i = 0; i < gridWidth; i++)
            {
                MainGrid.ColumnDefinitions.Add(new ColumnDefinition());
            }

            Key newKey = new Key();
            newKey.SharedSizeGroup = "KeyWithSymbolAndText";
            newKey.SymbolGeometry = (System.Windows.Media.Geometry)App.Current.Resources["BackIcon"];
            newKey.Text = JuliusSweetland.OptiKey.Properties.Resources.BACK;
            newKey.Value = KeyValues.BackFromKeyboardKey;

            MainGrid.Children.Add(newKey);
            Grid.SetColumn(newKey, gridWidth - 1);
            Grid.SetRow(newKey, gridHeight -1);

            Key newKey2 = new Key();
            newKey2.Text = "hello";
            newKey2.SharedSizeGroup = "KeyWithSymbolAndText";
            newKey2.Value = new KeyValue("a");

            MainGrid.Children.Add(newKey2);
            Grid.SetColumn(newKey2, 1);
            Grid.SetRow(newKey2, 1);
            Grid.SetColumnSpan(newKey2, 2);
            
        }
    }
}
