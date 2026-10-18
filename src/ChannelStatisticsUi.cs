namespace AnkaStudio;

public sealed partial class StudioForm
{
    void ShowChannelStatistics()=>ShowChannelStatistics(null);
    void ShowChannelStatistics(Action<Form>? preview)
    {
        using var dialog=Dialog("YouTube istatistikleri ve öneriler",1250,850);dialog.MinimumSize=new(1000,700);
        var toolbar=new FlowLayoutPanel {Dock=DockStyle.Top,Height=54,WrapContents=false};
        var period=Theme.Combo(["Son 28 gün","Son 90 gün","Son 365 gün"],"Son 28 gün",170);toolbar.Controls.Add(period);
        var tabs=new StudioTabs {Dock=DockStyle.Fill};
        var tablePage=new TabPage("Videolar ve Shorts") {BackColor=Theme.Bg};
        var advicePage=new TabPage("Veriye göre öneriler") {BackColor=Theme.Bg};
        var goalsPage=new TabPage("Para kazanma hedefi") {BackColor=Theme.Bg};tabs.TabPages.AddRange([tablePage,advicePage,goalsPage]);
        var summary=Theme.Label("Veriler bekleniyor…",11,true,width:1100,height:65);summary.Dock=DockStyle.Top;
        var status=Theme.Label("YouTube'dan son veriler alınacak.",10,width:1100,height:70);status.Dock=DockStyle.Bottom;
        var grid=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells,BackgroundColor=Theme.Panel,BorderStyle=BorderStyle.None,EnableHeadersVisualStyles=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false};
        grid.DefaultCellStyle.BackColor=Theme.Panel;grid.DefaultCellStyle.ForeColor=Theme.Text;grid.DefaultCellStyle.SelectionBackColor=Theme.AccentSurface;grid.DefaultCellStyle.SelectionForeColor=Color.White;grid.DefaultCellStyle.Font=Theme.Font(10);
        grid.ColumnHeadersDefaultCellStyle.BackColor=Theme.Elevated;grid.ColumnHeadersDefaultCellStyle.ForeColor=Theme.Text;grid.ColumnHeadersDefaultCellStyle.Font=Theme.Font(10,true);grid.RowTemplate.Height=32;tablePage.Controls.Add(grid);
        var advice=new TextBox {Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=Theme.Panel,ForeColor=Theme.Text,Font=Theme.Font(12),BorderStyle=BorderStyle.None};advicePage.Controls.Add(advice);
        var goals=new TextBox {Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=Theme.Panel,ForeColor=Theme.Text,Font=Theme.Font(12),BorderStyle=BorderStyle.None};goalsPage.Controls.Add(goals);
        var kind=Theme.Combo(["Tümü","Normal video","Shorts","Belirlenmedi"],"Tümü",180);toolbar.Controls.Add(kind);
        var cache=Path.Combine(store.Root,"cache","youtube-channel-statistics.json");
        ChannelStatistics? data=Json.Read(cache,new ChannelStatistics());if(data.ChannelId!=store.Settings.YouTubeChannelId)data=null;
        using var cancellation=new CancellationTokenSource();bool running=false;
        void Display(){
            if(data==null)return;
            summary.Text=$"{data.ChannelName} • Kanal toplam izlenme: {data.ChannelViews:N0} • Abone: {(data.Subscribers.HasValue?data.Subscribers.Value.ToString("N0"):"Gizli / alınamadı")}\nListelenen videolar: {data.Videos.Count} • Shorts: {data.Videos.Where(v=>v.Kind=="Shorts").Sum(v=>v.Views):N0} izlenme • Normal: {data.Videos.Where(v=>v.Kind=="Normal video").Sum(v=>v.Views):N0} izlenme";
            var rows=new System.Data.DataTable();rows.Columns.Add("Video");rows.Columns.Add("Tür");rows.Columns.Add("Durum");rows.Columns.Add("İzlenme (toplam)",typeof(long));rows.Columns.Add($"Son {data.Days} gün",typeof(long));rows.Columns.Add("Saat (dönem)",typeof(double));rows.Columns.Add("İzlenme %",typeof(double));rows.Columns.Add("Beğeni",typeof(long));rows.Columns.Add("Yorum",typeof(long));rows.Columns.Add("Günlük ort.",typeof(double));rows.Columns.Add("Video kimliği");
            foreach(var v in data.Videos.Where(v=>kind.Text=="Tümü"||v.Kind==kind.Text).OrderByDescending(v=>v.Views))rows.Rows.Add(v.Title,v.Kind,v.Visibility=="public"?"Yayında":v.Visibility,v.Views,(object?)v.PeriodViews??DBNull.Value,(object?)v.WatchHours??DBNull.Value,(object?)v.Retention??DBNull.Value,v.Likes,v.Comments,v.DailyAverage,v.Id);
            grid.DataSource=rows;grid.Columns[0].Width=360;grid.Columns[0].AutoSizeMode=DataGridViewAutoSizeColumnMode.None;grid.Columns["Video kimliği"]!.Visible=false;
            foreach(DataGridViewColumn column in grid.Columns)if(column.ValueType==typeof(long))column.DefaultCellStyle.Format="N0";else if(column.ValueType==typeof(double))column.DefaultCellStyle.Format="N1";
            advice.Text=YouTubeStatistics.Recommend(data);
            goals.Text=$"REKLAM GELİRİ HEDEFİ\r\n\r\nAbone: {(data.Subscribers.HasValue?data.Subscribers.Value.ToString("N0"):"Alınamadı")} / 1.000\r\n\r\nNormal video izlenme saati • son 365 gün (API): {(data.NormalWatchHours365.HasValue?data.NormalWatchHours365.Value.ToString("N1"):"Analitik izni gerekiyor")}\r\nReferans eşik: 4.000 uygun herkese açık izlenme saati.\r\n\r\nShorts engaged views • son 90 gün (API): {(data.ShortsEngagedViews90.HasValue?data.ShortsEngagedViews90.Value.ToString("N0"):"Analitik izni gerekiyor")}\r\nReferans eşik: 10.000.000 uygun Shorts izlenmesi.\r\n\r\nAPI saatleri ve engaged views, Studio'nun para kazanma için kabul ettiği kesin sayaçlar değildir. Reklam kampanyaları, görünürlük ve diğer uygunluk kuralları fark yaratabilir. Kesin ilerlemeyi YouTube Studio → Para kazanma bölümünden doğrula.\r\n\r\nBu eşikler tek başına başvurunun kabulü anlamına gelmez. Abone sayısı API tarafından yuvarlanabilir.";
            status.Text="Son güncelleme: "+data.Updated.ToOffset(TimeSpan.FromHours(3)).ToString("dd.MM.yyyy HH:mm")+" • "+data.Note;
        }
        Button? refresh=null;Button? permission=null;
        async Task Refresh(){if(running)return;running=true;refresh!.Enabled=permission!.Enabled=false;period.Enabled=false;status.Text="Lütfen bekleyin • istatistikler alınıyor…";
            try{var result=await new YouTubeStatistics(store).Fetch(period.SelectedIndex==1?90:period.SelectedIndex==2?365:28,message=>{if(!dialog.IsDisposed)status.Text=message;},cancellation.Token);if(dialog.IsDisposed)return;data=result;Json.Write(cache,result);Display();}
            catch(OperationCanceledException){}catch(Exception ex){if(!dialog.IsDisposed)status.Text="Güncellenemedi: "+ex.Message+" • Önceki veriler korunuyor.";}
            finally{running=false;if(!dialog.IsDisposed){refresh.Enabled=permission.Enabled=true;period.Enabled=true;}}
        }
        refresh=Theme.Button("Güncelle",()=>_=Refresh(),true,140);toolbar.Controls.Add(refresh);
        permission=Theme.Button("İstatistik izni ver",async()=>{if(running)return;if(engine.Busy){status.Text="Üretim bittikten sonra istatistik izni verebilirsin.";return;}permission!.Enabled=false;refresh!.Enabled=false;status.Text="Tarayıcıdaki Google izni bekleniyor…";try{await new YouTube(store).Connect(cancellation.Token,analytics:true,report:message=>{if(!dialog.IsDisposed)status.Text=message;});await Refresh();}catch(OperationCanceledException){if(!dialog.IsDisposed)status.Text="Google izin işlemi iptal edildi veya süre doldu. Yeniden deneyebilirsin.";}catch(Exception ex){if(!dialog.IsDisposed)status.Text=ex.Message;}finally{if(!dialog.IsDisposed)permission.Enabled=refresh.Enabled=true;}},false,190);toolbar.Controls.Add(permission);
        toolbar.Controls.Add(Theme.Button("YouTube Studio",()=>Theme.Open("https://studio.youtube.com/"),false,180));
        kind.SelectedIndexChanged+=(_,_)=>Display();
        grid.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0 && grid.Rows[e.RowIndex].Cells["Video kimliği"].Value is string id)Theme.Open("https://www.youtube.com/watch?v="+Uri.EscapeDataString(id));};
        dialog.Controls.Add(tabs);dialog.Controls.Add(summary);dialog.Controls.Add(toolbar);dialog.Controls.Add(status);dialog.FormClosed+=(_,_)=>cancellation.Cancel();
        Display();dialog.Shown+=async(_,_)=>{if(preview!=null)preview(dialog);else await Refresh();};dialog.ShowDialog(this);
    }
}
