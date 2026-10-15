namespace HotelReservas.Views;

public partial class InicioPage : ContentPage
{
    public InicioPage()
    {
        InitializeComponent();
    }

    // Los módulos abren el prototipo funcional (Views/PrototipoPage) en la pantalla correspondiente.
    private async void IrAReservas(object sender, EventArgs e) => await Shell.Current.GoToAsync("prototipo?vista=disp");
    private async void IrAHabitaciones(object sender, EventArgs e) => await Shell.Current.GoToAsync("prototipo?vista=habs");
    private async void IrAHuespedes(object sender, EventArgs e) => await Shell.Current.GoToAsync("prototipo?vista=hist");
    private async void IrAReportes(object sender, EventArgs e) => await Shell.Current.GoToAsync("prototipo?vista=ia");
}
