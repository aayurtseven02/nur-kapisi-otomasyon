namespace AnkaStudio;

// One embedded player per list. Switching rows cancels generation and stops the previous sample.
public sealed class VoiceSelectionList : UserControl
{
    readonly FlowLayoutPanel rows=new(){Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false};
    readonly ArchiveMusicPlayer player=new(compact:true){Visible=false};
    readonly System.Windows.Forms.Timer timer=new(){Interval=200};
    readonly Dictionary<string,Button> playButtons=[];
    readonly Label status=Theme.Label("Dinlemek için satırdaki ▶ tuşuna bas. Önizleme sağlayıcı kotasını kullanır.",9,width:600,height:48);
    readonly CancellationTokenSource lifetime=new();
    CancellationTokenSource? request;
    string? activeVoice;
    bool generating,observedPlaying,resourcesDisposed;
    public VoiceSelectionList(Store store,Settings settings,IEnumerable<string> voices,Action<string> save,Func<bool> busy,Func<string,CancellationToken,Task<string>>? sample=null)
    {
        Controls.Add(rows);status.Dock=DockStyle.Bottom;Controls.Add(status);Controls.Add(player);
        foreach(string voice in voices){
            var row=new Panel{Width=650,Height=64,Margin=new Padding(0,0,0,6),BackColor=Theme.Panel};
            var label=Theme.Label(voice,10,width:350,height:58);label.AccessibleName=voice;row.Controls.Add(label);
            var play=Theme.Button("▶ Dinle",()=>_=Toggle(voice),false,110);play.AccessibleName=voice+" sesini dinle veya durdur";play.Top=8;row.Controls.Add(play);playButtons[voice]=play;
            var select=Theme.Button("Kaydet",()=>{if(busy()){status.Text="Üretim bitince anlatıcıyı değiştir.";return;}StopSample();save(voice);status.Text="Seçildi ve kaydedildi: "+voice;},false,110);select.Top=8;select.AccessibleName=voice+" sesini seç ve kaydet";row.Controls.Add(select);
            void Fit(){int w=Math.Max(200,rows.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-8);row.Width=w;select.Left=w-110;play.Left=Math.Max(0,select.Left-116);label.Width=Math.Max(40,play.Left-8);}
            rows.SizeChanged+=(_,_)=>Fit();rows.Controls.Add(row);Fit();
        }
        timer.Tick+=(_,_)=>{if(generating||activeVoice==null)return;int state=player.PlaybackState;if(state==3)observedPlaying=true;if(observedPlaying&&state is 1 or 8 or 0)StopSample();};timer.Start();
        async Task Toggle(string voice){
            if(activeVoice==voice){StopSample();return;}
            StopSample();if(busy()){status.Text="Önizleme için üretimin bitmesini bekle.";return;}
            var current=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);request=current;activeVoice=voice;generating=true;playButtons[voice].Text="■ Durdur";status.Text="Ses örneği hazırlanıyor…";
            try{
                var copy=CreationFlow.Copy(settings);copy.GoogleTtsVoice=voice;
                string folder=Path.Combine(store.Root,"cache","voice-previews");Directory.CreateDirectory(folder);
                string file=Path.Combine(folder,TimedNarration.TextHash(voice+"|"+copy.VoiceSpeed)+".wav");
                if(sample!=null)file=await sample(voice,current.Token);else if(!File.Exists(file)){string pending=file+".partial.wav";try{await new GoogleTts(store).Speak("Merhaba. Bu, seçtiğiniz Türkçe sesin önizlemesidir.",pending,copy,current.Token);current.Token.ThrowIfCancellationRequested();File.Move(pending,file,true);}finally{if(File.Exists(pending))File.Delete(pending);}}
                current.Token.ThrowIfCancellationRequested();if(IsDisposed||request!=current)return;
                player.LoadAndPlay(file);if(!player.IsLoaded)throw new InvalidOperationException("Gömülü ses oynatıcısı başlatılamadı.");generating=false;status.Text="Çalıyor: "+voice;
            }catch(OperationCanceledException){}catch(Exception ex){if(!IsDisposed&&request==current){StopSample();status.Text="Ses örneği çalınamadı: "+ex.Message;}}
            finally{if(request==current){generating=false;request=null;}current.Dispose();}
        }
    }
    void StopSample(){request?.Cancel();request=null;player.StopPlayback();if(activeVoice!=null&&playButtons.TryGetValue(activeVoice,out var button))button.Text="▶ Dinle";activeVoice=null;generating=observedPlaying=false;}
    protected override void Dispose(bool disposing){if(disposing&&!resourcesDisposed){resourcesDisposed=true;lifetime.Cancel();StopSample();timer.Stop();timer.Dispose();player.Dispose();lifetime.Dispose();}base.Dispose(disposing);}
}
