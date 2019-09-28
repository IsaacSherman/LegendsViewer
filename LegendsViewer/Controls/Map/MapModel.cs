using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using WFC;

namespace LegendsViewer
{

    /* So here's my algorithm.  Basically, the map is made up of rectangles. Each rectangle is the same size 8x12 or 96 pixels.
   * Each rectangle is made up of 3 bytes.  RGB.  I'll create a bitarray of 96 and for each pixel that isn't 0 0 0 (0) to 1.  
   * That, combined with a color, gives me a rasterized and efficient means of storing each rectangle.  These can also be more easily queried.
   * Each shape is going to have a specific pattern of bits.  Because of that, I can further distill the bitarray down into an enumeration of shapes.  With that, 
   * I literally have a map as a representation of colors and terrain types.
   * The colors can be, not always cleanly, be distilled into the neutral, good, and evil counterparts. For instance, Good Mountains are yellow,
   * but yellow deserts are neutral.  That gives me what more or less the raw map that 
   * must live in DF somewhere- terrain and alignment (and coordinates) for every cell on the map. 
   * There are a few complications.  Roads and rivers have several different shapes for connecting different squares.  So those shapes will
   * need to be something like pathNS, pathNE.  They'll need the 15 different shapes, I think, since it's the superset of NESW without all set to none.
   * So first I'll write code to build my dictionary of shapes.  Once we have that, I'll use https://dwarffortresswiki.org/index.php/DF2014:Map_legend to implement
   * the rest of the alignment stuff and catch any shapes that don't exist on my map.
   * Once that's done, the rest is to add a button to legends viewer that lets you redraw your map.  
   * So what do we need to represent a tile?  Color, coordinate, "shape"...
     *
     * 11111111 XOR with
     * 00111000 Yields
     * 11000111
     *
     *00111000
     *00111000
     *
   */

    public class MapModel
    {
        private readonly Bitmap _map;

        public enum TerrainAlignment
        {
            Unset = 0,
            Good,
            Neutral,
            Evil,
            Ruins,
            Civilization,
            None,
        }

        public class TerrainTile : IEquatable<TerrainTile>, IComparable<TerrainTile>
        {
            public Color TileColor;
            public BitArray TileShape;
            public string Name = "";
            public TerrainAlignment Alignment;

            private const string noShape =
                "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000";

            public TerrainTile() : this(noShape, "Unset", "", "FFFFFFFF")
            {
                
            }

            public TerrainTile(string shape, string alignment, string name, string color)
            {
                TileShape = new BitArray(96);
                for (int i = 0; i < shape.Length; i++)
                {
                    TileShape[i] = shape[i] == '1';
                }

                switch (alignment)
                {
                    case "Good": Alignment = TerrainAlignment.Good;                   break;
                    case "Neutral": Alignment = TerrainAlignment.Neutral;             break;
                    case "Evil": Alignment = TerrainAlignment.Evil;                   break;
                    case "Ruins": Alignment = TerrainAlignment.Ruins;                 break;
                    case "Civilization": Alignment = TerrainAlignment.Civilization;   break;
                    case "None": Alignment = TerrainAlignment.None;                   break;
                    default:
                        Alignment = TerrainAlignment.Unset;
                        break;
                }

                Name = name;
                short r = Convert.ToInt16(color.Substring(2, 2), 16),
                    g =   Convert.ToInt16(color.Substring(4, 2), 16),
                    b =   Convert.ToInt16(color.Substring(6, 2), 16);

                TileColor = Color.FromArgb(255, r, g, b);
            }

            public bool Equals(TerrainTile other)
            {
                if (other == null)
                    throw new NullReferenceException("Comparison to null in TerrainTile");
                int[] array = new int[3], arrayOther = new int[3];
                TileShape.CopyTo(array, 0);
                other.TileShape.CopyTo(arrayOther, 0);
                //bool ret = other.TileShape == TileShape;
                bool ret = TileColor == other.TileColor;
                for (int i = 0; i < 3; ++i)
                {
                    ret &= arrayOther[i] == array[i];
                }

                return ret;
            }
            public int CompareTo(TerrainTile other)
            {
                int[] array = new int[3], arrayOther = new int[3];
                TileShape.CopyTo(array, 0);
                other.TileShape.CopyTo(arrayOther, 0);

                int comp = array[0].CompareTo(arrayOther[0]);
                if (comp != 0) return comp;
                comp = array[1].CompareTo(arrayOther[1]);
                if (comp != 0) return comp;
                comp = array[2].CompareTo(arrayOther[2]);
                return comp != 0 ? comp : TileColor.ToArgb().CompareTo(other.TileColor.ToArgb());
            }
        }


        public class TerrainTileComparer : IEqualityComparer<TerrainTile>
        {

            public bool Equals(TerrainTile x, TerrainTile y)
            {
                return x?.Equals(y) ?? false;
            }

            public int GetHashCode(TerrainTile obj)
            {
                int code = obj.TileColor.GetHashCode() * 3;
                code += obj.Alignment.GetHashCode() * 5;
                code += obj.Name.GetHashCode() * 7;
                var array = new int[3];
                obj.TileShape.CopyTo(array, 0);
                code ^= array[0] * 11;
                code ^= array[1] * 13;
                code ^= array[2] * 17;
                return code;
            }

        }


        public MapModel(Bitmap map)
        {
            _map = map;

        }

        public HashSet<TerrainTile> Tiles { get; } = new HashSet<TerrainTile>(new TerrainTileComparer());
        public static readonly Color DFBlack = Color.FromArgb(255, 32, 39, 49);
        public const int TileWidth = 8, TileHeight = 12;

        public void DoAnalysis()
        {
            int count = 0, numberAdded = 0;
            if (_map == null) return;
            for (int y = 0; y < _map.Size.Height; y += TileHeight)
            {
                for (int x = 0; x < _map.Size.Width; x += TileWidth)
                {
                    Color tileColor = DFBlack;
                    // string tile = ""; 
                    BitArray tile = new BitArray(TileHeight * TileWidth);
                    for (int a = 0; a < TileHeight; a++)
                    {
                        for (int b = 0; b < TileWidth; b++)
                        {
                            Color pixelColor = _map.GetPixel(x + b, y + a);
                            if (pixelColor != DFBlack)
                            {
                                tileColor = pixelColor;
                            }

                            //tile= pixelColor != DFBlack ? "1" : "0";
                            tile[a * TileWidth + b] = pixelColor != DFBlack;
                        }
                    }

                    //Tile done
                    bool added = Tiles.Add(new TerrainTile { TileColor = tileColor, TileShape = tile, Name = "", Alignment = TerrainAlignment.Unset });
                    if (added) numberAdded++;
                    count++;

                }

                //row done
            }
        }
    }
}
