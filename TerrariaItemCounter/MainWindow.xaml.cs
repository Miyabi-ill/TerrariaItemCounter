using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.Map;

namespace TerrariaItemCounter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static Dictionary<int, string> tileIdDict;
        private static Dictionary<string, short> itemIdDict;
        private static Dictionary<string, int> prefixIdDict;

        private readonly ObservableCollection<KeyValuePair<System.Windows.Point, int>> foundItemsCount = new ObservableCollection<KeyValuePair<System.Windows.Point, int>>();

        private string worldFileName;
        private Bitmap mapImage;
        private WorldMap worldMap;

        public MainWindow()
        {
            InitializeComponent();

            MapHelper.Initialize();
            Lang.InitializeLegacyLocalization();
            LanguageManager.Instance.SetLanguage(GameCulture.DefaultCulture);

            _ = new Main();
            Main.instance.Initialize();

            ImageViewer.PreviewMouseDown += ImageViewer_MouseDown;

            // Initialize ID dicts
            tileIdDict = new Dictionary<int, string>();
            var tileIdFields = typeof(TileID).GetFields(BindingFlags.Static | BindingFlags.Public);
            foreach (var field in tileIdFields)
            {
                if (field.Name == "Count" || field.Name == "None" || field.DeclaringType != typeof(ushort))
                {
                    continue;
                }
                tileIdDict.Add((ushort)field.GetValue(null), field.Name);
            }

            itemIdDict = new Dictionary<string, short>();
            var itemIdFields = typeof(ItemID).GetFields(BindingFlags.Static | BindingFlags.Public);
            foreach (var field in itemIdFields)
            {
                if (field.Name == "Count" || field.Name == "None")
                {
                    continue;
                }
                if (field.FieldType == typeof(short))
                {
                    var value = (short)field.GetValue(null);
                    itemIdDict.Add(field.Name, value);
                }
            }

            ItemNameSearch.Text = "Select Item";
            ItemNameSearch.DisplayMemberPath = "Key";
            ItemNameSearch.SelectedValuePath = "Value";
            ItemNameSearch.ItemsSource = itemIdDict;

            prefixIdDict = new Dictionary<string, int>();
            var prefixIdFields = typeof(PrefixID).GetFields(BindingFlags.Static | BindingFlags.Public);
            foreach (var field in prefixIdFields)
            {
                if (field.Name == "Count" || field.Name == "None")
                {
                    continue;
                }
                prefixIdDict.Add(field.Name, (int)field.GetValue(null));
            }

            ItemPrefixSearch.DisplayMemberPath = "Key";
            ItemPrefixSearch.SelectedValuePath = "Value";
            ItemPrefixSearch.ItemsSource = prefixIdDict;
            SearchResult.ItemsSource = foundItemsCount;
        }

        public void LoadWorld(string path)
        {
            for (int i = 0; i < Main.combatText.Length; i++)
            {
                Main.combatText[i] = new CombatText();
            }

            Main.mapReady = false;
            Main.ActiveWorldFileData = new WorldFileData(path, false);
            WorldFile.LoadWorld(false);
        }

        public int CountTiles(ushort tileId, int frameX, int frameY)
        {
            int count = 0;
            for (int x = 0; x < Main.maxTilesX; x++)
            {
                for (int y = 0; y < Main.maxTilesY; y++)
                {
                    if (Main.tile[x, y]?.active() == true
                        && Main.tile[x, y]?.type == tileId
                        && Main.tile[x, y]?.frameX == frameX
                        && Main.tile[x, y]?.frameY == frameY)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        public int CountTiles(int tileId)
        {
            int count = 0;
            for (int x = 0; x < Main.maxTilesX; x++)
            {
                for (int y = 0; y < Main.maxTilesY; y++)
                {
                    if (Main.tile[x, y]?.active() == true
                        && Main.tile[x, y]?.type == tileId)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        public Bitmap CreateMapImage()
        {
            int arrayIndex = 0;
            var array = new byte[Main.maxTilesX * Main.maxTilesY * 3];

            worldMap = new WorldMap(Main.maxTilesX, Main.maxTilesY)
            {
                _tiles = new MapTile[Main.maxTilesX, Main.maxTilesY]
            };

            for (int y = 0; y < Main.maxTilesY; y++)
            {
                for (int x = 0; x < Main.maxTilesX; x++)
                {
                    var mapTile = MapHelper.CreateMapTile(x, y, 255);
                    var color = MapHelper.GetMapTileXnaColor(ref mapTile);

                    array[arrayIndex * 3] = color.B;
                    array[arrayIndex * 3 + 1] = color.G;
                    array[arrayIndex * 3 + 2] = color.R;
                    arrayIndex++;

                    worldMap._tiles[x, y] = mapTile;
                }
            }

            return CreateBitmap(Main.maxTilesX, Main.maxTilesY, array);
        }

        private void Log_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;

            string[] fileList = (string[])e.Data.GetData(DataFormats.FileDrop, false);
            string filePath = fileList[0];
            if (filePath.EndsWith(".wld", StringComparison.InvariantCultureIgnoreCase))
            {
                if (File.Exists(filePath))
                {
                    worldFileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                    LoadWorld(filePath);

                    var image = CreateMapImage();
                    mapImage = image;
                    ImageViewer.ImageSource = Convert(image);

                    DropHelpGrid.Visibility = Visibility.Hidden;
                    foundItemsCount.Clear();
                }
            }
        }

        private void Log_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void ImageViewer_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var innerGrid = (Grid)((ScrollViewer)((Grid)ImageViewer.Content).Children[0]).Content;
            var pos = e.GetPosition(innerGrid);

            int x = (int)(pos.X / innerGrid.ActualWidth * Main.maxTilesX);
            int y = (int)(pos.Y / innerGrid.ActualHeight * Main.maxTilesY);

            try
            {
                if (Main.tile[x, y] == null)
                {
                    return;
                }
            }
            catch (IndexOutOfRangeException)
            {
                return;
            }

            if (!Main.tile[x, y].active())
            {
                return;
            }

            ushort tileId = Main.tile[x, y].type;
            int frameX = Main.tile[x, y].frameX;
            int frameY = Main.tile[x, y].frameY;

            int count = 0;
            if (MatchIDOnly.IsChecked == true)
            {
                count = CountTiles(tileId);
            }
            else
            {
                count = CountTiles(tileId, frameX, frameY);
            }

            if (tileIdDict.TryGetValue(tileId, out string name))
            {
                CountText.Text = "x: " + x + " y: " + y + " " + name + " : " + count;
            }
        }

        public BitmapImage Convert(Bitmap src)
        {
            MemoryStream ms = new MemoryStream();
            src.Save(ms, ImageFormat.Bmp);
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            ms.Seek(0, SeekOrigin.Begin);
            image.StreamSource = ms;
            image.EndInit();
            return image;
        }

        private Bitmap CreateBitmap(int width, int height, byte[] datas)
        {
            var b = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            //var ncp = b.Palette;
            //for (int i = 0; i < 256; i++)
            //    ncp.Entries[i] = System.Drawing.Color.FromArgb(255, i, i, i);
            //b.Palette = ncp;

            var BoundsRect = new Rectangle(0, 0, width, height);
            BitmapData bmpData = b.LockBits(BoundsRect,
                                            ImageLockMode.WriteOnly,
                                            b.PixelFormat);

            IntPtr ptr = bmpData.Scan0;

            int bytes = bmpData.Stride * b.Height;
            Marshal.Copy(datas, 0, ptr, bytes);
            b.UnlockBits(bmpData);
            return b;
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (ItemNameSearch.SelectedItem == null)
            {
                MatchCountText.Text = "Item name did not match.";
                MatchCountText.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            int id = ((KeyValuePair<string, short>)ItemNameSearch.SelectedItem).Value;

            int prefixId = -1;
            if (ItemPrefixSearch.SelectedItem != null)
            {
                var prefix = (KeyValuePair<string, int>)ItemPrefixSearch.SelectedItem;
                prefixId = prefix.Value;
            }

            int foundCount = 0;
            var foundPosition = new Dictionary<System.Windows.Point, int>();
            foreach (var chest in Main.chest)
            {
                if (chest != null)
                {
                    var chestPos = new System.Windows.Point(chest.x, chest.y);
                    foreach (var item in chest.item)
                    {
                        // active and id check
                        if (item != null
                            && item.active
                            && item.netID == id)
                        {
                            // prefix check
                            if (prefixId == -1
                                || item.prefix == prefixId)
                            {
                                foundCount += item.stack;
                                if (foundPosition.TryGetValue(chestPos, out int value))
                                {
                                    foundPosition[chestPos] += item.stack;
                                }
                                else
                                {
                                    foundPosition.Add(chestPos, item.stack);
                                }
                            }
                        }
                    }
                }
            }

            MatchCountText.Text = "Match: " + foundCount;
            MatchCountText.Foreground = System.Windows.Media.Brushes.Black;
            foundItemsCount.Clear();
            foreach (var pos in foundPosition)
            {
                foundItemsCount.Add(pos);
            }
        }

        private void SearchResult_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (((StackPanel)sender).DataContext is KeyValuePair<System.Windows.Point, int> pair)
            {
                ImageViewer.TargetPoint = pair.Key;
            }
        }

        private void SaveImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (mapImage != null && !string.IsNullOrEmpty(worldFileName))
            {
                string savePath = worldFileName + ".bmp";
                var dialog = new SaveFileDialog()
                {
                    FileName = savePath,
                    RestoreDirectory = true,
                    DefaultExt = ".bmp",
                    Filter = "Bitmap Image(*.bmp)|*.bmp",
                };
                var result = dialog.ShowDialog(this);
                if (result == true)
                {
                    mapImage.Save(dialog.FileName);
                }
            }
        }
    }
}
