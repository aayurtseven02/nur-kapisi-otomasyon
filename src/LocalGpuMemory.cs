using System.Net.Http.Json;
using System.Text.Json;
namespace AnkaStudio;

public static class LocalGpuMemory
{
    static readonly HttpClient Http=new(){Timeout=TimeSpan.FromSeconds(20)};
    public static async Task FreeIdleComfy(int port,CancellationToken ct)
    {
        try{
            using var response=await Http.GetAsync($"http://127.0.0.1:{port}/queue",ct);response.EnsureSuccessStatusCode();using var queue=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if(queue.RootElement.GetProperty("queue_running").GetArrayLength()!=0||queue.RootElement.GetProperty("queue_pending").GetArrayLength()!=0)return;
            using var freed=await Http.PostAsJsonAsync($"http://127.0.0.1:{port}/free",new{unload_models=true,free_memory=true},ct);
        }catch(Exception error)when(error is HttpRequestException or JsonException or OperationCanceledException){ct.ThrowIfCancellationRequested();}
    }
    public static async Task ReleaseText(Store store,CancellationToken ct)
    {
        if(!store.Settings.UseOllama)return;
        foreach(string model in new[]{store.Settings.OllamaModel,store.Settings.OllamaVisionModel}.Distinct())try{using var response=await Http.PostAsJsonAsync("http://127.0.0.1:11434/api/generate",new{model,keep_alive=0,stream=false},ct);}catch(Exception error)when(error is HttpRequestException or OperationCanceledException){ct.ThrowIfCancellationRequested();}
    }
}
