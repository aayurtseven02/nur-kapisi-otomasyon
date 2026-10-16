namespace AnkaStudio;

public sealed partial class StudioForm

{

    public void CaptureNewFeatures(string directory)

    {

        CapturePublicationHelp(directory);

        using var editor = new UserNarrationEditor(new UserNarrationOptions(), true, 1);

        editor.Shown += (_, _) => editor.BeginInvoke(() => {

            var tabs = editor.Controls.OfType<TabControl>().Single(); var text = tabs.TabPages[0].Controls.OfType<TextBox>().Single(); text.Text = "Hz.Muhammet hakkında kendi yazdığım metin.\r\nBu metin aynen korunacak.";

            editor.PerformLayout(); Application.DoEvents();

            using var bitmap = new Bitmap(editor.Width, editor.Height); editor.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(directory, "ready-text-editor.png"));

            editor.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Button>().First().PerformClick();

        });

        if (editor.ShowDialog(this) != DialogResult.OK || !editor.Value.LongText.Contains("Hz.Muhammet")) throw new InvalidOperationException("Hazır metin editörü kaydı başarısız.");

        using var dialog = Dialog("Müzik kullanım bilgileri", 850, 730); var flow = Theme.Flow(760); flow.Dock = DockStyle.Fill; flow.AutoScroll = true; dialog.Controls.Add(flow);

        var usage = new MusicUsage("", "", "", false, 720); flow.Controls.Add(usage);

        dialog.Shown += (_, _) => dialog.BeginInvoke(() => {



            dialog.PerformLayout(); Application.DoEvents();

            using var bitmap = new Bitmap(dialog.Width, dialog.Height); dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(directory, "music-copyright-warning.png"));



            dialog.Close();

        });

        dialog.ShowDialog(this);

        File.WriteAllText(Path.Combine(directory, "new-features-verified.txt"), "Hazır metin yazma / kaydetme, müzik bilgilendirme notu doğrulandı.");

    }

}

