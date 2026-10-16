namespace AnkaStudio;
public sealed partial class StudioForm
{
    Task<bool> ReviewStoryPicture(string path,string description,CancellationToken ct)
    {
        var result=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if(IsDisposed||ct.IsCancellationRequested){result.TrySetCanceled(ct);return result.Task;}
        void Show()
        {
            if(ct.IsCancellationRequested){result.TrySetCanceled(ct);return;}
            using var dialog=new Form{Text="Hikâye görselini onayla",StartPosition=FormStartPosition.CenterParent,Size=new Size(900,800),MinimumSize=new Size(650,500),BackColor=Theme.Bg,ForeColor=Theme.Text,ShowInTaskbar=false};
            Bitmap copy; using(var stream=File.OpenRead(path)) using(var original=Image.FromStream(stream)) copy=new Bitmap(original); using var picture=copy;
            var image=new PictureBox{Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,Image=picture,BackColor=Color.Black};
            var text=new Label{Dock=DockStyle.Top,Height=90,Padding=new Padding(12),Text=description+"\nOnaylamadan sonraki görsel veya video üretilmez.",ForeColor=Theme.Text};
            var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=65,Padding=new Padding(10)};
            actions.Controls.Add(Theme.Button("Onayla • devam et",()=>{result.TrySetResult(true);dialog.Close();},true,230));
            actions.Controls.Add(Theme.Button("Yeniden üret",()=>{result.TrySetResult(false);dialog.Close();},false,180));
            actions.Controls.Add(Theme.Button("Üretimi durdur",()=>{engine.Stop();result.TrySetCanceled();dialog.Close();},false,190));
            dialog.Controls.Add(image);dialog.Controls.Add(text);dialog.Controls.Add(actions);
            using var registration=ct.Register(()=>{result.TrySetCanceled(ct);if(dialog.IsHandleCreated&&!dialog.IsDisposed)try{dialog.BeginInvoke(()=>dialog.Close());}catch(InvalidOperationException){}});
            try{dialog.ShowDialog(this);}finally{if(!result.Task.IsCompleted){engine.Stop();result.TrySetCanceled();}}
        }
        try { if(InvokeRequired)BeginInvoke(()=>{try{Show();}catch(Exception ex){result.TrySetException(ex);}});else Show(); }
        catch(Exception ex){result.TrySetException(ex);}
        return result.Task;
    }
}
