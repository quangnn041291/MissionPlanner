using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;

namespace System.Drawing
{
    // Resources built with GenerateResourceUsePreserializedResources store icons as
    // .ico bytes and rebuild them at runtime through this converter.
    public class IconConverter : ImageConverter
    {
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is byte[] bytes)
                return new Icon(new MemoryStream(bytes));

            return base.ConvertFrom(context, culture, value);
        }
    }
}
