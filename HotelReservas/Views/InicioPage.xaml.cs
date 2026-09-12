namespace HotelReservas.Views;

public partial class InicioPage : ContentPage
{
    public InicioPage()
    {
        InitializeComponent();
    }

    // Propuesta inicial de navegación: cada módulo se abre por su ruta en el Shell.
    private async void IrAReservas(object sender, EventArgs e) => await Shell.Current.GoToAsync("//reservas");
    private async void IrAHabitaciones(object sender, EventArgs e) => await Shell.Current.GoToAsync("//habitaciones");
    private async void IrAHuespedes(object sender, EventArgs e) => await Shell.Current.GoToAsync("//huespedes");
    private async void IrAReportes(object sender, EventArgs e) => await Shell.Current.GoToAsync("//reportes");
}
