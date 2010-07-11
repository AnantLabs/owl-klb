using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;
using System.Windows.Controls;

using System.Runtime.InteropServices;
using IconImage = System.Drawing.Icon;
using System.Windows.Documents;
using System.Windows.Data;
using System.Windows;
using System.Globalization;
using System.Windows.Input;


namespace Owl
{
	
	class PInvokeWin32
	{
		#region Interop SHGetFileInfo
		// Constants that we need in the function call
		public const int SHGFI_ICON = 0x100;
		public const int SHGFI_SMALLICON = 0x1;
		public const int SHGFI_LARGEICON = 0x0;

		//The SHFILEINFO structure is very important as it will be our handle to various file information, among which is the graphic icon.
		// This structure will contain information about the file
		public struct SHFILEINFO
		{
			// Handle to the icon representing the file
			public IntPtr hIcon;
			// Index of the icon within the image list
			public int iIcon;
			// Various attributes of the file
			public uint dwAttributes;
			// Path to the file
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
			public string szDisplayName;
			// File type
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
			public string szTypeName;
		};
		[DllImport("Shell32.dll")]
		public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, int cbFileInfo, uint uFlags);
		#endregion
	}
    //http://social.msdn.microsoft.com/Forums/en-US/wpf/thread/82b30e02-aac4-4564-9e3b-05d5622b9005
    public class CustomTextBlock : TextBlock
    {
        public InlineCollection InlineCollection
        {
            get
            {
                return (InlineCollection)GetValue(InlineCollectionProperty);
            }
            set
            {
                SetValue(InlineCollectionProperty, value);
            }
        }

        public static readonly DependencyProperty InlineCollectionProperty = DependencyProperty.Register(
            "InlineCollection",
            typeof(InlineCollection),
            typeof(CustomTextBlock),
                new UIPropertyMetadata((PropertyChangedCallback)((sender, args) =>
                {
                    CustomTextBlock textBlock = sender as CustomTextBlock;

                    if (textBlock != null)
                    {
                        textBlock.Inlines.Clear();

                        InlineCollection inlines = args.NewValue as InlineCollection;

                        if (inlines != null)
                            textBlock.Inlines.AddRange(inlines.ToList());
                    }
                })));
    }
    public class HtmlConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (!string.IsNullOrEmpty(value as string))
            {
                FlowDocument fd = new FlowDocument();

                string[] text = ((string)value).Split(new char[]{'\\', '/'});

                Paragraph p = new Paragraph();
                string current_dir = "";
                for (int dir_index=0; dir_index<text.Length; dir_index++)
                {
                    Hyperlink link = new Hyperlink(new Run(text[dir_index]));
                    current_dir += text[dir_index]+"\\";
                    link.CommandParameter = current_dir.Clone();
                    link.Command = App.MyExploreCommand;
                    p.Inlines.Add(link);
                    p.Inlines.Add("\\");
                }
                //StringBuilder sb = new StringBuilder();

                //add text and pictures, etc. and return now InlineCollection instead of FlowDocument

                return p.Inlines;
            }
            else
            {
                return new FlowDocument();
            }
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }
    /*
    private void ThreadMain() 
    {
        // obtain the image memory stream
        WebRequest                      request         = WebRequest.Create("http://stackoverflow.com/content/img/so/logo.png");
        WebResponse                     response        = request.GetResponse();
        Stream                          stream          = response.GetResponseStream();

        // create a bitmap source while still in the background thread
        PngBitmapDecoder        decoder                 = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        BitmapFrame                     frame           = decoder.Frames[0];

        // freeze the bitmap source, so that we can pass it to the foreground thread
        BitmapFrame                     frozen          = (BitmapFrame) frame.GetAsFrozen();
        dispatcher.Invoke(new Action(() => { image.Source = frozen; }), new object[] { });
    }
    */

    public class Result : INotifyPropertyChanged
    {
		private static Dictionary<string, BitmapSource> ExtIconDic = new Dictionary<string, BitmapSource>(); 

        private string description;
        private string path;
        public event PropertyChangedEventHandler PropertyChanged;
        #region Properties Getters and Setters
        public string Filename
        {
            get { return System.IO.Path.GetFileName(path);  }
        }
        public string Directory
        {
            get { return System.IO.Path.GetDirectoryName(path); }
        }        
        public string Path
        {
            get { return this.path; }
            set
            {
                this.path = value;
                OnPropertyChanged("Path");
            }
        }
        public string Description
        {
            get { return this.description; }
            set
            {
                this.description = value;
                OnPropertyChanged("Description");
            }
        }
        public BitmapSource Icon
		{
			//TODO: use a background worker to list every list items and load its image
			//http://stackoverflow.com/questions/1738978/loading-image-in-thread-with-wpf/1740387
			//beware thread exception !
			//http://colbycavin.spaces.live.com/blog/cns!5FFDF795EBC7BEDF!173.entry
			get			
			{
				string ext = System.IO.Path.GetExtension(path).ToLower();
				BitmapSource bitmap_source = null;
				if (ExtIconDic.TryGetValue(ext, out bitmap_source))
					return bitmap_source;			
            
                //Get icon
				IconImage ico;
				ico = IconImage.ExtractAssociatedIcon(path);
				/*	
				System.IO.MemoryStream strm = new System.IO.MemoryStream();
				ico.Save(strm);
				IconBitmapDecoder BMPDec = new IconBitmapDecoder(strm, BitmapCreateOptions.None, BitmapCacheOption.Default);
				//myImage.Source = BMPDec.Frames[0];				
                System.Windows.Media.Imaging.BitmapFrame frame = BMPDec.Frames[0];
				strm.Close(); 
                */
                
                System.Drawing.Bitmap bmp = ico.ToBitmap();
                System.IO.MemoryStream strm = new System.IO.MemoryStream();
                bmp.Save(strm, System.Drawing.Imaging.ImageFormat.Png);
                strm.Seek(0, System.IO.SeekOrigin.Begin);
                PngBitmapDecoder pbd = new PngBitmapDecoder(strm, BitmapCreateOptions.None, BitmapCacheOption.Default);
                System.Windows.Media.Imaging.BitmapFrame frame = pbd.Frames[0];
                //frame.Freeze();
				//strm.Close();
                
				ExtIconDic.Add(ext, frame);
				return frame;
			}
            set
            {                
                OnPropertyChanged("Icon");
            }

			 /*
			{
				IntPtr hImgLarge;

				PInvokeWin32.SHFILEINFO shinfo = new PInvokeWin32.SHFILEINFO();
				hImgLarge = PInvokeWin32.SHGetFileInfo(path, 0, ref shinfo, Marshal.SizeOf(shinfo), PInvokeWin32.SHGFI_ICON | PInvokeWin32.SHGFI_LARGEICON);
				// Get the large icon from the handle
				System.Drawing.Icon myIcon = System.Drawing.Icon.FromHandle(shinfo.hIcon);
				// convert the large icon
				System.Drawing.Bitmap bitmap = myIcon.ToBitmap();

				BitmapSource bitSrc = null;
				var hBitmap = bitmap.GetHbitmap();
				try
				{
					//bitSrc = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
					//	hBitmap,
					//	IntPtr.Zero,
					//	System.Windows.Int32Rect.Empty,
					//	BitmapSizeOptions.FromEmptyOptions());
				}
				catch (Win32Exception)
				{
					bitSrc = null;
				}
				finally
				{
					//NativeMethods.DeleteObject(hBitmap);
				}

				return bitSrc;
			}
				*/
		}

        #endregion
        public Result(string description, string path)
        {
            this.description = description;
            this.path = path;
        }
        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
