namespace AnkaStudio;

public sealed partial class StudioForm

{

    public void CaptureArchiveActions(string directory)

    {

        Directory.CreateDirectory(directory);

        foreach(var state in new[]{"","Planlı bekliyor","Yayında"}) {

            var e=new Episode{Topic="Önizleme "+(state==""?"Tamamlanmış":state),ShortCount=0};e.Plan=Engine.DemoPlan(e);

            e.Videos.Add(new AssetVideo{Key="long",Title=e.Topic,QualityPassed=true,YouTubeId=state==""?"":"fixture",YouTubeState=state});store.Episodes.Add(e);

        }

        foreach(var group in new[]{"Tamamlanmış projeler","Planlanmış projeler","Yayına alınmış projeler"}) openArchiveGroups.Add(group);

        Navigate("İçerik paketleri");PerformLayout();Application.DoEvents();

        IEnumerable<Control> Children(Control p)=>p.Controls.Cast<Control>().SelectMany(c=>new[]{c}.Concat(Children(c)));

        if(Children(this).OfType<Button>().Count(b=>b.Text=="Düzenle")!=3 || Children(this).OfType<Button>().Count(b=>b.Text=="Sil")!=3)throw new Exception("Arşiv düğmeleri eksik.");

        using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(Point.Empty,Size));bitmap.Save(Path.Combine(directory,"archive-actions.png"));}

        Children(this).OfType<Button>().First(b=>b.Text=="Düzenle").PerformClick();PerformLayout();Application.DoEvents();

        if(!Children(this).OfType<Button>().Any(b=>b.Text=="2. Sahne editörü") || !Children(this).OfType<Button>().Any(b=>b.Text=="5. Videoları üret"))throw new Exception("Düzenleme adımları açılmadı.");

        using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(Point.Empty,Size));bitmap.Save(Path.Combine(directory,"archive-editor.png"));}

        File.WriteAllText(Path.Combine(directory,"check.txt"),"PASS: üç grupta Düzenle/Sil/İzle; Düzenle paket adımlarını açtı.");

    }

}

