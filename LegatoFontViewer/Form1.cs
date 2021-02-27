using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LegatoFontViewer
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }



        public struct my_glyph_data
        {
            public UInt16 codepoint;
            public UInt16 width;
            public UInt16 height;
            public UInt16 advance;
            public UInt16 bearingX;
            public UInt16 bearingY;
            public UInt16 flags;
            public UInt16 data_row_width;
            public UInt16 data_table_offset;
            public UInt16 unused;
        }

        private Dictionary<string, string> dict_fonts;

        private bool SelectFontFile()
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                textBox1.Text = openFileDialog1.FileName;
                return true;
            }
            else
            {
                return false;
            }
        }

        private void GetFonts()
        {
            if (string.IsNullOrEmpty(textBox1.Text))
            {
                if (!SelectFontFile())
                    return;
            }
            if (!System.IO.File.Exists(textBox1.Text))
            {
                MessageBox.Show("File not found:\n\n" + textBox1.Text, "Not found", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            comboBox1.Items.Clear();
            comboBox1.Enabled = false;
            numericUpDown1.Enabled = false;
            dict_fonts = new Dictionary<string, string>();
            textBoxFontInfo.Text = "";
            numericUpDown1.Value = 0;

            string file_contents = System.IO.File.ReadAllText(textBox1.Text).Replace("\r\n", "\n"); ;

            string pattern = @"const uint8_t (?<font_name>[\S]+)_data\[(?<font_data_totalbytes>[0-9]+)\] =[^{]+{(?<font_data>[^}]+)}";

            Regex regex = new Regex(pattern, RegexOptions.Multiline);
            MatchCollection colMatches = regex.Matches(file_contents);

            if (colMatches.Count == 0)
            {
                //TODO:  status bar?
                Console.WriteLine("Could not get font data!");
                return;
            }

            comboBox1.Enabled = true;
            numericUpDown1.Enabled = true;

            foreach (Match match in colMatches)
            {
                string font_name = match.Groups["font_name"].Value.ToString();
                string font_data_totalbytes = match.Groups["font_data_totalbytes"].Value.ToString();
                string font_data = match.Groups["font_data"].Value.ToString();
                Console.WriteLine("Font name:   {0}", font_name);
                Console.WriteLine("Total bytes: {0}", font_data_totalbytes);
                //Console.WriteLine("Byte data:   {0}", font_data);

                var bytes = HexStringToByteArray(font_data);

                comboBox1.Items.Add(font_name);

                string font_info = get_font_info(font_name, file_contents);

                font_info = font_info.Replace("\r\n", "\n").Replace("\n", "\r\n");

                dict_fonts.Add(font_name, font_info);
                
            }
                    
        }

        private string get_font_info(string font_name, string file_contents)
        {


            string pattern = @"\/\*[^\/]+"+font_name+@"$[^\/]+\*\/";
            //Console.WriteLine("Pattern: {0}!", pattern);

            Regex regex = new Regex(pattern, RegexOptions.Multiline);

            MatchCollection colMatches = regex.Matches(file_contents);

            if (colMatches.Count == 0)
            {
                Console.WriteLine("No font info for {0}!", font_name);
                return "";
            }
                

            return colMatches[0].Value.ToString();

        }

        private void get_glyph()
        {
            string selected_font = comboBox1.GetItemText(comboBox1.SelectedItem);

            if (string.IsNullOrEmpty(selected_font))
                return;

            string file_contents = System.IO.File.ReadAllText(textBox1.Text).Replace("\r\n","\n");

            string pattern = @"const uint8_t "+ selected_font + @"_data\[(?<font_data_totalbytes>[0-9]+)\] =[^{]+{(?<font_data>[^}]+)}";

            Console.WriteLine(pattern);
            
            Regex regex = new Regex(pattern, RegexOptions.Multiline);
            MatchCollection colMatches = regex.Matches(file_contents);

            if (colMatches.Count == 0)
            {
                //TODO:  status bar?
                Console.WriteLine("Could not get font data!");
                return;
            }

            Match match = colMatches[0];
            string font_data_totalbytes = match.Groups["font_data_totalbytes"].Value.ToString();
            string font_data = match.Groups["font_data"].Value.ToString();
            Console.WriteLine("Total bytes: {0}", font_data_totalbytes);
            //Console.WriteLine("Byte data:   {0}", font_data);

            var bytes = HexStringToByteArray(font_data);

            // First 2 bytes are length
            int total_glyphs = BitConverter.ToInt16(bytes, 0);
            Console.WriteLine("total_glyphs:  {0}", total_glyphs);
            numericUpDown1.Maximum = total_glyphs > 1 ? total_glyphs - 1 : 0;

            //Console.WriteLine("!!!!!!! match:   {0}", font_name);
            //TEST!  get the '!'
            my_glyph_data glyph1 = RawDeserialize<my_glyph_data>(bytes, 4 + Marshal.SizeOf(typeof(my_glyph_data)) * (int)numericUpDown1.Value);
            //4, 24, 44, 64
            Console.WriteLine("codepoint:  {0}", glyph1.codepoint);
            Console.WriteLine("width:  {0}", glyph1.width);
            Console.WriteLine("height:  {0}", glyph1.height);
            Console.WriteLine("data_row_width:  {0}", glyph1.data_row_width);
            Console.WriteLine("data_table_offset:  {0}", glyph1.data_table_offset);
            draw_glyph(file_contents, selected_font, (int)glyph1.width, (int)glyph1.height, (int)glyph1.data_row_width, (int)glyph1.data_table_offset);

        }

        private void draw_glyph(string file_contents, string font_name, int width, int height, int data_row_width, int data_table_offset)
        {
            string pattern = @"const uint8_t "+font_name+@"_glyphs\[(?<glyph_data_totalbytes>[0-9]+)\] =[^{]+{(?<glyph_data>[^}]+)}";

            Regex regex = new Regex(pattern, RegexOptions.Multiline);
            MatchCollection colMatches = regex.Matches(file_contents);

            if (colMatches.Count == 0)
            {
                Console.WriteLine("Could not get glyph data!");
                pictureBox1.Image = null;
                pictureBox2.Image = null;
                return;
            }

            if (width < 1 || height < 1)
            {
                Console.WriteLine("Bad data!");
                pictureBox1.Image = null;
                pictureBox2.Image = null;
                return;
            }

            Bitmap myBitmap = new Bitmap(width, height);

            Match match = colMatches[0];
            string glyph_data_totalbytes = match.Groups["glyph_data_totalbytes"].Value.ToString();
            string glyph_data = match.Groups["glyph_data"].Value.ToString();

            var bytes = HexStringToByteArray(glyph_data);

            Console.WriteLine("glyph_data_totalbytes:  {0}", glyph_data_totalbytes);
            Console.WriteLine("data_table_offset:  {0}", data_table_offset);
            Console.WriteLine("data_row_width:  {0}", data_row_width);
            int blah = height * data_row_width;

            int pos_y = 0;
            for (int i = data_table_offset; i < data_table_offset + blah; i+=data_row_width)
            {
                string row = "";
                for (int n = 0; n < data_row_width; n++)
                {
                    row += Convert.ToString(bytes[i + n], 2).PadLeft(8, '0');
                }

                for (int n = 0; n < width; n++)
                {
                    Color my_color = row.Substring(n, 1) == "1" ? Color.Black : Color.White;
                    int y = i - data_table_offset;
                    myBitmap.SetPixel(n, pos_y, my_color);

                }

                pos_y++;
            }

            pictureBox1.Image = myBitmap;

            using (Bitmap myBitmap_enlarged = (Bitmap)myBitmap.Clone())
            {
                if (myBitmap_enlarged == null)
                {
                    return;
                }
                //InterpolationMode.HighQualityBicubic; // smoother... but not what we want
                double new_height = (double)myBitmap_enlarged.Height;
                double new_width = (double)myBitmap_enlarged.Width;

                double ratio = new_height > new_width ? pictureBox2.Height / new_height : pictureBox2.Width / new_width;
                new_height *= ratio;
                new_width *= ratio;

                Image stretched = StretchedImage(myBitmap_enlarged, (int)new_width, (int)new_height, InterpolationMode.NearestNeighbor);
                pictureBox2.Image = stretched;

            }

            Console.WriteLine();

        }


        private Image StretchedImage(Image imgPhoto, int Width, int Height, InterpolationMode interpolation_mode)
        {
            int sourceWidth = imgPhoto.Width;
            int sourceHeight = imgPhoto.Height;
            int sourceX = 0;
            int sourceY = 0;
            int destX = 0;
            int destY = 0;


            Bitmap bmPhoto = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            bmPhoto.SetResolution(imgPhoto.HorizontalResolution,
                             imgPhoto.VerticalResolution);

            Graphics grPhoto = Graphics.FromImage(bmPhoto);
            //grPhoto.Clear(Color.Red);
            grPhoto.Clear(Color.Empty);

            grPhoto.InterpolationMode = interpolation_mode;

            grPhoto.DrawImage(imgPhoto,
                new Rectangle(destX, destY, Width, Height),
                new Rectangle(sourceX, sourceY, sourceWidth, sourceHeight),
                GraphicsUnit.Pixel);

            grPhoto.Dispose();
            return bmPhoto;
        }

        private void buttonBrowse_Click(object sender, EventArgs e)
        {
            SelectFontFile();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            GetFonts();
        }


        private static byte[] HexStringToByteArray(string hexstring)
        {
            hexstring = hexstring.Replace("0x", "");
            hexstring = Regex.Replace(hexstring, "[^0-9A-F]", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);

            byte[] bytes = new byte[hexstring.Length / 2];

            for (int i = 0; i < hexstring.Length - 1; i += 2)
            {
                string tmp = hexstring.Substring(i, 2);
                bytes[i / 2] = Convert.ToByte(tmp, 16);
            }
            return bytes;
        }

        public static T RawDeserialize<T>(byte[] rawData, int position)
        {
            int rawsize = Marshal.SizeOf(typeof(T));
            if (rawsize > rawData.Length - position)
                throw new ArgumentException("Not enough data to fill struct. Array length from position: " + (rawData.Length - position) + ", Struct length: " + rawsize);
            IntPtr buffer = Marshal.AllocHGlobal(rawsize);
            Marshal.Copy(rawData, position, buffer, rawsize);
            T retobj = (T)Marshal.PtrToStructure(buffer, typeof(T));
            Marshal.FreeHGlobal(buffer);
            return retobj;
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            get_glyph();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.Text = Application.ProductName;
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selected_font = comboBox1.GetItemText(comboBox1.SelectedItem);
            string value = "";

            if (!string.IsNullOrEmpty(selected_font))
            {
                if (dict_fonts.TryGetValue(selected_font, out value))
                {
                    Console.WriteLine("For key = \""+ selected_font+"\", value = {0}.", value);
                }
            }

            textBoxFontInfo.Text = value;
            numericUpDown1.Value = 0;
            pictureBox1.Image = null;
            pictureBox2.Image = null;
        }

        private void buttonGetGlyphs_Click(object sender, EventArgs e)
        {
            get_glyph();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutBox1 about = new AboutBox1();
            about.ShowDialog();

        }
    }
}
