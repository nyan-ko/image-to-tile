extern alias fat;

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Reflection;
using System.IO.Compression;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Xna.Framework;
using fat.Terraria;
using SysColor = System.Drawing.Color;

namespace ImageToTile {
    public class Program {
        
        public Program() {

        }

        public const string ImagePath = "images";

        public const string DictionaryPath = "ColorDictionary.dat";

        public const string OutputPath = "schematics";

        public static void Main(string[] args) {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, arg) => {
                string resourceName = new AssemblyName(arg.Name).Name + ".dll";
                string resource = Array.Find(typeof(Program).Assembly.GetManifestResourceNames(), element => element.EndsWith(resourceName));

                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)) {
                    Byte[] assemblyData = new Byte[stream.Length];
                    stream.Read(assemblyData, 0, assemblyData.Length);
                    return Assembly.Load(assemblyData);
                }
            };


            Console.WriteLine("Loading color dictionary...");
            if (!File.Exists(DictionaryPath)) {
                Console.WriteLine("Color dictionary does not exist. Ask nyan for a copy.");
                ProgramBreak();
                return;
            }
            LoadDictionary();
            Console.WriteLine("Loaded color dictionary.");

            Console.WriteLine("Checking for directories...");
            if (!Directory.Exists(ImagePath) || !Directory.Exists(OutputPath)) {

                Console.WriteLine("Directories do not exist; creating them now.");

                Directory.CreateDirectory(ImagePath);
                Directory.CreateDirectory(OutputPath);

                Console.WriteLine("You'll have to run this program again with the images in the valid directories.");
                ProgramBreak();
                return;
            }
            Console.WriteLine("Confirmed directories exist.");

            Console.WriteLine("Loading images...");

            int count = 0;
            
            foreach(var image in new DirectoryInfo(ImagePath).GetFiles()) {
                Bitmap bitmap;
                try {
                    bitmap = new Bitmap(Path.Combine(ImagePath, image.Name));
                }
                catch {
                    Console.WriteLine($"{image.Name} is an invalid image file. Skipping.");
                    continue;
                }

                Console.WriteLine($"Processing image \"{image.Name}\"...");

                string schemName = Path.GetFileNameWithoutExtension(image.FullName) + ".dat";

                using (BinaryWriter writer = new BinaryWriter(new BufferedStream(new GZipStream(File.Open(Path.Combine(OutputPath, schemName), FileMode.Create), CompressionMode.Compress)))) {
                    writer.WriteSection(Utils.GetSectionDataFromImageTileArray(Utils.ProcessBitmapImage(bitmap), bitmap.Width, bitmap.Height));
                }
                count++;
            }

            Console.WriteLine($"Successfully processed {count} images.");

            Application.Run();
        }

        public static void ProgramBreak() {
            Console.WriteLine("Program will terminate with next key press.");
            Console.ReadKey();
        }

        public static void LoadDictionary() {
            WallsByColors = JsonConvert.DeserializeObject<Dictionary<Microsoft.Xna.Framework.Color, ImageWall>>(File.ReadAllText(DictionaryPath));
        }

        public static Dictionary<Microsoft.Xna.Framework.Color, ImageWall> WallsByColors = new Dictionary<Microsoft.Xna.Framework.Color, ImageWall>();
        
    }

    public struct ImageWall {
        public short wallType;
        public byte paintType;
    }

    // Token: 0x02000348 RID: 840
    public class SectionData {  // just copied the entire class from DnSpy here lol
        // Token: 0x060018CD RID: 6349 RVA: 0x0001528F File Offset: 0x0001348F
        public SectionData(int w, int h) {
            this.Width = w;
            this.Height = h;
            this.Tiles = new Tile[this.Width, this.Height];
        }

        // Token: 0x060018CE RID: 6350 RVA: 0x000152BC File Offset: 0x000134BC
        public void ProcessTile(Tile tile, int x, int y) {
            this.Tiles[x, y] = tile;
        }

        // Token: 0x060018CF RID: 6351 RVA: 0x00444178 File Offset: 0x00442378
        public static SectionData SaveSection(int x, int y, int x2, int y2) {
            int w = x2 - x + 1;
            int h = y2 - y + 1;
            SectionData sectionData = new SectionData(w, h) {
                X = x,
                Y = y
            };
            for (int i = x; i <= x2; i++) {
                for (int j = y; j <= y2; j++) {
                    sectionData.ProcessTile((Tile)Main.tile[i, j], i - x, j - y);
                }
            }
            return sectionData;
        }

        // Token: 0x04003E12 RID: 15890
        public int X;

        // Token: 0x04003E13 RID: 15891
        public int Y;

        // Token: 0x04003E14 RID: 15892
        public int Width;

        // Token: 0x04003E15 RID: 15893
        public int Height;

        // Token: 0x04003E16 RID: 15894
        public Tile[,] Tiles;
    }

    public static class Utils {

        // stole most of this code from my old client which compiled everything into illegible variables and syntax
        // initially tried to remove the weird syntax but gave up

        public static void WriteSection(this BinaryWriter writer, SectionData section) {
            writer.Write(section.X);
            writer.Write(section.Y);
            writer.Write(section.Width);
            writer.Write(section.Height);
            for (int x = 0; x < section.Width; x++) {
                for (int y = 0; y < section.Height; y++) {
                    writer.WriteTile(section.Tiles[x, y]);
                }
            }
        }

        // Token: 0x060018D3 RID: 6355 RVA: 0x00444308 File Offset: 0x00442508
        public static void WriteTile(this BinaryWriter writer, Tile tile) {
            writer.Write(tile.sTileHeader);
            writer.Write(tile.bTileHeader);
            writer.Write(tile.bTileHeader2);
            if (tile.active()) {
                writer.Write(tile.type);
                if (Main.tileFrameImportant[(int)tile.type]) {
                    writer.Write(tile.frameX);
                    writer.Write(tile.frameY);
                }
            }
            writer.Write(tile.wall);
            writer.Write(tile.liquid);
        }

        public static SectionData GetSectionDataFromImageTileArray(ImageWall[,] imageTiles, int width, int height) {
            Tile[,] array = new Tile[width, height];
            for (int i = 0; i < width; i++) {
                for (int j = 0; j < height; j++) {
                    ImageWall imageTile = imageTiles[i, j];
                    array[i, j] = new Tile();

                    array[i, j].wall = (byte)imageTile.wallType;
                    array[i, j].wallColor((byte)imageTile.paintType);
                    
                }
            }
            return new SectionData(width, height) {
                Tiles = array,
                X = 0,
                Y = 0
            };
        }

        public static ImageWall[,] ProcessBitmapImage(Bitmap beemap) {
            int width = beemap.Width;
            int height = beemap.Height;
            ImageWall[,] array = new ImageWall[width, height];
            Dictionary<Microsoft.Xna.Framework.Color, ImageWall> dictionary = new Dictionary<Microsoft.Xna.Framework.Color, ImageWall>();  // cached image-color values so we dont have to go through every single pixel, only unique ones
            for (int i = 0; i < width; i++) {
                for (int j = 0; j < height; j++) {
                    Microsoft.Xna.Framework.Color xnaPixel = beemap.GetXnaPixel(i, j);
                    if (dictionary.ContainsKey(xnaPixel)) {
                        array[i, j] = dictionary[xnaPixel];
                    }
                    else {
                        ImageWall imageWall = xnaPixel.GetClosestImageWall();
                        array[i, j] = imageWall;
                        dictionary.Add(xnaPixel, imageWall);
                    }
                }
            }
            return array;
        }

        public static Microsoft.Xna.Framework.Color GetXnaPixel(this Bitmap bitmap, int x, int y) {
            SysColor pixel = bitmap.GetPixel(x, y);
            int num = pixel.R;
            int num2 = pixel.G;
            int num3 = pixel.B;
            int a = pixel.A;
            if (a != 255) {
                float num4 = 1f - a / 255f;
                num = (int)(Math.Abs(num - 255) * num4 + (float)num);
                num2 = (int)(Math.Abs(num2 - 255) * num4 + (float)num2);
                num3 = (int)(Math.Abs(num3 - 255) * num4 + (float)num3);
            }
            return new Microsoft.Xna.Framework.Color(num, num2, num3);
        }

        public static ImageWall GetClosestImageWall(this Microsoft.Xna.Framework.Color color) {
            ImageWall result;
            if (!Program.WallsByColors.TryGetValue(color, out result)) {
                int r = (int)color.R;
                int g = (int)color.G;
                int b = (int)color.B;
                float num = float.MaxValue;
                Microsoft.Xna.Framework.Color key = default(Microsoft.Xna.Framework.Color);
                foreach (var keyValuePair in Program.WallsByColors) {
                    Microsoft.Xna.Framework.Color key2 = keyValuePair.Key;
                    int r2 = (int)key2.R;
                    int g2 = (int)key2.G;
                    int b2 = (int)key2.B;
                    float num2 = (float)(r - r2) * 0.3f;  // weights colours to make them more accurate to the human eye or something
                    float num3 = (float)(g - g2) * 0.59f;
                    float num4 = (float)(b - b2) * 0.11f;
                    float num5 = num2 * num2 + num3 * num3 + num4 * num4;
                    if (num5 < num) {  // find color lowest distance
                        num = num5;
                        key = key2;
                    }
                }
                result = Program.WallsByColors[key];
            }
            return result;
        }
    }
}
