namespace AnkaStudio;



public static class PackageDeletion

{

    public static void Delete(Store store, Episode episode)

    {

        if (!store.Episodes.Contains(episode)) throw new InvalidOperationException("Paket bulunamadı.");

        if (!System.Text.RegularExpressions.Regex.IsMatch(episode.Id, "^[a-f0-9]{32}$")) throw new InvalidOperationException("Geçersiz paket kimliği.");

        var root = Path.GetFullPath(store.Root);

        var projects = Path.GetFullPath(Path.Combine(root, "projects"));

        var target = Path.GetFullPath(Path.Combine(projects, episode.Id));

        if (!target.StartsWith(projects + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Güvenli silme sınırı dışında klasör.");

        void Check(string path)

        {

            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Bağlantılı klasör veya dosya otomatik silinemez.");

            if (Directory.Exists(path)) foreach (var child in Directory.EnumerateFileSystemEntries(path)) Check(child);

        }

        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Bağlantılı çalışma klasörü silme için uygun değil.");

        if (Directory.Exists(projects) && (File.GetAttributes(projects) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Bağlantılı proje klasörü silinemez.");

        var trash = Path.Combine(root, "deleted-packages", episode.Id + "-" + Guid.NewGuid().ToString("N"));

        if (Directory.Exists(target)) { Check(target); Directory.CreateDirectory(Path.GetDirectoryName(trash)!); Directory.Move(target, trash); }

        var index = store.Episodes.IndexOf(episode);

        store.Episodes.RemoveAt(index);

        try { store.Save(); } catch { store.Episodes.Insert(index, episode); if (Directory.Exists(trash)) Directory.Move(trash, target); throw; }

        if (Directory.Exists(trash)) {

            var trashRoot = Path.GetFullPath(Path.Combine(root, "deleted-packages")) + Path.DirectorySeparatorChar;

            if (!Path.GetFullPath(trash).StartsWith(trashRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Silme arşivi çalışma sınırının dışında.");

            try { Check(trash); Directory.Delete(trash, true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { Json.RecoveryMessages.Enqueue("Paket kaydı silindi; dosyalar silme arşivinde kaldı: " + trash); }

        }

    }

}



public sealed partial class StudioForm

{

    bool deletingPackage;

    void DeletePackage(Episode episode)

    {

        if (engine.Busy || autoRunning || deletingPackage) { Message("Önce çalışan işi durdur ve işlemin tamamlanmasını bekle."); return; }

        deletingPackage = true;

        try

        {

            var message = $"Bu projeyi tamamen silmek istediğinize emin misiniz?\n\n{episode.Topic}\n\nNormal video, Shorts, ses, altyazı, kapak, senaryo ve bu paketin yerel dosyaları kalıcı olarak silinecek. Bu işlem geri alınamaz.\n\nHesap bağlantıları, API anahtarları ve ortak stok/ses arşivleri korunur. YouTube'a yüklenen videolar ve GitHub'daki dosyalar silinmez.";

            if (Theme.Alert(this, message, "Projeyi tamamen sil • Emin misiniz?", MessageBoxButtons.YesNo) != DialogResult.Yes) return;

            PackageDeletion.Delete(store, episode);

            Navigate("Videolarım");

            Message("Proje ve ona ait yerel dosyalar silindi.");

        }

        catch (Exception ex) { Message("Proje silinemedi: " + ex.Message); }

        finally { deletingPackage = false; }

    }

}

