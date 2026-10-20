using System.Drawing.Drawing2D;
namespace AnkaStudio;

public sealed class WorkflowTimeline : Control
{
    public VideoScript Script {get;}
    public int Selected {get;private set;}
    [System.ComponentModel.DefaultValue(true)] public bool AllowEditing {get;set;}=true;
    [System.ComponentModel.DefaultValue(true)] public bool Estimated {get;set;}=true;
    public event Action<int>? SelectedChanged;
    public event Action? BeforeEdit;
    public event Action? Edited;
    public event Action<double>? Seek;
    readonly Dictionary<string,Bitmap> thumbnails=new(StringComparer.OrdinalIgnoreCase);
    readonly CancellationTokenSource stop=new();
    readonly Store? store;
    static readonly SemaphoreSlim thumbnailGate=new(2);
    MusicTrack? dragAudio;int audioEdge;double audioStart,audioEnd;int dragScene=-1;Point dragOrigin;int boundary=-1;double originalLeft,originalRight,originX;bool changed;
    [System.ComponentModel.DefaultValue(0)] public int ContentWidth {get;set;}
    [System.ComponentModel.DefaultValue(0)] public int ViewOffset {get;set;}
    int DrawingWidth=>Math.Max(Width,ContentWidth);
    public WorkflowTimeline(VideoScript script,Store? store=null){Script=script;this.store=store;DoubleBuffered=true;Width=1100;Height=310;BackColor=Theme.Panel;Cursor=Cursors.Hand;AccessibleName="Sahne zaman çizelgesi";}
    public int MusicRowTop=>Height<290?181:217;
    public int EffectsRowTop=>Height<290?210:249;
    int VisualRowBottom=>Height<290?83:96;
    public double Total=>Math.Max(.1,Script.Scenes.Sum(s=>Math.Max(.1,s.Duration)));
    public static string Clock(double seconds)=>TimeSpan.FromSeconds(Math.Max(0,seconds)).ToString(@"hh\:mm\:ss\.ff");
    double X(double t)=>164+t/Total*Math.Max(1,DrawingWidth-178)-ViewOffset;
    public void SelectScene(int index){Selected=Math.Clamp(index,0,Math.Max(0,Script.Scenes.Count-1));SelectedChanged?.Invoke(Selected);PlayheadSeconds=Script.Scenes.Take(Selected).Sum(x=>x.Duration);Seek?.Invoke(PlayheadSeconds);Invalidate();}
    public static void ResizeBoundary(VideoScript script,int index,double change)
    {
        if(index<0||index>=script.Scenes.Count-1||!double.IsFinite(change))throw new InvalidOperationException("Bölüm sınırı bulunamadı veya süre geçersiz.");var left=script.Scenes[index];var right=script.Scenes[index+1];double amount=Math.Clamp(change,.1-left.Duration,right.Duration-.1);left.Duration+=amount;right.Duration-=amount;
    }
    protected override async void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);await LoadThumbnails();}
    async Task LoadThumbnails()
    {
        foreach(string path in Script.Scenes.Select(s=>s.StockFile).Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase)){
            if(stop.IsCancellationRequested)return;
            try{
                string preview=path;
                if(!WorkflowMedia.ImageFile(path)){
                    if(store==null)continue;
                    string directory=Path.Combine(store.Root,"cache","timeline");Directory.CreateDirectory(directory);
                    preview=Path.Combine(directory,TimedNarration.TextHash(path+new FileInfo(path).Length+File.GetLastWriteTimeUtc(path).Ticks)+".jpg");
                    if(!File.Exists(preview)){await thumbnailGate.WaitAsync(stop.Token);try{using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);timeout.CancelAfter(TimeSpan.FromSeconds(30));await Media.Exec(store.Ffmpeg,["-y","-ss","0","-i",path,"-frames:v","1","-vf","scale=160:90:force_original_aspect_ratio=decrease",preview],directory,timeout.Token);}finally{thumbnailGate.Release();}}
                }
                using var original=Image.FromFile(preview);var image=new Bitmap(160,90);using(var g=Graphics.FromImage(image)){g.Clear(Theme.Bg);g.InterpolationMode=InterpolationMode.HighQualityBicubic;double scale=Math.Min(160d/original.Width,90d/original.Height);g.DrawImage(original,new Rectangle((int)((160-original.Width*scale)/2),(int)((90-original.Height*scale)/2),(int)(original.Width*scale),(int)(original.Height*scale)));}
                if(IsDisposed){image.Dispose();return;}if(thumbnails.Remove(path,out var prior))prior.Dispose();thumbnails[path]=image;Invalidate();
            }catch(Exception ex)when(ex is IOException or ArgumentException or HttpRequestException or InvalidOperationException or OperationCanceledException){if(stop.IsCancellationRequested)return;}
        }
    }
    public string MusicLabel="";
    public bool SubtitlesEnabled=true;
    public double PlayheadSeconds;
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(11,19,36));using var font=Theme.Font(9,true);using var small=Theme.Font(8);using var grid=new Pen(Color.FromArgb(24,148,163,184));
        using(var labels=new SolidBrush(Theme.Sidebar))g.FillRectangle(labels,0,0,160,Height);
        string[] names=["V1 • Video & Sahne","T1 • Alt Yazı","A1 • Konuşma & Ses","A2 • Müzik & Efekt"];
        Color[] colors=[StudioGlass.Emerald,StudioGlass.Amber,StudioGlass.Cyan,StudioGlass.Purple];bool compact=Height<290;int[] ys=compact?[39,93,139,185]:[39,109,165,221];
        for(int row=0;row<4;row++){
            TextRenderer.DrawText(g,names[row],font,new Rectangle(10,ys[row]+4,145,24),colors[row],TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g,row==3?"Fon müziği":"Bölümü seç • Süreyi düzenle",small,new Rectangle(10,ys[row]+30,145,20),Theme.Muted,TextFormatFlags.EndEllipsis);
            g.DrawLine(grid,160,ys[row]+(compact?43:52),Width,ys[row]+(compact?43:52));
        }
        var saved=g.Save();g.SetClip(new Rectangle(164,0,Math.Max(1,Width-164),Height));
        double pixels=Math.Max(1,DrawingWidth-178)/Total,ideal=85/pixels;
        double tick=new[]{1d,2,5,10,15,30,60,120,300,600}.FirstOrDefault(x=>x>=ideal);if(tick==0)tick=600;
        double first=Math.Max(0,Math.Floor((ViewOffset-164)/pixels/tick)*tick);
        for(double time=first;time<=Total;time+=tick){float x=(float)X(time);if(x>Width+90)break;if(x<150)continue;
            g.DrawLine(grid,x,27,x,Height-33);TextRenderer.DrawText(g,TimeSpan.FromSeconds(time).ToString(@"mm\:ss"),small,new Rectangle((int)x+3,3,75,22),Theme.Muted,TextFormatFlags.NoPadding);
        }
        double t=0;
        void Block(double start,double end,int y,int height,Color accent,string text,bool selected=false,Bitmap? image=null){
            int left=(int)X(start)+1,right=(int)X(end)-3;if(right<164||left>Width||right-left<4)return;
            var rect=new Rectangle(left,y,Math.Max(4,right-left),height);StudioGlass.Clip(g,rect,accent,selected);
            int inset=12;if(image!=null&&rect.Width>105){g.DrawImage(image,new Rectangle(left+9,y+6,50,height-12));inset=66;}
            TextRenderer.DrawText(g,text,font,new Rectangle(left+inset,y+4,Math.Max(1,rect.Width-inset-12),24),Theme.Text,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter);
            if(height>=48)TextRenderer.DrawText(g,$"{start:0.0}s – {end:0.0}s     {end-start:0.0}s",small,new Rectangle(left+12,y+29,Math.Max(1,rect.Width-24),18),Color.FromArgb(196,220,233),TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
        }
        for(int i=0;i<Script.Scenes.Count;i++){
            var scene=Script.Scenes[i];double end=t+Math.Max(.1,scene.Duration);
            Block(t,end,35,compact?48:61,StudioGlass.Emerald,$"{i+1} • "+(scene.VisualKind=="video"?(scene.VideoFailure==""?"LTX video • ":"Fotoğraf yedeği • "):"")+(string.IsNullOrWhiteSpace(scene.VisualQuery)?scene.Narration:scene.VisualQuery),i==Selected,thumbnails.GetValueOrDefault(scene.StockFile));
            if(SubtitlesEnabled){
                if(scene.SpeechCaptions.Count>0)foreach(var caption in scene.SpeechCaptions)Block(t+Math.Clamp(caption.Start,0,scene.Duration),t+Math.Clamp(caption.End,0,scene.Duration),compact?89:105,compact?40:47,StudioGlass.Amber,caption.Text,i==Selected);
                else Block(t,end,compact?89:105,compact?40:47,StudioGlass.Amber,scene.SubtitleText!=""?scene.SubtitleText:scene.Narration,i==Selected);
            }
            Block(t,end,compact?135:161,compact?40:47,StudioGlass.Cyan,scene.Narration,i==Selected);t=end;
        }
        if(MusicLabel!=""){double musicStart=Math.Clamp(Script.MusicTrack?.Start??0,0,Total),musicEnd=Script.MusicTrack?.End is >0?Math.Min(Total,Script.MusicTrack.End):Total;if(musicEnd>musicStart)Block(musicStart,musicEnd,MusicRowTop,Script.SoundEffects?.Any(x=>x.Enabled)==true?28:compact?40:49,StudioGlass.Purple,"♫ "+MusicLabel);}
        else TextRenderer.DrawText(g,"Fon müziği seçilmedi",small,new Rectangle(176,MusicRowTop+12,Math.Max(1,Width-180),22),Theme.Muted,TextFormatFlags.EndEllipsis);
        foreach(var effect in Script.SoundEffects??[])if(effect.Enabled)Block(effect.Start,Math.Min(Total,effect.End>0?effect.End:Total),EffectsRowTop,25,StudioGlass.Rose,"✦ "+Path.GetFileName(effect.File));
        float play=(float)X(Math.Clamp(PlayheadSeconds,0,Total));using(var marker=new Pen(StudioGlass.Rose,2))g.DrawLine(marker,play,0,play,Height-34);
        using(var brush=new SolidBrush(StudioGlass.Rose))g.FillPolygon(brush,[new PointF(play,0),new PointF(play+9,7),new PointF(play,14)]);
        g.Restore(saved);
        TextRenderer.DrawText(g,(Estimated?"Tahmini süreler":"Gerçek konuşma süreleri")+" • Toplam "+Clock(Total)+(AllowEditing?" • Sınırları sürükle":" • Hazırlık sürüyor"),small,new Rectangle(164,Height-30,Math.Max(1,Width-178),24),Theme.Muted,TextFormatFlags.EndEllipsis);
    }
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left||e.X<164)return;if(AllowEditing&&e.Y>=MusicRowTop&&e.Y<=Height-35){
            double time=(e.X+ViewOffset-164)/Math.Max(1,DrawingWidth-178)*Total;
            var track=e.Y>=EffectsRowTop?Script.SoundEffects?.LastOrDefault(x=>x.Enabled&&time>=x.Start&&time<=(x.End>0?x.End:Total)):null;
            track??=Script.MusicTrack?.Enabled==true?Script.MusicTrack:null;
            if(track!=null&&time>=track.Start&&time<=(track.End>0?track.End:Total)){
                dragAudio=track;audioStart=track.Start;audioEnd=track.End>0?track.End:Total;originX=e.X;audioEdge=Math.Abs(e.X-X(audioStart))<10?-1:Math.Abs(e.X-X(audioEnd))<10?1:0;changed=false;BeforeEdit?.Invoke();Capture=true;return;
            }
        }
        if(e.Y<28){PlayheadSeconds=Math.Clamp((e.X+ViewOffset-164)/Math.Max(1,DrawingWidth-178)*Total,0,Total);Seek?.Invoke(PlayheadSeconds);Invalidate();return;}double t=0;boundary=-1;for(int i=0;i<Script.Scenes.Count;i++){t+=Script.Scenes[i].Duration;if(AllowEditing&&e.Y>=35&&e.Y<=VisualRowBottom&&i<Script.Scenes.Count-1&&Math.Abs(e.X-X(t))<9){boundary=i;originalLeft=Script.Scenes[i].Duration;originalRight=Script.Scenes[i+1].Duration;originX=e.X;changed=false;BeforeEdit?.Invoke();Capture=true;return;}if(e.X<=X(t)){SelectScene(i);if(e.Y>=35&&e.Y<=VisualRowBottom){dragScene=i;dragOrigin=e.Location;}return;}}}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragAudio!=null&&AllowEditing){double delta=(e.X-originX)/Math.Max(1,DrawingWidth-178)*Total;if(audioEdge<0)dragAudio.Start=Math.Clamp(audioStart+delta,0,audioEnd-.1);else if(audioEdge>0)dragAudio.End=Math.Clamp(audioEnd+delta,audioStart+.1,Math.Min(Total,1800));else{double duration=audioEnd-audioStart;dragAudio.Start=Math.Clamp(audioStart+delta,0,Math.Max(0,Math.Min(Total,1800)-duration));dragAudio.End=dragAudio.Start+duration;}changed=true;Invalidate();return;}if(dragScene>=0&&AllowEditing&&e.Button==MouseButtons.Left&&(Math.Abs(e.X-dragOrigin.X)>6||Math.Abs(e.Y-dragOrigin.Y)>6)){int source=dragScene;dragScene=-1;var data=new DataObject();data.SetData("TubeFlow.SceneIndex",source);DoDragDrop(data,DragDropEffects.Move);return;}if(boundary<0||!AllowEditing)return;Script.Scenes[boundary].Duration=originalLeft;Script.Scenes[boundary+1].Duration=originalRight;ResizeBoundary(Script,boundary,(e.X-originX)/Math.Max(1,DrawingWidth-178)*Total);changed=true;Invalidate();}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);dragScene=-1;if(dragAudio!=null){dragAudio=null;Capture=false;if(changed)Edited?.Invoke();return;}if(boundary<0)return;boundary=-1;Capture=false;if(changed)Edited?.Invoke();}
    protected override void Dispose(bool disposing){if(disposing){stop.Cancel();foreach(var image in thumbnails.Values)image.Dispose();thumbnails.Clear();}base.Dispose(disposing);}
}

// Keep the painted control within the viewport. Moving a many-thousand-pixel
// child with native AutoScroll can leave its double-buffered surface blank.
public sealed class WorkflowTimelineViewport : Panel
{
    public WorkflowTimeline Timeline {get;}
    public HScrollBar Scrollbar {get;}=new(){Dock=DockStyle.Bottom,Height=20};
    int virtualWidth;
    public WorkflowTimelineViewport(WorkflowTimeline timeline,int contentWidth)
    {
        Timeline=timeline;virtualWidth=contentWidth;Height=334;DoubleBuffered=true;BackColor=Theme.Panel;
        Timeline.Dock=DockStyle.Fill;Controls.Add(Timeline);Controls.Add(Scrollbar);
        Scrollbar.ValueChanged+=(_,_)=>{Timeline.ViewOffset=Scrollbar.Value;Timeline.Invalidate();};
        SizeChanged+=(_,_)=>RefreshRange();Timeline.SelectedChanged+=EnsureSelectedVisible;
    }
    public void SetContentWidth(int width){virtualWidth=width;RefreshRange();}
    void RefreshRange()
    {
        int width=Math.Max(1,ClientSize.Width);Timeline.ContentWidth=Math.Max(width,virtualWidth);
        Scrollbar.Minimum=0;Scrollbar.Maximum=Timeline.ContentWidth-1;Scrollbar.LargeChange=width;Scrollbar.SmallChange=60;
        Scrollbar.Value=Math.Clamp(Scrollbar.Value,0,Math.Max(0,Timeline.ContentWidth-width));Timeline.ViewOffset=Scrollbar.Value;Timeline.Invalidate();
    }
    void EnsureSelectedVisible(int index)
    {
        double start=Timeline.Script.Scenes.Take(index).Sum(x=>Math.Max(.1,x.Duration));
        int left=(int)(164+start/Timeline.Total*Math.Max(1,Timeline.ContentWidth-178));
        int right=(int)(164+(start+Math.Max(.1,Timeline.Script.Scenes[index].Duration))/Timeline.Total*Math.Max(1,Timeline.ContentWidth-178));
        if(left<Scrollbar.Value+164||right>Scrollbar.Value+ClientSize.Width)Scrollbar.Value=Math.Clamp(left-164,0,Math.Max(0,Timeline.ContentWidth-ClientSize.Width));
    }
}

public static class TimelineEditing
{
    public static void SwapVisuals(VideoScript script,int from,int to)
    {
        if(from<0||to<0||from>=script.Scenes.Count||to>=script.Scenes.Count)throw new InvalidOperationException("Sahne bulunamadı.");
        if(from==to)return;
        foreach(string name in "StockFile,StockUrl,StockMetadata,StockSelectionVersion,StockReviewQuery,StockReviewTheme,Attribution,LicenseUrl,Creator,UserLocal,RightsConfirmed,AiImageHash,AiImageQuality,AiImagePrompt,AiPromptSourceHash,AiCharacterIds,VisualKind,VideoMotionPrompt,AiVideoFile,AiVideoHash,VideoFailure".Split(',')){
            var property=typeof(Scene).GetProperty(name)!;var value=property.GetValue(script.Scenes[from]);property.SetValue(script.Scenes[from],property.GetValue(script.Scenes[to]));property.SetValue(script.Scenes[to],value);
        }
    }
    public static void Duration(VideoScript script,int index,double seconds)
    {
        if(index<0||index>=script.Scenes.Count||!double.IsFinite(seconds)||seconds<.1)throw new InvalidOperationException("Geçerli bölüm süresi gir.");
        if(script.Scenes.Count<2)throw new InvalidOperationException("Tek bölümün süresi konuşmayla belirlenir; toplam ses süresi değiştirilmedi.");
        int neighbor=index<script.Scenes.Count-1?index+1:index-1;double difference=seconds-script.Scenes[index].Duration;
        if(script.Scenes[neighbor].Duration-difference<.1-1e-7)throw new InvalidOperationException("Komşu bölümde yeterli süre yok. Toplam konuşma süresi korunmalı.");
        WorkflowTimeline.ResizeBoundary(script,Math.Min(index,neighbor),index<neighbor?difference:-difference);
    }
    public static void Split(VideoScript script,int index,int word=-1)
    {
        var scene=script.Scenes[index];var words=System.Text.RegularExpressions.Regex.Matches(scene.Narration,@"\S+");if(words.Count<2||scene.Duration<.2)throw new InvalidOperationException("Bölmek için en az iki kelime ve yeterli süre gerekli.");
        word=word>0&&word<words.Count?word:words.Count/2;int cut=words[word].Index;var right=CreationFlow.Copy(scene);double leftSeconds=Math.Clamp(scene.Duration*word/words.Count,.1,scene.Duration-.1);
        string captions=scene.SubtitleText;scene.Narration=scene.Narration[..cut];right.Narration=right.Narration[cut..];
        if(captions!=""){var captionsWords=System.Text.RegularExpressions.Regex.Matches(captions,@"\S+");int count=(int)Math.Round(captionsWords.Count*word/(double)words.Count);int split=count>0&&count<captionsWords.Count?captionsWords[count].Index:captions.Length;scene.SubtitleText=captions[..split];right.SubtitleText=captions[split..];}
        right.Duration=scene.Duration-leftSeconds;scene.Duration=leftSeconds;scene.SpeechCaptions=[];right.SpeechCaptions=[];scene.SpeechTextHash=right.SpeechTextHash="";script.Scenes.Insert(index+1,right);
    }
    public static void Merge(VideoScript script,int index)
    {
        if(index<0||index>=script.Scenes.Count-1)throw new InvalidOperationException("Birleştirmek için sonraki bölümü seç.");var left=script.Scenes[index];var right=script.Scenes[index+1];
        left.SubtitleText=(left.SubtitleText!=""?left.SubtitleText:left.Narration).TrimEnd()+" "+(right.SubtitleText!=""?right.SubtitleText:right.Narration).TrimStart();left.Narration=left.Narration.TrimEnd()+" "+right.Narration.TrimStart();left.Duration+=right.Duration;left.SpeechCaptions=[];left.SpeechTextHash="";script.Scenes.RemoveAt(index+1);
    }
}
