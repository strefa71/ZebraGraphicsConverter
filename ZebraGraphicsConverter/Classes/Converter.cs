using System;
using System.Drawing;
using System.Linq;

namespace ZebraGraphicsConverter
{
    public class Converter
    {

        #region DLL import
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        extern static bool DeleteObject(IntPtr hObject);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        extern static IntPtr GetDC(IntPtr hwnd);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        extern static IntPtr CreateCompatibleDC(IntPtr hdc);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        extern static int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        extern static int DeleteDC(IntPtr hdc);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        extern static IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        extern static int BitBlt(IntPtr hdcDst, int xDst, int yDst, int w, int h, IntPtr hdcSrc, int xSrc, int ySrc, int rop);

        private static int SRCCOPY = 0xCC0020;

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        extern private static IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO bmi, uint Usage, ref IntPtr bits, IntPtr hSection, uint dwOffset);

        private static uint BI_RGB = 0;
        private static uint DIB_RGB_COLORS = 0;
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct BITMAPINFO
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValArray, SizeConst = 256)]
            public uint[] cols;
        }

        private static uint MAKERGB(int r, int g, int b)
        {
            return System.Convert.ToUInt32(b & 255) | System.Convert.ToUInt32((r & 255) << 8) | System.Convert.ToUInt32((g & 255) << 16);
        }
        #endregion

        public Converter(Image image)
        {
            Picture = image;
        }

        public Converter (string zpl)
        {
            ZPL_ImageCode = zpl;
        }

        public enum ConversionEnum
        {
            ToZpl, ToImage, Rotate
        }

        public string ZPL_ImageCode { get; set; }
        public Image Picture { get; set; } = default;

        public bool Convert(ConversionEnum direction = ConversionEnum.ToZpl)
        {
            bool result = true;
            switch (direction)
            {
                case ConversionEnum.ToZpl:
                    result = ConvertToZpl();
                    break;
                case ConversionEnum.ToImage:
                    result = ConvertToImage();
                    break;
                default:
                    break;
            }
            return result;
        }
        //todo: konwersja na 300dpi. Czy wtedy trzeba powiekszyć obrazek?

        private bool ConvertToZpl()
        {
            if (Picture == default)
            {
                ZPL_ImageCode = string.Empty;
                return false;
            }
            Bitmap _image = (Bitmap)Picture.Clone();
            _image.RotateFlip(RotateFlipType.RotateNoneFlipY);
            Bitmap bmp = CopyToBpp(_image, 1);
            byte[] bitmapFileData;
            int bitmapDataOffset = 62;
            int width = _image.Width;
            int height = _image.Height;
            double widthInBytes = Math.Ceiling(width / 8.0);
            //create array of bytes
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
            {
                Picture.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                bitmapFileData = ms.ToArray();
                ms.Close();
            }
            //release resources
            bmp.Dispose();
            _image.Dispose();

            int bitmapDataLength = bitmapFileData.Length;
            //Copy over the actual bitmap data from the bitmap file.
            //This represents the bitmap data without the header information.
            int bitmapDataSize = bitmapDataLength - bitmapDataOffset;
            byte[] bitmap = new byte[bitmapDataSize];
            Buffer.BlockCopy(bitmapFileData, bitmapDataOffset, bitmap, 0, bitmapDataSize);
            //Invert bitmap colors
            for (int i = 0; i < bitmapDataSize - 1; i++)
            {
                bitmap[i] = (byte)(bitmap[i] ^ 0xFF);
            }

            string ZPLImageDataString = BitConverter.ToString(bitmap).Replace("-", String.Empty);
            //final ZPL command
            ZPL_ImageCode = $"^GFA,{bitmapDataSize},{widthInBytes * height},{widthInBytes},{ZPLImageDataString}";
            return true;
        }

        /// <summary>
        /// Converts from ZPL to image
        /// </summary>
        private bool ConvertToImage()
        {
            //first, check the code is valid
            if (!CheckZplCode())
            {
                Picture = default;
                return false;
            }
            int widthInBytes = int.Parse(ZPL_ImageCode.Split(',')[3]);
            int TotalBytes = int.Parse(ZPL_ImageCode.Split(',')[2]);
            int h = (int)Math.Ceiling((decimal)TotalBytes / widthInBytes);
            int w = widthInBytes * 8;
            string t = ZPL_ImageCode.Split(',')[4]; //image data
            byte[] bitmap = new byte[TotalBytes * 2 + 1];
            int y = 0;
            for (int i = 0; i <= t.Length - 2; i += 2)
            {
                bitmap[y] = (byte)(System.Convert.ToUInt32(t.Substring(i, 2), 16) ^ 0xFF);
                y += 1;
            }
            Bitmap bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format1bppIndexed);
            System.Drawing.Imaging.BitmapData bmData = bmp.LockBits(new Rectangle(0, 0, w, h), System.Drawing.Imaging.ImageLockMode.ReadWrite, bmp.PixelFormat);
            IntPtr pNative = bmData.Scan0;
            System.Runtime.InteropServices.Marshal.Copy(bitmap, 0, pNative, widthInBytes * h);
            bmp.UnlockBits(bmData);
            bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
            Picture = (Bitmap)bmp.Clone();
            bmp.Dispose();
            return true;
        }

        bool CheckZplCode()
        {
            bool result = !string.IsNullOrEmpty(ZPL_ImageCode);
            if (result)
            {
                System.Collections.ArrayList results = new System.Collections.ArrayList();
                if (ZPL_ImageCode.Split(',').Length == 5)
                {
                    results.Add(ZPL_ImageCode.Split(',')[0].Equals("^GFA"));
                    int x;
                    results.Add(int.TryParse(ZPL_ImageCode.Split(',')[1], out x));
                    results.Add(int.TryParse(ZPL_ImageCode.Split(',')[2], out x));
                    results.Add(int.TryParse(ZPL_ImageCode.Split(',')[3], out x));
                } else 
                    results.Add(false);

                result = !results.ToArray().Any(x => (bool)x == false);
            }
            return result;
        }

        public void ToGrayscale()
        {
            if (Picture.PixelFormat != System.Drawing.Imaging.PixelFormat.Format24bppRgb)
            {
                Picture = AForge.Imaging.Image.Clone((Bitmap)Picture, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                if (!AForge.Imaging.Image.IsGrayscale((Bitmap)Picture))
                    Picture = AForge.Imaging.Filters.Grayscale.CommonAlgorithms.BT709.Apply((Bitmap)Picture);
            }
        }
        /// <summary>
        /// Expand the width of image multiplied by 32
        /// </summary>
        public void Expand()
        {
            int w = (int) Math.Ceiling(Picture.Width / 32.0) * 32;
            int h = (int) Math.Ceiling(Picture.Height / 8.0) * 8;
            Bitmap b = new Bitmap(Picture, w, h);
            Graphics gr = Graphics.FromImage(b);
            gr.FillRectangle(Brushes.White, 0, 0, b.Width, b.Height);
            gr.DrawImage(Picture, new Point(0, 0));
            Picture = (Bitmap)b.Clone();
            //relase resources
            b.Dispose();
            gr.Dispose();
        }
        /// <summary>
        /// Change the size based of width
        /// </summary>
        /// <param name="new_width"></param>
        public void Resize(int new_width)
        {
            double ratio = Picture.Width / Picture.Height;
            AForge.Imaging.Filters.ResizeBilinear filter = new AForge.Imaging.Filters.ResizeBilinear(new_width, (int)Math.Ceiling(new_width / ratio));
            Picture = filter.Apply((Bitmap)Picture);
            Expand();
        }

        public void Rotate(RotateFlipType flipType = RotateFlipType.Rotate90FlipXY)
        {
            Picture.RotateFlip(flipType);
            Expand();
        }
        /// <summary>
        /// Shrink image based on white color as default
        /// </summary>
        public void Shrink(Color? color = null)
        {
            AForge.Imaging.Filters.Shrink filter = new AForge.Imaging.Filters.Shrink(color ?? Color.White);
            Picture = filter.Apply((Bitmap)Picture);
            Expand();
        }

        /// <summary>
        /// Dither 8bpp grayscale image
        /// </summary>
        public void Dither()
        {
            if (!AForge.Imaging.Image.IsGrayscale((Bitmap)Picture))
                return;
            AForge.Imaging.Filters.FloydSteinbergDithering filter = new AForge.Imaging.Filters.FloydSteinbergDithering();
            Picture = filter.Apply((Bitmap)Picture);
            //Expand();
        }

        /// <summary>
        /// Treshold image to 1bpp bitmap
        /// </summary>
        public void Treshold()
        {
            Picture = CopyToBpp((Bitmap)Picture, 1);
        }



        /// <summary>
        ///     ''' Copies a bitmap into a 1bpp/8bpp bitmap of the same dimensions, fast
        ///     ''' </summary>
        ///     ''' <param name="b">original bitmap</param>
        ///     ''' <param name="bpp">1 or 8, target bpp</param>
        ///     ''' <returns>a 1bpp copy of the bitmap</returns>
        private Bitmap CopyToBpp(Bitmap b, int bpp)
        {
            if (bpp != 1 && bpp != 8)
                throw new System.ArgumentException("1 or 8", "bpp");
            int w = b.Width;

            // Plan: built into Windows GDI is the ability to convert
            // bitmaps from one format to another. Most of the time, this
            // job is actually done by the graphics hardware accelerator card
            // and so is extremely fast. The rest of the time, the job is done by
            // very fast native code.
            // We will call into this GDI functionality from C#. Our plan:
            // (1) Convert our Bitmap into a GDI hbitmap (ie. copy unmanaged->managed)
            // (2) Create a GDI monochrome hbitmap
            // (3) Use GDI "BitBlt" function to copy from hbitmap into monochrome (as above)
            // (4) Convert the monochrone hbitmap into a Bitmap (ie. copy unmanaged->managed)

            int h = b.Height;
            IntPtr hbm = b.GetHbitmap();
            // this is step (1)
            // 
            // Step (2): create the monochrome bitmap.
            // "BITMAPINFO" is an interop-struct which we define below.
            // In GDI terms, it's a BITMAPHEADERINFO followed by an array of two RGBQUADs
            BITMAPINFO bmi = new BITMAPINFO();
            bmi.biSize = 40;
            // the size of the BITMAPHEADERINFO struct
            bmi.biWidth = w;
            bmi.biHeight = h;
            bmi.biPlanes = 1;
            // "planes" are confusing. We always use just 1. Read MSDN for more info.
            bmi.biBitCount = System.Convert.ToInt16(bpp);
            // ie. 1bpp or 8bpp
            bmi.biCompression = BI_RGB;
            // ie. the pixels in our RGBQUAD table are stored as RGBs, not palette indexes
            bmi.biSizeImage = System.Convert.ToUInt32(((w + 7) & 0xFFFFFFF8U) * h / (double)8);
            bmi.biXPelsPerMeter = 1000000;
            // not really important
            bmi.biYPelsPerMeter = 1000000;
            // not really important
            // Now for the colour table.
            uint ncols = System.Convert.ToUInt32(1) << bpp;
            // 2 colours for 1bpp; 256 colours for 8bpp
            bmi.biClrUsed = ncols;
            bmi.biClrImportant = ncols;
            bmi.cols = new uint[256];
            // The structure always has fixed size 256, even if we end up using fewer colours
            if (bpp == 1)
            {
                bmi.cols[0] = MAKERGB(0, 0, 0);
                bmi.cols[1] = MAKERGB(255, 255, 255);
            }
            else
                for (int i = 0; i <= ncols - 1; i++)
                    bmi.cols[i] = MAKERGB(i, i, i);
            // For 8bpp we've created an palette with just greyscale colours.
            // You can set up any palette you want here. Here are some possibilities:
            // greyscale: for (int i=0; i<256; i++) bmi.cols[i]=MAKERGB(i,i,i);
            // rainbow: bmi.biClrUsed=216; bmi.biClrImportant=216; int[] colv=new int[6]{0,51,102,153,204,255};
            // for (int i=0; i<216; i++) bmi.cols[i]=MAKERGB(colv[i/36],colv[(i/6)%6],colv[i%6]);
            // optimal: a difficult topic: http://en.wikipedia.org/wiki/Color_quantization
            // 
            // Now create the indexed bitmap "hbm0"
            IntPtr bits0 = IntPtr.Zero;
            // not used for our purposes. It returns a pointer to the raw bits that make up the bitmap.
            IntPtr hbm0 = CreateDIBSection(IntPtr.Zero, ref bmi, DIB_RGB_COLORS, ref bits0, IntPtr.Zero, 0);
            // 
            // Step (3): use GDI's BitBlt function to copy from original hbitmap into monocrhome bitmap
            // GDI programming is kind of confusing... nb. The GDI equivalent of "Graphics" is called a "DC".
            IntPtr sdc = GetDC(IntPtr.Zero);
            // First we obtain the DC for the screen
            // Next, create a DC for the original hbitmap
            IntPtr hdc = CreateCompatibleDC(sdc);
            SelectObject(hdc, hbm);
            // and create a DC for the monochrome hbitmap
            IntPtr hdc0 = CreateCompatibleDC(sdc);
            SelectObject(hdc0, hbm0);
            // Now we can do the BitBlt:
            BitBlt(hdc0, 0, 0, w, h, hdc, 0, 0, SRCCOPY);
            // Step (4): convert this monochrome hbitmap back into a Bitmap:
            System.Drawing.Bitmap b0 = System.Drawing.Bitmap.FromHbitmap(hbm0);
            // 
            // Finally some cleanup.
            DeleteDC(hdc);
            DeleteDC(hdc0);
            ReleaseDC(IntPtr.Zero, sdc);
            DeleteObject(hbm);
            DeleteObject(hbm0);
            // 
            return b0;
        }
    }
}
