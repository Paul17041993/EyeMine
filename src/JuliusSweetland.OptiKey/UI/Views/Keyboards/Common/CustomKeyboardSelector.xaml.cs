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
using System.Windows.Media;
using System.Reflection;

using log4net;


namespace JuliusSweetland.OptiKey.UI.Views.Keyboards.Common
{

    /// <summary>
    /// Interaction logic for CustomKeyboardSelector.xaml
    /// </summary>
    public partial class CustomKeyboardSelector : KeyboardView
    {
        #region Constants

        private const string ApplicationDataSubPath = @"JuliusSweetland\OptiKey\Keyboards\";
        private const string OriginalKeyboardsSubPath = @"Keyboards\";

        #endregion

        #region Private Members
    
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // TODO: Make user configurable?
        private int mRows = 3;
        private int mCols = 3;
        
        private int mPageIndex = 0;

        private List<string> mKeyboardFilenames;
        
        #endregion

        private static string GetUserKeyboardFolder()
        {
            var applicationDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ApplicationDataSubPath);
            Directory.CreateDirectory(applicationDataPath); //Does nothing if already exists

            Log.DebugFormat("GetUserKeyboardFolder: {0}", applicationDataPath);
            
            return applicationDataPath;
        }

        private int RowFromIndex(int index)
        {
            return index / this.mCols; // integer division
        }

        private int ColFromIndex(int index)
        {
            return index % this.mCols;
        }
        private void AddKeyboardKey(string keyboardLink, int index) {
            LinkKey lKey = new LinkKey();
            lKey.Link = keyboardLink;
            lKey.SharedSizeGroup = "KeyboardKey";
            lKey.Text = Path.GetFileNameWithoutExtension(keyboardLink); //TODO: extract name from xml, default to filename        
            this.AddKey(lKey,  this.RowFromIndex(index),  this.ColFromIndex(index));
        }

        private void FindKeyboards()
        {
            string filePath = GetUserKeyboardFolder();

            string[] fileArray = Directory.GetFiles(filePath, "*.xml");

            Log.DebugFormat("found {0} keyboard files", fileArray.Length);

            // Put all files in a list
            // TODO: Read xml and check okay?
            // TODO: think about ordering?
            mKeyboardFilenames = new List<string>();
            foreach (string fileName in fileArray)
            {
                mKeyboardFilenames.Add(Path.Combine(filePath, fileName));
                Log.DebugFormat("found keyboard file: {0}", fileName);
            }
        }

        public CustomKeyboardSelector(int pageIndex = 0)
        {
            this.mPageIndex = pageIndex; 
           
            InitializeComponent();

            // Look for custom keyboard files
            this.FindKeyboards();

            // Setup grid
            for (int i = 0; i < this.mRows; i++)
            {
                MainGrid.RowDefinitions.Add(new RowDefinition());
            }

            for (int i = 0; i < this.mCols; i++)
            {
                MainGrid.ColumnDefinitions.Add(new ColumnDefinition());
            }

            // Add back key 
            { 
                Key newKey = new Key();
                newKey.SharedSizeGroup = "BackButton";
                newKey.SymbolGeometry = (System.Windows.Media.Geometry)App.Current.Resources["BackIcon"];
                newKey.Text = JuliusSweetland.OptiKey.Properties.Resources.BACK;
                newKey.Value = KeyValues.BackFromKeyboardKey;
                this.AddKey(newKey, this.mRows - 1, this.mCols - 1);
            }
            
            // Add keyboard keys, or blanks
            // TODO: fill in N-1th button with a key if there's exactly N-1 keyboards.
            int maxKeyboardsPerPage = this.mCols * this.mRows - 2;
            int totalNumKeyboards = mKeyboardFilenames.Count;
            int remainingKeyboards =  totalNumKeyboards - maxKeyboardsPerPage*mPageIndex;
            int nKBs = Math.Min(remainingKeyboards, maxKeyboardsPerPage);
            int firstKB = maxKeyboardsPerPage * mPageIndex;
            for (int i = 0; i < maxKeyboardsPerPage; i++)
            {
                if (i < nKBs)
                {
                    // Add key to link to keyboard
                    string keyboardName = mKeyboardFilenames[firstKB + i];
                    this.AddKeyboardKey(keyboardName, i);
                }
                else
                {
                    // Add empty/inactive key for consistent aesthetic
                    Key newKey = new Key();
                    if (i == 0)
                    {
                        // Special case for empty grid                       
                        newKey.SharedSizeGroup = "BackButton";
                        newKey.Text = "No keyboards\nfound"; // TODO: resource string   
                    }
                    this.AddKey(newKey, this.RowFromIndex(i), this.ColFromIndex(i));
                }
            }

            // Add "more" key if required
            if (nKBs < remainingKeyboards) {
                Key newKey = new Key();
                newKey.SharedSizeGroup = "BackButton";
                // TODO: symbol '...'
                newKey.Text = "More..."; // TODO: resource string
                //newKey.Value = KeyValues.KeyValueLink; // TODO: need to separate loading keyboardselector vs keyboard.
                this.AddKey(newKey, this.mRows - 1, this.mCols - 2);
            }

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
