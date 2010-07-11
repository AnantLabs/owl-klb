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
using CustomWindow;

namespace LittleBeagle
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
    /// 
	
#if false    
    public partial class MainWindow : StandardWindow
#else
    public partial class MainWindow : Window
#endif
    {
		public MainWindow()
		{
			this.InitializeComponent();

			// Insert code required on object creation below this point.
		}

		private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			// TODO: Add event handler implementation here.
			App MyApplication = ((App)Application.Current);
            MyApplication.BuildIndex();
		}

		private void SearchField_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
		{
			// TODO: Add event handler implementation here.
            App MyApplication = ((App)Application.Current);
            MyApplication.Search(SearchField.Text);
		}
	}
}