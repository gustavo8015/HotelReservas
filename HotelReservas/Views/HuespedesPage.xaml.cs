namespace HotelReservas.Views;

public partial class HuespedesPage : ContentPage
{
    public HuespedesPage()
    {
        InitializeComponent();
    }

    private async void AbrirPrototipo(object sender, EventArgs e) => await Shell.Current.GoToAsync("prototipo?vista=hist");
}
