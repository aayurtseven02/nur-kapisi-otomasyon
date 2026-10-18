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
    int boundary=-1;double originalLeft,originalRight,originX;bool changed;
    public WorkflowTimeline(VideoScript script,Store? store=null){Script=script;this.store=store;DoubleBuffered=true;Width=1100;Height=238;BackColor=Theme.Panel;Cursor=Cursors.Hand;AccessibleName="Sahne zaman çizelgesi";}
    public double Total=>Math.Max(.1,Script.Scenes.Sum(s=>Math.Max(.1,s.Duration)));
    public static string Clock(double seconds)=>TimeSpan.FromSeconds(Math.Max(0,seconds)).ToString(@"hh\:mm\:ss\.ff");
    double X(double t)=>92+t/Total*Math.Max(1,Width-106);
    public void SelectScene(int index){Selected=Math.Clamp(index,0,Math.Max(0,Script.Scenes.Count-1));SelectedChanged?.Invoke(Selected);Seek?.Invoke(Script.Scenes.Take(Selected).Sum(x=>x.Duration));Invalidate();}
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
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;using var font=Theme.Font(9);using var small=Theme.Font(8);using var pen=new Pen(Theme.Border);using var active=new Pen(Theme.Teal,2);
        TextRenderer.DrawText(g,"GÖRÜNTÜ",small,new Rectangle(4,55,84,40),Theme.Muted,TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g,"KONUŞMA",small,new Rectangle(4,139,84,30),Theme.Muted,TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g,"ALTYAZI",small,new Rectangle(4,177,84,28),Theme.Muted,TextFormatFlags.VerticalCenter);
        double t=0;
        for(int i=0;i<Script.Scenes.Count;i++){
            var scene=Script.Scenes[i];float left=(float)X(t),right=(float)X(t+Math.Max(.1,scene.Duration));var bounds=new Rectangle((int)left+1,31,Math.Max(1,(int)(right-left)-3),99);
            if(bounds.Width>1){using var fill=new SolidBrush(i==Selected?Theme.AccentSurface:Theme.Elevated);g.FillRectangle(fill,bounds);
                if(thumbnails.TryGetValue(scene.StockFile,out var thumb)){var imageRect=new Rectangle(bounds.X+3,34,Math.Max(1,Math.Min(bounds.Width-6,160)),63);g.DrawImage(thumb,imageRect);}
                TextRenderer.DrawText(g,$"{i+1} • {scene.Duration:0.00} sn",font,new Rectangle(bounds.X+4,99,Math.Max(1,bounds.Width-8),27),Theme.Text,TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter);
                if(i==Selected)g.DrawRectangle(active,bounds);else g.DrawRectangle(pen,bounds);
                using var audio=new SolidBrush(Color.FromArgb(37,75,63));g.FillRectangle(audio,bounds.X,139,bounds.Width,31);
                TextRenderer.DrawText(g,scene.Narration,font,new Rectangle(bounds.X+4,141,Math.Max(1,bounds.Width-8),27),Theme.Text,TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
                using var caption=new SolidBrush(Theme.Elevated);g.FillRectangle(caption,bounds.X,177,bounds.Width,28);
                TextRenderer.DrawText(g,scene.SubtitleText!=""?scene.SubtitleText:scene.Narration,small,new Rectangle(bounds.X+4,179,Math.Max(1,bounds.Width-8),24),Theme.Text,TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
            }
            TextRenderer.DrawText(g,Clock(t),small,new Rectangle((int)left,3,95,23),Theme.Muted,TextFormatFlags.NoPadding);
            if(i<Script.Scenes.Count-1)g.DrawLine(active,right,27,right,209);t+=scene.Duration;
        }
        TextRenderer.DrawText(g,(Estimated?"Tahmini süreler • ses üretildiğinde güncellenir":"Gerçek konuşma süreleri")+" • Toplam "+Clock(Total)+(AllowEditing?" • Sınırları sürükle":" • Hazırlık sürüyor; düzenleme kilitli"),small,new Rectangle(92,213,Math.Max(1,Width-106),24),Theme.Muted,TextFormatFlags.EndEllipsis);
    }
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;double t=0;boundary=-1;for(int i=0;i<Script.Scenes.Count;i++){t+=Script.Scenes[i].Duration;if(AllowEditing&&i<Script.Scenes.Count-1&&Math.Abs(e.X-X(t))<9){boundary=i;originalLeft=Script.Scenes[i].Duration;originalRight=Script.Scenes[i+1].Duration;originX=e.X;changed=false;BeforeEdit?.Invoke();Capture=true;return;}if(e.X<=X(t)){SelectScene(i);return;}}}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(boundary<0||!AllowEditing)return;Script.Scenes[boundary].Duration=originalLeft;Script.Scenes[boundary+1].Duration=originalRight;ResizeBoundary(Script,boundary,(e.X-originX)/Math.Max(1,Width-106)*Total);changed=true;Invalidate();}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(boundary<0)return;boundary=-1;Capture=false;if(changed)Edited?.Invoke();}
    protected override void Dispose(bool disposing){if(disposing){stop.Cancel();foreach(var image in thumbnails.Values)image.Dispose();thumbnails.Clear();}base.Dispose(disposing);}
}

public static class TimelineEditing
{
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
