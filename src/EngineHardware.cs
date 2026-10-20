using System.Diagnostics;
using System.Text.Json;
namespace AnkaStudio;

public static class EngineHardware
{
    public sealed record Capability(string Backend,string Device);
    public sealed record Candidate(string Folder,string Backend,string Device);
    public static string? Python(string folder)=>new[]{Path.Combine(folder,"ComfyUI",".venv","Scripts","python.exe"),Path.Combine(folder,"python_embeded","python.exe"),Path.Combine(folder,"standalone-env","python.exe")}.FirstOrDefault(File.Exists);
    public static Candidate[] Order(IEnumerable<(string Folder,Capability[] Devices)> engines)
        =>engines.SelectMany(x=>x.Devices.Select(d=>new Candidate(x.Folder,d.Backend,d.Device)))
            .OrderBy(x=>x.Backend=="CUDA"||x.Backend=="ROCm"?0:x.Backend=="XPU"?1:x.Backend=="DirectML"?2:3).ToArray();
    public static string[] Arguments(string backend)=>backend=="CPU"?["--cpu"]:backend=="DirectML"?["--directml"]:["--lowvram"];
    public static async Task<Capability[]> Probe(string folder,CancellationToken ct)
    {
        const string script="""
import json
result=[]
try:
 import torch
 try:
  if torch.cuda.is_available():
   t=torch.ones(1,device='cuda'); (t+t).item()
   result.append({'Backend':'ROCm' if torch.version.hip else 'CUDA','Device':torch.cuda.get_device_name(0)})
 except Exception: pass
 try:
  if hasattr(torch,'xpu') and torch.xpu.is_available():
   t=torch.ones(1,device='xpu'); (t+t).item()
   result.append({'Backend':'XPU','Device':torch.xpu.get_device_name(0)})
 except Exception: pass
 try:
  if not result:
   import torch_directml
   device=torch_directml.device(); t=torch.ones(1,device=device); (t+t).cpu().item()
   result.append({'Backend':'DirectML','Device':'DirectML GPU'})
 except Exception: pass
 result.append({'Backend':'CPU','Device':'CPU'})
except Exception: pass
print('TUBEFLOW_HARDWARE='+json.dumps(result))
""";
        var info=new ProcessStartInfo(Python(folder)??throw new InvalidOperationException("Yerel motorun Python ortamı bulunamadı.")){WorkingDirectory=folder,UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true};foreach(string arg in new[]{"-s","-u","-c",script})info.ArgumentList.Add(arg);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(35));using var process=Process.Start(info)??throw new InvalidOperationException("Donanım denetimi başlatılamadı.");
        var stdout=process.StandardOutput.ReadToEndAsync(timeout.Token);var stderr=process.StandardError.ReadToEndAsync(timeout.Token);
        try{await process.WaitForExitAsync(timeout.Token);string output=await stdout;await stderr;string? line=output.Split('\n').LastOrDefault(x=>x.StartsWith("TUBEFLOW_HARDWARE="));return line==null?[]:JsonSerializer.Deserialize<Capability[]>(line[18..].Trim())??[];}
        finally{if(!process.HasExited)try{process.Kill(true);}catch{} }
    }
}
