namespace AnkaStudio;

public static class SceneVideoChoice

{

    public static OpenFileDialog Dialog(Store store, bool pool)

    {

        string folder = pool ? new MediaPool(store).Folder : Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

        if (pool) Directory.CreateDirectory(folder);

        return new OpenFileDialog { Title = pool ? "Video havuzundan sahneye video seç" : "Farklı kaynaktan video seç", Filter = "Video|*.mp4;*.mov;*.mkv;*.webm", InitialDirectory = folder, FileName = pool ? folder + Path.DirectorySeparatorChar : "", RestoreDirectory = true, CheckFileExists = true, Multiselect = false };

    }

    public static void Apply(Scene scene, string file, PoolItem? item)

    {

        scene.StockFile = file; scene.UserLocal = true; scene.StockSelectionVersion = StockSearch.SelectionVersion;

        scene.RightsConfirmed = item?.RightsConfirmed ?? false; scene.StockUrl = item?.Source ?? "Kullanıcının yerel arşivi";

        scene.Creator = item?.Creator ?? "Kullanıcı arşivi"; scene.LicenseUrl = item?.License ?? "Kullanıcı tarafından lisansı doğrulanacak"; scene.Attribution = item?.Attribution ?? ""; scene.StockMetadata = item == null ? "" : item.Tags + " " + item.Category;

    }

}

