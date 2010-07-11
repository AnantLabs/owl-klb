using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Owl
{
	public partial class PageAdmin
	{
		public PageAdmin()
		{
			this.InitializeComponent();

			// Insert code required on object creation below this point.
		}

        private void BuildIndex_Click(object sender, RoutedEventArgs e)
        {
            App my_app = (App)Application.Current;
            my_app.BuildIndex(false);
        }
		private void UpdateIndex_Click(object sender, RoutedEventArgs e)
		{
			App my_app = (App)Application.Current;
			my_app.BuildIndex(true);
		}

		private void Hyperlink_Click(object sender, RoutedEventArgs e)
		{
			App my_app = (App)Application.Current;
			App.MyExploreCommand.Execute(my_app.Infos.LogPath);

		}
	}
}