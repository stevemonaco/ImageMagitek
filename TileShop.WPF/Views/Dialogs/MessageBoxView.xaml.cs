using System;
using System.Windows;

namespace TileShop.WPF.Views
{
    /// <summary>
    /// Interaction logic for MessageBoxView.xaml
    /// </summary>
    public partial class MessageBoxView : Window
    {
        public MessageBoxView()
        {
            InitializeComponent();
        }

        private void RootObject_Activated(object sender, EventArgs e)
        {
            InvalidateMeasure();
        }
    }
}
