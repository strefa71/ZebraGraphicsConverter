using System;
using System.Drawing;
using System.Windows.Forms;

namespace ZebraGraphicsConverter.Controls
{
    public partial class ImageDisplayCtl : UserControl
    {
        public ImageDisplayCtl()
        {
            InitializeComponent();
        }

        public string ZPL_ImageCode { get;  set; }
        public Image Picture {  get
            {
                return pictureBox1.Image;
            } }

        public void ClearImage()
        {
            pictureBox1.Image = default;
        }

        public void SetImage(Image image)
        {
            pictureBox1.Image = image;
            txtWidth.Text = image?.Width.ToString();
            txtHeight.Text = image?.Height.ToString();
        }

        public void Convert(Converter.ConversionEnum direction)
        {
            Action action;
            switch (direction)
            {
                case Converter.ConversionEnum.ToZpl:
                    action = ToZpl;
                    break;
                case Converter.ConversionEnum.ToImage:
                    action = ToImage;
                    break;
                case Converter.ConversionEnum.Rotate:
                    action = Rotate;
                    break;
                default:
                    action = () => { };
                    break;
            }
            action();
        }

        void ToImage()
        {
            Converter converter;
            converter = new Converter(ZPL_ImageCode);
            if (converter.Convert(Converter.ConversionEnum.ToImage))
            {
                SetImage(converter.Picture);
            }
            else
            {
                pictureBox1.Image = Properties.Resources.error;
            };
        }

        void ToZpl()
        {
            if(pictureBox1.Image == default)
            {
                ZPL_ImageCode = "Please open image first";
                pictureBox1.Image = Properties.Resources.error;
                return;
            }
            Converter converter;
            converter = new Converter(pictureBox1.Image);
            converter.ToGrayscale();//first, convert to gray
            converter.Treshold(); //second, convert to 1bpp
            pictureBox1.Image = converter.Picture; //show picture
            if (converter.Convert(Converter.ConversionEnum.ToZpl))
            {
                ZPL_ImageCode = converter.ZPL_ImageCode;
            }
            else
            {
                ZPL_ImageCode = "Error during conversion.";
                pictureBox1.Image = Properties.Resources.error;
            };

        }

        void Rotate()
        {
            Converter converter = new Converter(pictureBox1.Image);
            converter.Rotate(RotateFlipType.Rotate90FlipNone);
            pictureBox1.Image = converter.Picture;
        }
    }
}
