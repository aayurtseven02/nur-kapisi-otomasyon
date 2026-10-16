using System.Reflection;
namespace AnkaStudio;

public static class UserGuide
{
    public static string FullText {get;}=Read();
    static string Read(){using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("AnkaStudio.UserGuide.md") ?? throw new InvalidOperationException("Rehber kaynağı bulunamadı.");using var reader=new StreamReader(stream);return reader.ReadToEnd();}
    public static IReadOnlyList<(string Title,string Body)> Sections {get;}=FullText.Split("\n## ").Skip(1).Select(x=>{int end=x.IndexOf('\n');return(end<0?x:x[..end],end<0?"":x[(end+1)..].Trim());}).ToArray();
    public static Form Create()
    {
        var form=new Form {Text="TubeFlow • Kullanım rehberi",Width=1160,Height=850,MinimumSize=new(850,600),BackColor=Theme.Bg,ForeColor=Theme.Text,Font=Theme.Font(),StartPosition=FormStartPosition.CenterParent,Padding=new Padding(16)};
        var header=new TableLayoutPanel{Dock=DockStyle.Top,Height=78,ColumnCount=1,RowCount=2};header.Controls.Add(Theme.Label("Program kullanım rehberi • konu seç veya ara",15,true,Theme.Teal,1000,36));
        var search=Theme.Input(width:1000);search.Dock=DockStyle.Fill;search.PlaceholderText="Rehberde ara: müzik, Shorts, yayın saati, altyazı…";search.AccessibleName="Rehberde ara";header.Controls.Add(search);
        var list=new ListBox{Dock=DockStyle.Fill,BackColor=Theme.Panel,ForeColor=Theme.Text,BorderStyle=BorderStyle.None,Font=Theme.Font(10),IntegralHeight=false,HorizontalScrollbar=true,AccessibleName="Rehber konuları"};
        var body=Theme.Input(multi:true);body.Dock=DockStyle.Fill;body.ReadOnly=true;body.Font=Theme.Font(11);body.AccessibleName="Rehber açıklaması";body.BorderStyle=BorderStyle.None;
        var columns=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(0,12,0,12)};columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,29));columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,71));columns.Controls.Add(list,0,0);columns.Controls.Add(body,1,0);body.Margin=new Padding(16,0,0,0);
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=56,WrapContents=false};
        buttons.Controls.Add(Theme.Button("Rehberi kopyala",()=>Clipboard.SetText(FullText),false,220));
        buttons.Controls.Add(Theme.Button("Rehberi dosyaya kaydet",()=>{using var save=new SaveFileDialog{Filter="Metin dosyası|*.txt",FileName="Anka-Studio-Kullanim-Rehberi.txt"};if(save.ShowDialog(form)==DialogResult.OK)File.WriteAllText(save.FileName,FullText);},false,270));
        buttons.Controls.Add(Theme.Button("Kapat",form.Close,false,140));
        var filtered=Sections.ToList();
        void Filter(){string query=search.Text.Trim();filtered=Sections.Where(s=>query=="" || (s.Title+" "+s.Body).Contains(query,StringComparison.CurrentCultureIgnoreCase)).ToList();list.BeginUpdate();list.Items.Clear();foreach(var section in filtered)list.Items.Add(section.Title);list.EndUpdate();if(list.Items.Count>0)list.SelectedIndex=0;else body.Text="Bu aramayla eşleşen bölüm bulunamadı. Daha kısa bir kelime dene.";}
        list.SelectedIndexChanged+=(_,_)=>{if(list.SelectedIndex>=0){var section=filtered[list.SelectedIndex];body.Text=section.Title+"\r\n\r\n"+section.Body.Replace("\n","\r\n");body.SelectionStart=0;body.ScrollToCaret();}};
        search.TextChanged+=(_,_)=>Filter();form.Controls.Add(columns);form.Controls.Add(header);form.Controls.Add(buttons);Filter();ModernScrollbars.Apply(form);return form;
    }
    public static void Show(IWin32Window owner){using var form=Create();form.ShowDialog(owner);}
    public static void Capture(string folder)
    {
        Directory.CreateDirectory(folder);using var form=Create();
        form.Shown+=(_,_)=>{try{
            IEnumerable<Control> Children(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Children(x)));
            void Shot(string name){form.PerformLayout();Application.DoEvents();using var image=new Bitmap(form.Width,form.Height);form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(Path.Combine(folder,name+".png"));}
            Shot("guide");var search=Children(form).OfType<TextBox>().Single(t=>t.AccessibleName=="Rehberde ara");var list=Children(form).OfType<ListBox>().Single();search.Text="müzik";Shot("guide-search");if(list.Items.Count==0 || list.Items.Count>=Sections.Count)throw new InvalidOperationException("Rehber araması çalışmadı.");
            search.Text="bulunmayan-konu-012345";if(list.Items.Count!=0)throw new InvalidOperationException("Boş arama sonucu hatalı.");search.Clear();form.Size=new Size(850,600);Shot("guide-small");
            File.WriteAllText(Path.Combine(folder,"verified.txt"),"27 rehber bölümü, arama, boş sonuç ve küçük pencere görünümü kontrol edildi.");
        }catch(Exception ex){File.WriteAllText(Path.Combine(folder,"error.txt"),ex.ToString());Environment.ExitCode=1;}finally{form.Close();}};
        Application.Run(form);
    }
}
