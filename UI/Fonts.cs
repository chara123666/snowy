using System.Drawing.Text;

namespace Snowy.UI;

internal static class CustomFonts
{
    private static readonly PrivateFontCollection Collection = new();

    public static readonly FontFamily? Lancelot = Load("Lancelot-Regular.ttf");

    private static FontFamily? Load(string fileName)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fonts", fileName);
            if (!File.Exists(path)) return null;

            Collection.AddFontFile(path);
            return Collection.Families.Length > 0 ? Collection.Families[^1] : null;
        }
        catch
        {
            return null;
        }
    }
}
