using System.Runtime.InteropServices;
namespace AnkaStudio;
public static class DeviceMemory
{
    [StructLayout(LayoutKind.Sequential)] struct MemoryStatus {public uint Length,Load;public ulong TotalPhysical,AvailablePhysical,TotalPage,AvailablePage,TotalVirtual,AvailableVirtual,Extended;}
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GlobalMemoryStatusEx(ref MemoryStatus value);
    public static string Note(){var value=new MemoryStatus{Length=(uint)Marshal.SizeOf<MemoryStatus>()};if(!GlobalMemoryStatusEx(ref value))return "Modelin indirme boyutu RAM gereksinimi değildir. Küçük modelle deneme yap; gerçek kullanım donanıma ve metin uzunluğuna bağlıdır.";double total=value.TotalPhysical/1073741824d,available=value.AvailablePhysical/1073741824d;return $"Bilgisayar belleği: {total:0.0} GB • şu an kullanılabilir: {available:0.0} GB. "+(available<8?"Önce küçük modelle deneme yap; diğer programları kapatmak boş belleği artırabilir. ":"Seçili modeli önce dene; metin uzunluğu bellek kullanımını artırır. ")+"Modelin disk boyutu RAM gereksinimi değildir; bu ölçüm hız garantisi vermez.";}
}
