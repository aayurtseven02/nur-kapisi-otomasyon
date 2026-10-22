using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace AnkaStudio;

// Register in this process only: both native WinForms text and GDI+ use Inter.
internal static class UiFonts
{
    static readonly PrivateFontCollection fonts = new();
    public static readonly FontFamily Family = Load();

    static FontFamily Load()
    {
        foreach (string name in new[] { "Inter-Regular.ttf", "Inter-Bold.ttf" })
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", name);
            if (AddFontResourceEx(path, 0x10, IntPtr.Zero) == 0)
                throw new InvalidOperationException("Arayüz fontu yüklenemedi: " + name);
            fonts.AddFontFile(path);
        }
        return fonts.Families.First(f => f.Name == "Inter");
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern int AddFontResourceEx(string file, uint flags, IntPtr reserved);
}
