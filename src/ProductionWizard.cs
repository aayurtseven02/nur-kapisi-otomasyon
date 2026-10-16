using System.Text.Json;

namespace AnkaStudio;

public sealed partial class StudioForm
{
    Episode? wizardDraft;
    void StartWizardProject(){if(engine.Busy){Message("Çalışan işlemi tamamla veya durdur.");return;}wizardDraft=new Episode{IncludeLong=false,ShortCount=1,ShortSeconds=90,Music=false};selectedWorkspaceId=wizardDraft.Id;Navigate("Video hazırla");}
    void QuickWizardScene(Episode episode,Scene scene)
    {
        if(engine.Busy||episode.Videos.Any(v=>v.YouTubeId!="")){Message("Çalışan veya yayınlanmış içerikte yeni sürüm oluştur.");return;}
        using var dialog=Dialog("Sahneyi düzenle",820,560);var flow=Theme.Flow(730);flow.Dock=DockStyle.Fill;dialog.Controls.Add(flow);
        var text=Theme.Input(scene.Narration,true,700);text.Height=180;flow.Controls.Add(text);
        flow.Controls.Add(Theme.Label("Cümleyi değiştirirsen ses yeniden hazırlanır. Görsel yenileme talebinde mevcut görüntü, üretim tamamlanana kadar saklanır.",10,width:700,height:65));
        var refresh=Theme.Check("Bu sahnenin görselini yeniden üret",false,700);flow.Controls.Add(refresh);
        flow.Controls.Add(Theme.Button("Kaydet • üretim adımına dön",()=>{
            if(string.IsNullOrWhiteSpace(text.Text)){Message("Sahne metni boş olamaz.");return;}
            if(text.Text==scene.Narration&&!refresh.Checked){dialog.Close();return;}
            File.WriteAllText(Path.Combine(store.Folder(episode),"wizard-edit-backup-"+DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()+".json"),JsonSerializer.Serialize(episode,Json.Options));
            scene.Narration=text.Text;scene.SpeechTextHash="";scene.SpeechCaptions=[];
            if(refresh.Checked){scene.AiImagePrompt += ". New composition variation "+Guid.NewGuid().ToString("N")[..8];scene.AiImageHash="";scene.StockFile="";scene.RightsConfirmed=false;}
            episode.Approved=false;episode.ApprovalHash="";episode.TextDesignPending=true;episode.Status="Senaryo hazır";episode.WizardStep=2;store.Save();dialog.Close();Navigate("Video hazırla");
        },true,420));dialog.ShowDialog(this);
    }
    public void CaptureProductionWizard(string folder)
    {
        Directory.CreateDirectory(folder);var episode=new Episode{Topic="Bir Gencin İmanı, Bir Hükümdarın Korkusu",IncludeLong=false,ShortCount=1,WizardPreset="story",Demo=true,Creation=new CreationFlow{Normal=false,TextMode="own",OwnText="Bir gencin imanını ve cesaretini anlatan onaylı örnek metin.",Subtitles=true,SubtitleStyle="Sinematik altın",AiVisuals=true,Mode="auto",Split=true,CoverChoice="ai",Reached=4}};
        episode.Creation!.ApprovedHash=TimedNarration.TextHash(episode.Creation.OwnText);store.Episodes.Add(episode);wizardDraft=null;selectedWorkspaceId=episode.Id;
        foreach(int width in new[]{1120,1440}){
            Width=width;
            for(int step=0;step<5;step++){
                episode.WizardStep=step;episode.Plan=step<2?null:Engine.DemoPlan(episode);Navigate("Video hazırla");Application.DoEvents();
                if(nav.Count!=3)throw new InvalidOperationException("Ana menü üç bölüm değil.");
                if(CardChildren(page).OfType<Button>().Count(b=>b.AccessibleName=="Ana işlem")!=1)throw new InvalidOperationException("Tek ana işlem bulunamadı.");
                if(CardChildren(page).OfType<Button>().Count(b=>b.AccessibleName?.StartsWith("Üretim adımı:")==true)!=5)throw new InvalidOperationException("Beş adım eksik.");
                if(step==4&&CardChildren(page).OfType<Button>().Single(b=>b.AccessibleName=="Ana işlem").Enabled)throw new InvalidOperationException("Onaysız yayın düğmesi açık.");
                using var image=new Bitmap(Width,Height);DrawToBitmap(image,new Rectangle(Point.Empty,Size));image.Save(Path.Combine(folder,$"{width}-adim-{step+1}.png"));
            }
        }
    }
}
