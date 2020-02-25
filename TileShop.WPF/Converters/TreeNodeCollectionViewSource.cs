using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace TileShop.WPF.Converters
{
    public class TreeNodeCollectionViewSource : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var cvs = new CollectionViewSource() { Source = value };

            var sortPriority = new SortDescription("SortPriority", ListSortDirection.Ascending);
            var sortAlphabetical = new SortDescription("Name", ListSortDirection.Ascending);
            cvs.SortDescriptions.Add(sortPriority);
            cvs.SortDescriptions.Add(sortAlphabetical);

            return cvs.View;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
