namespace AnkaStudio;

public sealed partial class StudioForm
{
    public void AuditSettingsLayout(string folder)
    {
        Directory.CreateDirectory(folder);Show();Application.DoEvents();
        foreach(int width in new[]{1000,1366}){
            Width=width;Navigate("Yönetim ayarları");Application.DoEvents();
            var groups=CardChildren(page).OfType<StudioDisclosure>().Where(x=>x.Parent?.Parent==page||x.Controls.OfType<Button>().Any(b=>b.AccessibleName?.EndsWith("bölümünü aç veya kapat")==true)).ToArray();
            foreach(var group in groups){
                if(group.IsDisposed||group.Parent==null)continue;
                var selector=CardChildren(page).OfType<ComboBox>().Single(x=>x.AccessibleName=="Ayar bölümü");selector.SelectedItem=group.Controls[0].Text;
                PerformLayout();Application.DoEvents();
                foreach(var parent in new[]{group.Content}.Concat(CardChildren(group.Content).OfType<FlowLayoutPanel>())){
                    if(!parent.Visible)continue;
                    foreach(Control child in parent.Controls){if(!child.Visible)continue;if(child.Right>parent.ClientSize.Width+2)throw new InvalidOperationException($"Sağdan taşma: {group.Controls[0].Text} / {child.GetType().Name} / {child.Text} {child.Right}>{parent.ClientSize.Width}");}
                }
                string name=group.Controls[0].Text.Replace('/','-');
                if(name is "Google TTS kota" or "Ses - müzik" or "Arayüz - ses"){
                    body.ScrollControlIntoView(group);Application.DoEvents();using var image=new Bitmap(Width,Height);DrawToBitmap(image,new Rectangle(Point.Empty,Size));image.Save(Path.Combine(folder,$"{width}-{name}.png"));
                }
            }
        }
    }
}

