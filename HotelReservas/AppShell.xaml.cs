namespace HotelReservas;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        // Ruta del prototipo funcional (se abre desde la pantalla de inicio con ?vista=...)
        Routing.RegisterRoute("prototipo", typeof(Views.PrototipoPage));
    }
}
