namespace MagicMouse.Windows.App;

public sealed partial class MainWindow
{
    private bool _reportingUiFailure;
    private async Task RunUiActionAsync(string operation,Func<Task> action)
    {
        if(_closed) return;
        try { await action(); }
        catch(Exception exception)
        {
            await LogAsync($"{operation} failed: {exception}");
            if(_closed || _reportingUiFailure) return;
            _reportingUiFailure=true;
            try { await ShowSheetAsync("No se pudo completar la acción","Puedes continuar usando la app. "+exception.Message); }
            catch(Exception reportException) { await LogAsync($"Error dialog unavailable: {reportException}"); }
            finally { _reportingUiFailure=false; }
        }
    }
}
