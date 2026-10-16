namespace AnkaStudio;

public sealed partial class StudioForm
{
    public void CaptureLaptopLayout(string directory)
    {
        Directory.CreateDirectory(directory);
        IEnumerable<Control> All(Control c) => c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
        var results=new List<string>();
        var realFixture=Path.Combine(Environment.CurrentDirectory,"work","published-episode-fixture.json");
        if(File.Exists(realFixture)) {
            var saved=Json.Read(realFixture,new Episode());
            results.Add("Actual fixture: group="+PackageArchive.PublicationGroup(saved)+", progress="+ProjectState.Progress(saved)+", hashes="+string.Join(",",saved.Videos.Select(v=>v.PlanHash==Reliability.MediaPlanHash(saved,v.Key))));
            File.WriteAllLines(Path.Combine(directory,"fixture-diagnostic.txt"),results);
            if(ProjectState.Progress(saved)!=100 || !ProjectState.CompletedStages(saved,false).All(x=>x))throw new InvalidOperationException("Gerçek yayınlanmış proje eksik görünüyor.");
            store.Episodes.Add(saved);OpenWorkspace(saved);PerformLayout();Application.DoEvents();
            var phases=All(this).OfType<StudioButton>().Where(b=>b.AccessibleName?.StartsWith("Hazırlık adımı:")==true).ToList();
            if(phases.Count!=5 || phases.Any(b=>!b.Completed))throw new InvalidOperationException("Yayınlı projenin beş ana adımı tamamlanmadı.");
            using(var shot=new Bitmap(Width,Height)){DrawToBitmap(shot,new Rectangle(Point.Empty,Size));shot.Save(Path.Combine(directory,"published-project.png"));}
        }
        foreach(var size in new[]{new Size(1120,700),new Size(1000,650),new Size(900,600)})
        {
            Size=size;Navigate("Bağlantı Merkezi");PerformLayout();Application.DoEvents();
            All(this).OfType<Button>().Single(b=>b.Text=="Stok / araştırma").PerformClick();PerformLayout();Application.DoEvents();
            var label=All(this).OfType<Label>().Single(l=>l.Text.ToUpper(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"))=="Vecteezy / stok video".ToUpper(System.Globalization.CultureInfo.GetCultureInfo("tr-TR")));
            var card=label.Parent!;var last=All(card).OfType<Button>().Where(b=>b.Text!="i").Last();
            ScrollableControl? scroll=null;
            for(Control? c=card.Parent;c!=null;c=c.Parent)if(c is ScrollableControl x && x.AutoScroll){scroll=x;break;}
            if(scroll==null)throw new InvalidOperationException("Stok paneli kaydırma alanı eksik.");
            scroll.ScrollControlIntoView(card);scroll.AutoScrollPosition=new Point(0,Math.Max(0,scroll.VerticalScroll.Maximum-scroll.ClientSize.Height+1));Application.DoEvents();
            var bottom=scroll.PointToClient(card.PointToScreen(new Point(0,card.Height)));
            if(bottom.Y>scroll.ClientSize.Height+20)throw new InvalidOperationException($"Stok kartının altı erişilemiyor: {size}, {bottom.Y}/{scroll.ClientSize.Height}");
            var visible=body.RectangleToScreen(body.ClientRectangle);
            var buttons=All(card).OfType<Button>().Where(b=>b.Text!="i").ToList();
            foreach(var button in buttons)if(!visible.Contains(button.RectangleToScreen(button.ClientRectangle)))throw new InvalidOperationException($"Vecteezy tuşu gerçek ekran alanı dışında: {size}, {button.Text}");
            foreach(var button in buttons)for(Control? ancestor=button.Parent;ancestor!=null;ancestor=ancestor.Parent)if(!ancestor.RectangleToScreen(ancestor.ClientRectangle).Contains(button.RectangleToScreen(button.ClientRectangle)))throw new InvalidOperationException($"Vecteezy tuşu üst panelde kırpılıyor: {size}, {button.Text}, {ancestor.GetType().Name}, button={button.Bounds}, ancestor={ancestor.ClientRectangle}");
            using(var shot=new Bitmap(Width,Height)){DrawToBitmap(shot,new Rectangle(Point.Empty,Size));shot.Save(Path.Combine(directory,$"stock-bottom-{size.Width}.png"));}
            Navigate("Ayarlar");PerformLayout();Application.DoEvents();
            using(var shot=new Bitmap(Width,Height)){DrawToBitmap(shot,new Rectangle(Point.Empty,Size));shot.Save(Path.Combine(directory,$"settings-{size.Width}.png"));}
            results.Add($"PASS {size.Width}x{size.Height}: stok kartı altına kaydırma");
        }
        var published=new Episode {Topic="Yayın durumu testi",ShortCount=0,Plan=Engine.DemoPlan(new Episode {ShortCount=0}),Approved=false};
        foreach(var item in Rules.Scripts(published))published.Videos.Add(new AssetVideo {Key=item.Key,YouTubeId="fixture-id",YouTubeState="Yayında",File="missing-local-test.mp4",PlanHash=Reliability.MediaPlanHash(published,item.Key)});
        if(ProjectState.Progress(published)!=100 || !ProjectState.CompletedStages(published,false).All(x=>x))throw new InvalidOperationException("Yayınlanmış proje tamamlandı görünmüyor.");
        published.TextDesignPending=true;
        if(ProjectState.Progress(published)==100 || ProjectState.CompletedStages(published,false).All(x=>x))throw new InvalidOperationException("Değişen yayınlanmış proje güncel sayıldı.");
        results.Add("PASS: yayınlanmış proje %100; sonraki değişiklik yeniden üretim gerektiriyor");
        var shortText=new Episode{Topic="Eksik kelime testi",LongMinutes=1,ShortCount=0,Plan=Engine.DemoPlan(new Episode{ShortCount=0}),Error="Kelime sayısı eksik",ResumeStage="Produce"};
        store.Episodes.Add(shortText);OpenWorkspace(shortText);PerformLayout();Application.DoEvents();
        if(!All(this).OfType<Button>().Any(b=>b.Text=="Eksik Kelimeleri Tamamla ve Devam Et"))throw new InvalidOperationException("Kelime tamamlama tuşu eksik.");
        results.Add("PASS: kelime bütçesi eksik projede tamamlama tuşu mevcut");
        File.WriteAllLines(Path.Combine(directory,"result.txt"),results);
    }
}
