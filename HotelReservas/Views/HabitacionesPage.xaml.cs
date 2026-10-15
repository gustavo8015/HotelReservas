namespace HotelReservas.Views;

public partial class HabitacionesPage : ContentPage
{
    public HabitacionesPage()
    {
        InitializeComponent();
    }

    private async void AbrirPrototipo(object sender, EventArgs e) => await Shell.Current.GoToAsync("prototipo?vista=habs");
}
