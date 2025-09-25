using System.Globalization;

namespace DFIComplianceApp.Converters;

public class BoolToEyeSource : IValueConverter
{
    // when Entry.IsPassword == true  →  show the “closed” icon
    public object Convert(object value, Type _, object __, CultureInfo ___)
        => (value as bool?) == true ? "eye_off.png" : "eye.png";

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => throw new NotImplementedException();
}