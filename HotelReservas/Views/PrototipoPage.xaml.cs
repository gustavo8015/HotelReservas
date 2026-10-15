namespace HotelReservas.Views;

[QueryProperty(nameof(Vista), "vista")]
public partial class PrototipoPage : ContentPage
{
    private string _vista = "inicio";

    public string Vista
    {
        get => _vista;
        set { _vista = string.IsNullOrWhiteSpace(value) ? "inicio" : value; Cargar(); }
    }

    public PrototipoPage()
    {
        InitializeComponent();
        Cargar();
    }

    private void Cargar()
    {
        // Resources/Raw/prototipo.html se empaqueta como asset de Android (file:///android_asset/).
        // apk=1 activa el modo pantalla completa; vista indica la pantalla inicial (inicio, disp, habs, hist, ia).
        Web.Source = new UrlWebViewSource { Url = $"file:///android_asset/prototipo.html?apk=1&vista={_vista}" };
    }

    protected override bool OnBackButtonPressed()
    {
        // El botón Atrás del teléfono vuelve a la pantalla de inicio nativa.
        _ = Shell.Current.GoToAsync("..");
        return true;
    }
}
