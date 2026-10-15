namespace HotelReservas.Views;

public partial class ReservasPage : ContentPage
{
    public ReservasPage()
    {
        InitializeComponent();
    }

    private async void AbrirPrototipo(object sender, EventArgs e) => await Shell.Current.GoToAsync("prototipo?vista=disp");
}
