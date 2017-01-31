using JuliusSweetland.OptiKey.UI.Controls;
using JuliusSweetland.OptiKey.Models;
using JuliusSweetland.OptiKey.Enums;
using System.Windows.Controls;
using System.Xml.Serialization;
using System.IO;
using System.Xml.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


// Helper method to get optional elements from XML
public static class Extensions
{
    public static string GetStringElementIfExists(this XElement @this, string element, string defaultString)
    {
        return @this.Element(element) != null ? @this.Element(element).Value : defaultString;
    }
       
    public static int GetIntElementIfExists(this XElement @this, string element, int defaultInt)
    {
        return @this.Element(element) != null ? (int)@this.Element(element) : defaultInt;
    }
}

namespace JuliusSweetland.OptiKey.UI.Views.Keyboards.Common
{
    /// <summary>
    /// Interaction logic for CustomKeyboard.xaml
    /// </summary>
    public partial class CustomKeyboard : KeyboardView
    {

        // code smell: this does more than one thing
        private void GetCommonPropsAndAddKey(Key key, XElement el)
        {
            string sizeGroup = "KeyWithSymbolAndText"; // TODO: make configurable?

            key.SharedSizeGroup = sizeGroup;
            key.Text = el.GetStringElementIfExists("Text", "");
            var row = el.GetIntElementIfExists("Row", 0);
            var col = el.GetIntElementIfExists("Col", 0);
            var width = el.GetIntElementIfExists("Width", 1);
            var height = el.GetIntElementIfExists("Height", 1);

            this.AddKey(key, row, col, width, height);
        }

        public CustomKeyboard()
        {

            InitializeComponent();

            string inputFile = "c:/Users/Kirsty/Documents/keyboard.xml";

            // TODO: IO exception
            XmlSerializer serializer = new XmlSerializer(typeof(KeyboardSpec));
            FileStream readStream = new FileStream(inputFile, FileMode.Open);
            KeyboardSpec spec = (KeyboardSpec)serializer.Deserialize(readStream);
            readStream.Close();

            //            spec.Items[0].

            var xmlStr = File.ReadAllText(inputFile);
            var str = XElement.Parse(xmlStr);
            var grid = str.Element("Grid");
            var keys = str.Element("Keys");

            // TEMP: dynamically add hardcoded stuff
            // TODO: read from an input file.
            int gridWidth = (int)grid.Element("Cols");
            int gridHeight = (int)grid.Element("Rows");

            for (int i = 0; i < gridHeight; i++)
            {
                MainGrid.RowDefinitions.Add(new RowDefinition());
            }

            for (int i = 0; i < gridWidth; i++)
            {
                MainGrid.ColumnDefinitions.Add(new ColumnDefinition());
            }

            // Iterate over each possible type of key and add to keyboard
            foreach (XElement el in keys.Elements("Key"))
            {
                Key newKey = new Key();
                var value = el.GetStringElementIfExists("Value", "");
                if (value.Length > 0)
                {
                    // bollocks, KeyValues isn't an enum. Phooey.
                    /*KeyValues kval;
                    Enum.TryParse<KeyValues>(value, kval);
                    
                    if (KeyValue.TryParse(value, out localkval))
                    {
                        kval = localFk;
                    }*/
                }
                this.GetCommonPropsAndAddKey(newKey, el);
            }
            foreach (XElement el in keys.Elements("TextKey"))
            {
                Key newKey = new Key();
                var value = el.GetStringElementIfExists("Value", "");
                if (value.Length > 0)
                {
                    newKey.Value = new KeyValue(value);
                }
                this.GetCommonPropsAndAddKey(newKey, el);
            }
            foreach (XElement el in keys.Elements("KeyPressKey"))
            {
                //TODO
            }
            foreach (XElement el in keys.Elements("BehaviourKey"))
            {
                Key newKey = new Key();
                
                var value = el.GetStringElementIfExists("FunctionKey", "");
                if (value.Length > 0)
                {  
                    FunctionKeys fKey;
                    if (Enum.TryParse(value, out fKey))
                    {
                        newKey.Value = new KeyValue(fKey);
                    }
                    else {
                        // TODO: error!
                    }
                }

                this.GetCommonPropsAndAddKey(newKey, el);

            }

            //{
            //    Key newKey = new Key();
            //    newKey.SharedSizeGroup = "KeyWithSymbolAndText";
            //    newKey.SymbolGeometry = (System.Windows.Media.Geometry)App.Current.Resources["BackIcon"];
            //    newKey.Text = JuliusSweetland.OptiKey.Properties.Resources.BACK;
            //    newKey.Value = KeyValues.BackFromKeyboardKey;

            //    this.AddKey(newKey, gridHeight - 1, gridWidth - 1);
            //}
            //Key newKey2 = new Key();
            //newKey2.Text = "hello";
            //newKey2.SharedSizeGroup = "KeyWithSymbolAndText";
            //newKey2.Value = new KeyValue("Hello, World");

            //this.AddKey(newKey2, 1, 1, 1, 2);

            //Key newKey3 = new Key();
            //newKey3.Text = "bye";
            //newKey3.SharedSizeGroup = "KeyWithSymbolAndText";
            //newKey3.Value = new KeyValue("Tara");

            //this.AddKey(newKey3, 0, 0);

            //{
            //    KeyPressKey newKey4 = new KeyPressKey();
            //    newKey4.SharedSizeGroup = "KeyWithSymbolAndText";
            //    newKey4.Text = "pressA";
            //    newKey4.KeyPressType = KeyValuePress.KeyPressType.Press;
            //    newKey4.Key = "a";
            //    this.AddKey(newKey4, 0, 1);

            //}
            //{
            //    KeyPressKey newKey4 = new KeyPressKey();
            //    newKey4.SharedSizeGroup = "KeyWithSymbolAndText";
            //    newKey4.Text = "releaseA";
            //    newKey4.KeyPressType = KeyValuePress.KeyPressType.Release;
            //    newKey4.Key = "a";
            //    this.AddKey(newKey4, 0, 2);

            //}
            //{
            //    KeyPressKey newKey4 = new KeyPressKey();
            //    newKey4.SharedSizeGroup = "KeyWithSymbolAndText";
            //    newKey4.Text = "A";
            //    newKey4.KeyPressType = KeyValuePress.KeyPressType.PressAndRelease;
            //    newKey4.Key = "a";
            //    this.AddKey(newKey4, 0, 3);

            //}

        }

        private void AddKey(KeyBase key, int row, int col, int rowspan = 1, int colspan = 1)
        {
            MainGrid.Children.Add(key);
            Grid.SetColumn(key, col);
            Grid.SetRow(key, row);
            Grid.SetColumnSpan(key, colspan);
            Grid.SetRowSpan(key, rowspan);

        }
    }
}
