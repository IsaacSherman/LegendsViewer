using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.Configuration;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using LegendsViewer;
using LegendsViewer.Controls.Map;

namespace TestHarness
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();

        }

        public class NoSmoothingPictureBox : PictureBox
        {
            protected override void OnPaint(PaintEventArgs pe)
            {
                pe.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                base.OnPaint(pe);
            }
        }


        private Bitmap map;
        private MapModel model;
        private void Button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog dg = new OpenFileDialog { Filter = "bmp files (*.bmp)|*.bmp|All files (*.*)|*.*" };
            if (dg.ShowDialog() != DialogResult.OK) return;

            textBox1.Text = dg.SafeFileName;
            map = new Bitmap(dg.FileName);
            model = new MapModel(map);
            BackgroundWorker bg = new BackgroundWorker();
            bg.DoWork += (o, args) => model.DoAnalysis();
            bg.RunWorkerCompleted += AnalysisComplete;
            bg.RunWorkerAsync();
            lbInfo.Text = "Analyzing.";
        }

        private List<MapModel.TerrainTile> TileList;
        private List<Bitmap> bitmaps = new List<Bitmap>();
        private void AnalysisComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            TileList = model.Tiles.ToList();
            TileList.Sort();
            lbInfo.Invoke((Action)(() => lbInfo.Text = "Analysis Complete"));
            CreateBitmaps();

            ShowMapControls();
            SetIndex(0);
        }

        private void ShowMapControls(bool b=true)
        {
            pictureBox1.Visible = btnNext.Visible = btnPrev.Visible = cbTerrainAlignment.Visible =
                tbTerrainName.Visible = label2.Visible = label3.Visible = btnSet.Visible = b;

        }

        /// <summary>
        /// The Bitmaps and tiles are loosely synced via index= sorting the tiles after calling this method will break that sync.
        /// </summary>
        private void CreateBitmaps()
        {
            if (bitmaps != null) bitmaps = new List<Bitmap>();//Redraw every time
            foreach (MapModel.TerrainTile terrainTile in TileList)
            {
                Bitmap temp = new Bitmap(MapModel.TileWidth, MapModel.TileHeight);
                for (int i = 0; i < terrainTile.TileShape.Length; i++)
                {
                    bool colored = terrainTile.TileShape[i]; // == '1';
                    temp.SetPixel(i % MapModel.TileWidth, i / MapModel.TileWidth,
                        colored ? terrainTile.TileColor : MapModel.DFBlack);
                }

                bitmaps.Add(temp);
                //Now bitmaps and TileList are synced.
            }
        }

        private int index;
        private void SetIndex(int newIndex)
        {
            Debug.Assert(TileList != null);
            if (index > TileList.Count || index < 0) return;
            index = newIndex;
            btnPrev.Enabled = index > 0;
            btnNext.Enabled = index < TileList.Count - 1;
            pictureBox1.Image = bitmaps[index];
            cbTerrainAlignment.SelectedIndex = (int)TileList[index].Alignment;
            tbTerrainName.Text = TileList[index].Name ?? "";
        }

        private void BtnSet_Click(object sender, EventArgs e)
        {
            TileList[index].Name = tbTerrainName.Text;
            TileList[index].Alignment = (MapModel.TerrainAlignment)cbTerrainAlignment.SelectedIndex;

            //if (TileList.All((x) => x.Name != "" && x.Alignment != MapModel.TerrainAlignment.Unset))
            // {
            btnFinish.Visible = true;
            // }
        }

        private void BtnNext_Click(object sender, EventArgs e)
        {
            SetIndex(index + 1);
        }

        private void BtnPrev_Click(object sender, EventArgs e)
        {
            SetIndex(index - 1);
        }

        private const string ShapeKey = "Shape", AlignKey = "Alignment", ColorKey = "Color", NameKey = "Name";
        private void BtnFinish_Click(object sender, EventArgs e)
        {

            XmlDocument doc = new XmlDocument();
            doc.AppendChild(doc.CreateElement("Document"));
            foreach (MapModel.TerrainTile terrainTile in TileList)
            {
                XmlElement tile = doc.CreateElement("TerrainTile");
                string bits = terrainTile.TileShape.Cast<bool>().Aggregate("", (current, b) => current + (b ? "1" : "0"));
                tile.SetAttribute(ShapeKey, bits);
                tile.SetAttribute(AlignKey, terrainTile.Alignment.ToString());
                bits = $"{terrainTile.TileColor.ToArgb():X8}";
                tile.SetAttribute(ColorKey, bits);
                tile.SetAttribute(NameKey, terrainTile.Name);
                doc.DocumentElement.AppendChild(tile);
            }
            doc.Save(@".\baseTileSet.xml");

        }

        private void Button2_Click(object sender, EventArgs e)
        {

            OpenFileDialog dg = new OpenFileDialog { Filter = "xml files (*.xml)|*.xml|All files (*.*)|*.*" };
            if (dg.ShowDialog() != DialogResult.OK) return;
            HashSet<MapModel.TerrainTile> TileSet;
            bool mapAnalyzed = true;
            if ((TileList?.Count ?? 0) == 0)
            {
                mapAnalyzed = false;
                TileSet = new HashSet<MapModel.TerrainTile>();

            }
            else
            {
                TileSet = new HashSet<MapModel.TerrainTile>(TileList);
            }
            TileList = new List<MapModel.TerrainTile>();
            XmlDocument doc = new XmlDocument();
            doc.Load(dg.FileName);
            foreach (XmlElement node in doc.ChildNodes[0].ChildNodes)
            {
                if (node.Name != "TerrainTile") continue;
                MapModel.TerrainTile temp = new MapModel.TerrainTile(node.Attributes[ShapeKey].Value,
                    node.Attributes[AlignKey].Value, node.Attributes[NameKey].Value,
                    node.Attributes[ColorKey].Value);
                if (mapAnalyzed && TileSet.Contains(temp)) continue;
                TileList.Add(temp);
                TileSet.Add(temp);
            }

            TileList.Sort();
            CreateBitmaps();
            ShowMapControls();
            SetIndex(0);
        }
    }
}
