namespace AnkaStudio;
public sealed partial class StudioForm
{
    Form WelcomeSetup()
    {
        var dialog=Dialog("Anka’ya hoş geldin",670,500);var flow=Theme.Flow(590);flow.Dock=DockStyle.Fill;dialog.Controls.Add(flow);
        int step=0;string channel=store.Settings.ChannelName;string preset="story";
        void Draw(){foreach(var control in flow.Controls.Cast<Control>().ToArray())control.Dispose();flow.Controls.Clear();
            flow.Controls.Add(Theme.Label($"Kolay kurulum • {step+1}/3",20,true,width:560,height:50));
            if(step==0){flow.Controls.Add(Theme.Label("Kanalının adını yaz. YouTube hesabını Merkez’den ayrıca bağlayabilirsin.",11,width:560,height:65));var name=Theme.Input(channel,width:560);name.AccessibleName="Kurulum kanal adı";name.TextChanged+=(_,_)=>channel=name.Text;flow.Controls.Add(name);}
            else if(step==1){flow.Controls.Add(Theme.Label("İlk içeriğinin biçimini seç. Sonraki projelerde değiştirebilirsin.",11,width:560,height:65));var kind=Theme.Combo(ProductionPresets.All.Select(p=>p.Name).ToArray(),ProductionPresets.All.First(p=>p.Id==preset).Name,560);kind.SelectedIndexChanged+=(_,_)=>preset=ProductionPresets.All[kind.SelectedIndex].Id;flow.Controls.Add(kind);}
            else flow.Controls.Add(Theme.Label("Hazırsın. Stüdyo’da metnini girip beş adımda ilerleyebilirsin. Ses ve görsel motorlarının bağlantısını Merkez’de kontrol et. Kanal hesabı bu ekranla otomatik bağlanmaz.",12,width:560,height:150));
            flow.Controls.Add(Theme.Button(step<2?"Devam et":"Stüdyo’yu aç",()=>{if(step<2){step++;Draw();return;}store.Settings.ChannelName=channel.Trim()==""?"Benim kanalım":channel.Trim();store.Settings.SetupCompleted=true;store.Save();wizardDraft=new Episode{IncludeLong=false,ShortCount=1,ShortSeconds=90,WizardPreset=preset};dialog.DialogResult=DialogResult.OK;},true,300));
            flow.Controls.Add(Theme.Button("Daha sonra",()=>{store.Settings.SetupCompleted=true;store.Save();dialog.DialogResult=DialogResult.Cancel;},false,220));
        }
        Draw();return dialog;
    }
    void ShowWelcomeSetup(){using var dialog=WelcomeSetup();if(dialog.ShowDialog(this)==DialogResult.OK)Navigate("Video hazırla");}
}
