namespace AnkaStudio;

public sealed partial class StudioForm
{
    public void CaptureDarkReference(string folder)
    {
        Directory.CreateDirectory(folder);
        TopMost=true; if(!Visible) Show(); Activate();
        var before=System.Text.Json.JsonSerializer.Serialize(new {store.Settings,store.Episodes},Json.Options);
        foreach(int width in new[]{1120,1440}) {
            Width=width;Height=900;
            foreach(string route in new[]{"Video hazırla","Videolarım","Bağlantı Merkezi","Yönetim ayarları","Medya arşivleri","İşlem kayıtları"}) {
                Navigate(route);PerformLayout();Application.DoEvents();Refresh();
                if(Theme.Bg.GetBrightness()>.15 || Theme.Text.GetBrightness()<.85)
                    throw new InvalidOperationException("Referans koyu tema ve okunaklı tipografi uygulanmadı.");
                foreach(var button in nav.Values) {
                    if(button.Parent?.Parent?.BackColor!=Theme.Sidebar)
                        throw new InvalidOperationException("Gezinme ortak koyu yüzeyden ayrıldı.");
                }
                using var image=new Bitmap(Width,Height);using(var graphics=Graphics.FromImage(image))graphics.CopyFromScreen(Location,Point.Empty,Size);
                image.Save(Path.Combine(folder,$"{width}-{route}.png"));
            }
        }
        Navigate("Video hazırla");ToggleAssistant();Application.DoEvents();Refresh();Application.DoEvents();Thread.Sleep(100);
        if(assistantHost?.Visible!=true || assistantHost.BackColor!=Theme.Panel)
            throw new InvalidOperationException("Koyu sohbet paneli açılmadı.");
        using(var image=new Bitmap(Width,Height)) {using(var graphics=Graphics.FromImage(image))graphics.CopyFromScreen(Location,Point.Empty,Size);image.Save(Path.Combine(folder,"1440-sohbet.png"));}
        ToggleAssistant();
        if(before!=System.Text.Json.JsonSerializer.Serialize(new {store.Settings,store.Episodes},Json.Options))
            throw new InvalidOperationException("Görünüm kontrolü kayıtlı proje veya ayarı değiştirdi.");
    }
}
