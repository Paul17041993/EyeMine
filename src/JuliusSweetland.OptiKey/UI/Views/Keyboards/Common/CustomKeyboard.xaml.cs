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

            this.AddKey(newKey, gridHeight - 1, gridWidth - 1);

            Key newKey2 = new Key();
            newKey2.Text = "hello";
            newKey2.SharedSizeGroup = "KeyWithSymbolAndText";
            newKey2.Value = new KeyValue("Hello, World");

            this.AddKey(newKey2, 1, 1, 1, 2);

            Key newKey3 = new Key();
            newKey3.Text = "bye";
            newKey3.SharedSizeGroup = "KeyWithSymbolAndText";
            newKey3.Value = new KeyValue("Tara");

            this.AddKey(newKey3, 0, 0);

        }

        private void AddKey(Key key, int row, int col, int rowspan = 1, int colspan = 1)
        {
            MainGrid.Children.Add(key);
            Grid.SetColumn(key, col);
            Grid.SetRow(key, row);
            Grid.SetColumnSpan(key, colspan);
            Grid.SetRowSpan(key, rowspan);

        }
    }
}
