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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Owl
{
    /// <summary>
    /// Interaction logic for Page1.xaml
    /// </summary>
    public partial class PageSearch : Page
    {
        public PageSearch()
        {
            InitializeComponent();
        }

        private void SearchField_TextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
        	// TODO: Add event handler implementation here.
	
        }

        private void SearchField_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
        	// TODO: Add event handler implementation here.
	
        }

        private void Search_Click(object sender, System.Windows.RoutedEventArgs e)
        {
        	// TODO: Add event handler implementation here.
			App the_app = (App)Application.Current;
			the_app.Search(SearchField.Text);	
		}

        private void SearchField_TextInput(object sender, RoutedEventArgs e)
        {
            App the_app = (App)Application.Current;
            the_app.Search(SearchField.Text);
        }

		private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			if ((e.Property.Name == "IsVisible") && (e.NewValue.Equals(true)))
			{
				//SearchField.SelectAll();
				//SearchField.ReleaseMouseCapture();
				//Keyboard.Focus(SearchField);
				//SearchField.SelectAll();

			}
		}

    }
}
