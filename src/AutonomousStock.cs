namespace AnkaStudio;

public static class AutonomousStock
{
    public static async Task<bool> Try(Store store,Episode e,Scene scene,Services service,HashSet<string> used,CancellationToken ct)
    {
        if(e.Creation?.Job.Mode is not ("full" or "review"))return false;
        if(!StockVisualReview.Ready(store))throw new StockVisualReview.UnavailableException("Otonom stok seçimi için yerel görsel okuma modelini etkinleştir. Konu dışı görüntü denetimsiz seçilmedi.");
        var library=new FreeMediaLibrary(store,service.InjectedClient?service.Http:null);
        foreach(string query in scene.VisualQuery.Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Take(3)){
            if(query.Length>200)continue;
            var candidates=await library.Search(query,"Video",ct);
            foreach(var item in candidates.Take(6)){
                ct.ThrowIfCancellationRequested();
                try{
                    string file=await library.Download(item,ct);string hash=Reliability.FileHash(file);if(hash==""||used.Contains(hash))continue;
                    var candidate=new Scene{StockFile=file,VisualQuery=query};
                    if(!await service.ReviewStock(candidate,ct))continue;
                    scene.StockFile=file;scene.StockUrl=item.Source;scene.Creator=item.Creator;scene.LicenseUrl=item.LicenseUrl;scene.Attribution=item.Attribution;scene.StockMetadata="Konu denetimli ücretsiz stok video • "+query;scene.UserLocal=false;scene.RightsConfirmed=true;scene.AiImageHash="";used.Add(hash);e.Log("Otonom ücretsiz video bulundu: "+query);return true;
                }catch(StockVisualReview.UnavailableException){throw;}catch(Exception ex)when(ex is not OperationCanceledException){e.Log("Ücretsiz stok adayı kullanılamadı: "+ex.GetType().Name);}
            }
        }
        return false;
    }
}
