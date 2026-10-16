namespace AnkaStudio;
public static class CoverReference
{
    public static string Import(Store store,string file)
    {
        if(new FileInfo(file).Length>32_000_000)throw new InvalidOperationException("Referans görseli en fazla 32 MB olmalı.");
        using var input=File.OpenRead(file);using var image=Image.FromStream(input);
        if(image.Width<256||image.Height<256||(long)image.Width*image.Height>40_000_000)throw new InvalidOperationException("Referans görselinin ölçüsü uygun değil.");
        var folder=Path.Combine(store.Root,"cover-references");Directory.CreateDirectory(folder);
        var target=Path.Combine(folder,Reliability.FileHash(file)+".png");
        if(!File.Exists(target)){using var bitmap=new Bitmap(image);bitmap.Save(target,System.Drawing.Imaging.ImageFormat.Png);}
        return target;
    }
}
