namespace AnkaStudio;

public sealed class LocalVideoRecovery
{
    bool attempted;
    public async Task Run(Func<Exception?,Task> render,Func<Task> restart,CancellationToken ct)
    {
        try{await render(null);}
        catch(Exception error)when(!attempted&&(error is HttpRequestException or InvalidOperationException||error is OperationCanceledException&&!ct.IsCancellationRequested)){
            ct.ThrowIfCancellationRequested();attempted=true;await restart();ct.ThrowIfCancellationRequested();await render(error);
        }
    }
}
