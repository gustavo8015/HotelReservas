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
        Web.Navigating += Web_Navigating;
        Cargar();
    }

    private async void Web_Navigating(object? sender, WebNavigatingEventArgs e)
    {
        // Los enlaces externos (p. ej. wa.me para compartir el comprobante por WhatsApp)
        // se abren con la app correspondiente del teléfono y no dentro del WebView.
        if (e.Url.StartsWith("http://") || e.Url.StartsWith("https://"))
        {
            e.Cancel = true;
            try { await Launcher.Default.OpenAsync(new Uri(e.Url)); }
            catch { await DisplayAlert("Compartir", "No se pudo abrir WhatsApp en este dispositivo.", "OK"); }
        }
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
