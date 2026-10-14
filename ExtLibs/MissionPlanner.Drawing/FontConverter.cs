using System;
using System.ComponentModel;
using System.Globalization;

namespace System.Drawing
{
    // Resources built with GenerateResourceUsePreserializedResources store fonts as
    // strings like "Microsoft Sans Serif, 8.25pt, style=Bold" and rebuild them at
    // runtime through this converter.
    public class FontConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (!(value is string text))
                return base.ConvertFrom(context, culture, value);

            text = text.Trim();
            if (text.Length == 0)
                return null;

            if (culture == null)
                culture = CultureInfo.CurrentCulture;

            var separator = culture.TextInfo.ListSeparator[0];
            var parts = text.Split(separator);

            var name = parts[0].Trim();
            var size = 8.25f;
            var unit = GraphicsUnit.Point;
            var style = FontStyle.Regular;

            if (parts.Length > 1)
                ParseSize(parts[1].Trim(), culture, ref size, ref unit);

            // "style=Bold, Italic" spans the remaining parts
            for (var i = 2; i < parts.Length; i++)
            {
                var part = parts[i].Trim();
                var eq = part.IndexOf('=');
                if (eq >= 0)
                    part = part.Substring(eq + 1).Trim();
                if (part.Length > 0)
                    style |= (FontStyle)Enum.Parse(typeof(FontStyle), part, true);
            }

            return new Font(name, size, style, unit);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value,
            Type destinationType)
        {
            if (destinationType == typeof(string) && value is Font font)
            {
                if (culture == null)
                    culture = CultureInfo.CurrentCulture;

                var sep = culture.TextInfo.ListSeparator + " ";
                var text = font.Name + sep + font.Size.ToString(culture) + UnitSuffix(font.Unit);
                if (font.Style != FontStyle.Regular)
                    text += sep + "style=" + font.Style.ToString().Replace(",", sep.TrimEnd());
                return text;
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }

        private static void ParseSize(string text, CultureInfo culture, ref float size, ref GraphicsUnit unit)
        {
            var end = 0;
            while (end < text.Length && (char.IsDigit(text[end]) || text[end] == '.' || text[end] == ',' ||
                                         text[end] == '-' || text[end] == '+'))
                end++;

            if (end > 0)
                size = float.Parse(text.Substring(0, end), NumberStyles.Float, culture);

            switch (text.Substring(end).Trim().ToLowerInvariant())
            {
                case "px": unit = GraphicsUnit.Pixel; break;
                case "world": unit = GraphicsUnit.World; break;
                case "display": unit = GraphicsUnit.Display; break;
                case "in": unit = GraphicsUnit.Inch; break;
                case "doc": unit = GraphicsUnit.Document; break;
                case "mm": unit = GraphicsUnit.Millimeter; break;
                default: unit = GraphicsUnit.Point; break;
            }
        }

        private static string UnitSuffix(GraphicsUnit unit)
        {
            switch (unit)
            {
                case GraphicsUnit.Pixel: return "px";
                case GraphicsUnit.World: return "world";
                case GraphicsUnit.Display: return "display";
                case GraphicsUnit.Inch: return "in";
                case GraphicsUnit.Document: return "doc";
                case GraphicsUnit.Millimeter: return "mm";
                default: return "pt";
            }
        }
    }
}
