using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.AvalonEdit.Document;
using System.Windows.Data;

namespace SharpMath.UI.Converters
{
    public class AvalonEditDocumentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is string s ? new TextDocument(s) : new TextDocument();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is TextDocument doc ? doc.Text : string.Empty;
        }
    }

}
