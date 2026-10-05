using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MagicMouse.Windows.App.Devices;

namespace MagicMouse.Windows.App;

public sealed partial class MainWindow
{
    private UIElement DriverSetupCard()
    {
        var status=new StackPanel { Spacing=12 };
        status.Children.Add(Text("Comprobando Bluetooth y controladores…",12,DesignTokens.SecondaryText));
        async Task Refresh()
        {
            var reading=await DriverReadinessService.ReadAsync(_currentMouseDevice);
            if(_closed || status.XamlRoot is null) return;
            status.Children.Clear();
            void Row(string label,string value)
            {
                var row=new Grid{ColumnSpacing=24};row.ColumnDefinitions.Add(new(){Width=new GridLength(150)});row.ColumnDefinitions.Add(new());
                row.Children.Add(Text(label,12,DesignTokens.BodyText));var detail=Text(value,12,DesignTokens.SecondaryText);detail.LineHeight=18;Grid.SetColumn(detail,1);row.Children.Add(detail);status.Children.Add(row);
            }
            Row("Bluetooth",reading.BluetoothAvailable ? "Adaptador disponible" : "No se detectó un adaptador");
            Row("Componentes HID",reading.WindowsHidAvailable ? "Componentes de Windows disponibles" : "Comprueba las actualizaciones de Windows");
            Row("Magic Mouse",reading.DeviceName ?? "No detectado · empareja y enciende tu mouse");
            Row("Controlador del mouse",reading.DeviceName is null ? "Pendiente de conectar el dispositivo" : reading.Provider is null ? "Windows no expone el nombre del controlador" : $"{reading.Provider}{(reading.Version is null ? "" : " · "+reading.Version)}");
            Row("Superficie multitáctil",_inputConnected && _decodedReports>0 ? $"{_decodedReports} reportes táctiles interpretados" : "Pendiente de verificar con el dispositivo");
        }
        status.Loaded+=async (_,_)=>await RunUiActionAsync("Comprobar controladores",Refresh);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};
        actions.Children.Add(ActionButton("Emparejar Magic Mouse",async()=>await global::Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:bluetooth"))));
        actions.Children.Add(ActionButton("Comprobar",async()=>{await _discovery.RefreshAsync(); await Refresh();}));
        actions.Children.Add(ActionButton("Windows Update",async()=>await global::Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:windowsupdate"))));
        return SectionCard("Controladores y conexión","Prepara tu Magic Mouse y comprueba qué puede leer Windows.",status,actions,Text("La app incluye su entorno de ejecución. Windows gestiona Bluetooth y los controladores HID. Los gestos requieren que el controlador permita leer la superficie táctil; su compatibilidad se verifica al conectar el mouse.",11,DesignTokens.SecondaryText));
    }
}
