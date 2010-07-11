using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Navigation;
using System.Threading;

using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Threading;
using System.Windows;
using System.Windows.Input;
namespace Owl
{
	public class KeyboardHandler : IDisposable
	{
		//http://msdn.microsoft.com/en-us/library/ms927178.aspx
		//http://api.farmanager.com/en/winapi/virtualkeycodes.html
		public const int WM_HOTKEY = 0x0312;
		public const int VIRTUALKEYCODE_FOR_CAPS_LOCK = 0x14;
		public const int VK_LWIN = 0x5B;
		public const int VK_MENU = 0x12;
		public const int VK_LCONTROL = 0xA2;
		public const int VK_SPACE = 0x20;
		public const int VK_O = 0x4F;
		public const int VK_F1 = 0x70;
		public const int MOD_WIN = 0x8;
		public const int MOD_NOREPEAT = 0x16;
		public const int MOD_CONTROL = 0x2;
		public const int MOD_SHIFT = 0x4;
		public const int MOD_ALT = 0x1;

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

		private readonly Window _mainWindow;
		WindowInteropHelper _host;

		public KeyboardHandler(Window mainWindow)
		{
			_mainWindow = mainWindow;
			_host = new WindowInteropHelper(_mainWindow);

			SetupHotKey(_host.Handle);
			ComponentDispatcher.ThreadPreprocessMessage += ComponentDispatcher_ThreadPreprocessMessage;
		}
		public event EventHandler HotKeyPressed = null;//delegate { };
		void ComponentDispatcher_ThreadPreprocessMessage(ref MSG msg, ref bool handled)
		{
			if (msg.message == WM_HOTKEY)
			{
				//Handle hot key kere
				if (HotKeyPressed!=null)
					HotKeyPressed(this, EventArgs.Empty);
			}
		}

		private void SetupHotKey(IntPtr handle)
		{
			if (RegisterHotKey(handle, GetType().GetHashCode(), MOD_ALT, VK_F1))
			{
				handle = handle;
			}
		}

		public void Dispose()
		{
			UnregisterHotKey(_host.Handle, GetType().GetHashCode());
		}
	}
    /// <summary>
    /// Interaction logic for Navigation.xaml
    /// </summary>
    public partial class Navigation : NavigationWindow
    {
        private System.Windows.Forms.NotifyIcon m_notifyIcon;
        private System.Drawing.Icon[]   m_owlIcons;
		KeyboardHandler m_hotkey;
        public Navigation()
        {
            InitializeComponent();

            m_owlIcons = new System.Drawing.Icon[6];

            System.IO.Stream iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Owl;component/media/owl.ico")).Stream;
            m_owlIcons[0] = new System.Drawing.Icon(iconStream, 16, 16);//new Size(16,16));
            iconStream.Dispose();
            iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Owl;component/media/owl_2.ico")).Stream;
            m_owlIcons[1] = new System.Drawing.Icon(iconStream, 16, 16);//new Size(16,16));
            iconStream.Dispose();
            iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Owl;component/media/owl_4.ico")).Stream;
            m_owlIcons[2] = new System.Drawing.Icon(iconStream, 16, 16);//new Size(16,16));
            iconStream.Dispose();
            iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Owl;component/media/owl_6.ico")).Stream;
            m_owlIcons[3] = new System.Drawing.Icon(iconStream, 16, 16);//new Size(16,16));
            iconStream.Dispose();
            iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Owl;component/media/owl_8.ico")).Stream;
            m_owlIcons[4] = new System.Drawing.Icon(iconStream, 16, 16);//new Size(16,16));
            iconStream.Dispose();
            iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Owl;component/media/owl_10.ico")).Stream;
            m_owlIcons[5] = new System.Drawing.Icon(iconStream, 16, 16);//new Size(16,16));
            iconStream.Dispose();


            m_notifyIcon = new System.Windows.Forms.NotifyIcon();
            m_notifyIcon.BalloonTipText = "The app has been minimized. Click the tray icon to show.";
            m_notifyIcon.BalloonTipTitle = "Owl";
            m_notifyIcon.Text = "Owl";
            m_notifyIcon.Icon = m_owlIcons[0];            
            //System.Drawing.Icon as a Resource
            //new Icon(GetType(),"Icon1.ico");
            //m_notifyIcon.Icon = new System.Drawing.Icon(new System.Uri("Media/owl.ico"));
            //new System.Drawing.Icon("D:\\Perso\\Dev\\littlebeagle\\LittleBeagle\\Media\\owl.ico");
            m_notifyIcon.Click += new EventHandler(m_notifyIcon_Click);
            m_notifyIcon.MouseMove += new System.Windows.Forms.MouseEventHandler(m_notifyIcon_BalloonTipShown);

            ((Application.Current as App).JobItems).CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(Job_CollectionChanged);

			m_hotkey = new KeyboardHandler(this);
			m_hotkey.HotKeyPressed += m_notifyIcon_Click;
		}
        private Timer _timer = null;
        private int   _icon_frame = 0;
        private void _TimerCallback(object state)
        {
            int index = _icon_frame % 10;			
            //back and forth
            if (index>=5)
                index = 9 - _icon_frame;
			index = index % 6;
            if (m_owlIcons[index]!=null)
                m_notifyIcon.Icon = m_owlIcons[index];
            _icon_frame =(_icon_frame+1) % 10;

            m_notifyIcon_BalloonTipShown(this, null);
        }
        private void Job_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (((Application.Current as App).JobItems).Count > 0)
            {
                if (_timer == null)
                {
                    _timer = new Timer(_TimerCallback, this, 0, 250);
                    _icon_frame = 0;
                }
            }
            else
            {
                if (_timer != null)
                {
                    _timer.Dispose();
                    _timer = null;
                }
                m_notifyIcon.Icon = m_owlIcons[0];
            }

        }


		private void Window_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				WindowState = WindowState.Minimized;
				e.Handled = true;
			}

		}

        private WindowState m_storedWindowState = WindowState.Normal;
        private void Window_StateChanged(object sender, EventArgs e)
        {
			if (WindowState == WindowState.Minimized)
			{
				Hide();
				if (m_notifyIcon != null)
					m_notifyIcon.ShowBalloonTip(100);
			}
			else
			{
				m_storedWindowState = WindowState;
			}
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            CheckTrayIcon();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (_timer!=null)
            {
                _timer.Dispose();
                _timer = null;
            }
            foreach (System.Drawing.Icon ico in m_owlIcons)
            {
                if (ico != null) ico.Dispose();
            }

            m_notifyIcon.Dispose();
            m_notifyIcon = null;

			m_hotkey.Dispose();
			m_hotkey = null;
		

        }

        void m_notifyIcon_Click(object sender, EventArgs e)
        {
			if (WindowState == WindowState.Normal)
				Activate();
			else
				Show();

            WindowState = m_storedWindowState;

			//this.Navigate(new Uri("PageSearch.xaml", UriKind.Relative));
			//Keyboard.Focus(this. SearchField);
			if (this.NavigationService.Content is PageSearch)
			{
				//http://www.budnack.net/Lists/Posts/Post.aspx?ID=21
				PageSearch _page = this.NavigationService.Content as PageSearch;
				_page.SearchField.SelectAll();
				Keyboard.Focus(_page.SearchField);
			}
        }
        void m_notifyIcon_BalloonTipShown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            string hint = "";
            foreach (JobStatus job in (Application.Current as App).jobQueue.jobStatusList)
            {
                if (hint.Length>0)
                    hint+="\n";
                hint+=job.Description;
            }
            if (hint.Length == 0)
                hint = @"Owl";
            m_notifyIcon.Text = hint;
        }
        void CheckTrayIcon()
        {
            ShowTrayIcon(!IsVisible);
        }

        void ShowTrayIcon(bool show)
        {
            if (m_notifyIcon != null)
                m_notifyIcon.Visible = show;
        }
    }
}
