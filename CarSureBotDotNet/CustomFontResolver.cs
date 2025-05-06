using PdfSharp.Fonts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CarSureBotDotNet
{
    public class CustomFontResolver : IFontResolver
    {
        private const string FontName = "TimesNewRoman#";

        public byte[] GetFont(string faceName)
        {
            if (faceName == FontName)
            {
                return LoadFontData("CarSureBotDotNet.Fonts.times.ttf"); // заміни, якщо інший неймспейс
            }

            throw new ArgumentException($"Шрифт '{faceName}' не знайдено.");
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            if (familyName.Equals("Times New Roman", StringComparison.OrdinalIgnoreCase))
            {
                return new FontResolverInfo(FontName);
            }

            return null;
        }

        private byte[] LoadFontData(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new InvalidOperationException($"Не знайдено вбудований шрифт: {resourceName}");

            byte[] data = new byte[stream.Length];
            stream.Read(data, 0, data.Length);
            return data;
        }
    }
}
