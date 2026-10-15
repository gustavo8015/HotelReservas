namespace HotelReservas.Views;

public partial class ReportesPage : ContentPage
{
    public ReportesPage()
    {
        InitializeComponent();
    }

    private async void AbrirPrototipo(object sender, EventArgs e) => await Shell.Current.GoToAsync("prototipo?vista=ia");
}
