using System;
using System.Drawing;
using System.Windows.Forms;

namespace ZebraGraphicsConverter
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            if(!DesignMode)
            {
                InitializeForm();
                CreateActions();
            }
        }

        void InitializeForm()
        {

        }

        void CreateActions()
        {
            toolStripMenu.ItemClicked += ToolStripMenu_ItemClicked;
        }

        void OpenImage()
        {
            using (OpenFileDialog dlg = new OpenFileDialog() { Filter = "Image Files(*.BMP; *.JPG; *.PNG)|*.PNG;*.BMP;*.JPG;|Text files(*.txt)|*.txt",
             Title ="Open File", RestoreDirectory = true, Multiselect = false} )
            {
                DialogResult result = dlg.ShowDialog(this);
                if(result == DialogResult.OK)
                {
                    imageDisplaySrc.ClearImage();
                    imageDisplayDest.ClearImage();
                    if (!dlg.FileName.ToUpper().EndsWith(".TXT"))
                    {
                        try
                        {
                            imageDisplaySrc.SetImage(Image.FromFile(dlg.FileName));
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }
                    } else
                    {
                        txtZPL.Text = System.IO.File.ReadAllText(dlg.FileName);
                        ShowZPLGraphics(txtZPL.Text);
                    }
                }
                dlg.Dispose();
            }
        }

        void ShowZPLGraphics(string data)
        {
            imageDisplaySrc.ZPL_ImageCode = data;
            imageDisplaySrc.Convert( Converter.ConversionEnum.ToImage);
        }

        void DoConversion()
        {
            imageDisplayDest.SetImage(imageDisplaySrc.Picture);
            imageDisplayDest.Convert( Converter.ConversionEnum.ToZpl);
            txtZPL.Text = imageDisplayDest.ZPL_ImageCode;
        }

        void RotateImage()
        {
            imageDisplaySrc.Convert(Converter.ConversionEnum.Rotate);
        }

        private void ToolStripMenu_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            Action action;
            switch(e.ClickedItem.Tag?.ToString())
            {
                case "OPEN_IMAGE":
                    action = OpenImage;
                    break;
                case "CONVERT":
                    action = DoConversion;
                    break;
                case "TO_CLIPBOARD":
                    action = () => { Clipboard.SetText(string.IsNullOrEmpty(txtZPL.Text) ? "no data" : txtZPL.Text); };
                    break;
                case "ROTATE":
                    action = RotateImage;
                    break;
                default:
                    action = () => { MessageBox.Show("No action"); };
                    break;
            }
            action();
        }
    }
}
