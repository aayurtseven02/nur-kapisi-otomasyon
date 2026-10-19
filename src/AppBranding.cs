using System.Diagnostics;
using System.Runtime.InteropServices;
namespace AnkaStudio;

internal static class AppBranding
{
    public static Image Logo()
    {
        using var stream=typeof(AppBranding).Assembly.GetManifestResourceStream("AnkaStudio.Assets.tubeflow-horizontal.png")!;
        using var source=Image.FromStream(stream);var logo=new Bitmap(710,200);
        using(var g=Graphics.FromImage(logo)){g.Clear(Color.Transparent);g.DrawImage(source,new Rectangle(0,0,710,200),new Rectangle(155,185,710,200),GraphicsUnit.Pixel);}return logo;
    }
    [DllImport("shell32.dll")] static extern void SHChangeNotify(uint events,uint flags,IntPtr item1,IntPtr item2);
    public static bool ForwardLegacy(string[] args){
        var next=Path.Combine(AppContext.BaseDirectory,"TubeFlow.exe");
        if(!string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath),"AnkaStudio",StringComparison.OrdinalIgnoreCase)||!File.Exists(next))return false;
        try{var start=new ProcessStartInfo(next){UseShellExecute=false,WorkingDirectory=AppContext.BaseDirectory};foreach(var arg in args)start.ArgumentList.Add(arg);return Process.Start(start)!=null;}catch{return false;}
    }
    public static int RefreshShortcuts(string appFolder,IEnumerable<string> folders){
        string target=Path.Combine(Path.GetFullPath(appFolder),"TubeFlow.exe"),legacy=Path.Combine(Path.GetFullPath(appFolder),"AnkaStudio.exe");if(!File.Exists(target))return 0;
        int changed=0;dynamic? shell=null;
        try{var type=Type.GetTypeFromProgID("WScript.Shell");if(type==null)return 0;shell=Activator.CreateInstance(type)!;
            foreach(var folder in folders.Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists))foreach(var file in Directory.EnumerateFiles(folder,"*.lnk",SearchOption.TopDirectoryOnly).ToArray()){
                dynamic? shortcut=null;try{shortcut=shell.CreateShortcut(file);string old=shortcut.TargetPath;if(!string.Equals(old,legacy,StringComparison.OrdinalIgnoreCase)&&!string.Equals(old,target,StringComparison.OrdinalIgnoreCase))continue;string newPath=Path.Combine(folder,"TubeFlow.lnk");if(!string.Equals(file,newPath,StringComparison.OrdinalIgnoreCase)&&File.Exists(newPath))continue;shortcut.TargetPath=target;shortcut.IconLocation=target+",0";shortcut.Description="TubeFlow";shortcut.WorkingDirectory=appFolder;shortcut.Save();if(!string.Equals(file,newPath,StringComparison.OrdinalIgnoreCase))File.Move(file,newPath);changed++;}catch{}finally{if(shortcut!=null)Marshal.FinalReleaseComObject((object)shortcut);}
            }
        }catch{}finally{if(shell!=null)Marshal.FinalReleaseComObject((object)shell);}
        if(changed>0)SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);return changed;
    }
    public static void RefreshUserShortcuts(){var desktop=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);var programs=Environment.GetFolderPath(Environment.SpecialFolder.Programs);RefreshShortcuts(AppContext.BaseDirectory,[desktop,programs,Path.Combine(programs,"AnkaStudio"),Path.Combine(programs,"TubeFlow")]);}
}
