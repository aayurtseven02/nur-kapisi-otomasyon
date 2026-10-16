namespace AnkaStudio;

/// <summary>Compatibility data only; new projects use local production and direct YouTube.</summary>
public static class LegacyVideoState
{
    public static void Clear(Episode e)
    {
        e.RemoteUploadJob=e.RemoteUploadNotifiedJob=e.RemoteUploadRepo=e.RemoteUploadNote=e.RemoteUploadState=e.RemoteState=e.RemoteRepo=e.RemoteRunId="";
        e.RemoteUploadRelease=e.RemoteUploadAsset=0;e.RemoteUploadSchedule=false;
    }
}
