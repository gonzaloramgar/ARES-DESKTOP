namespace AresAssistant.ViewModels;

/// <summary>
/// ViewModel principal de la aplicación.
/// Controla el modo de visualización: overlay compacto o HUD completo.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private bool _isOverlayMode = true;
    private bool _isInitializingModules;
    private string _initializationStatus = "Cargando módulos...";

    public bool IsOverlayMode
    {
        get => _isOverlayMode;
        set
        {
            SetField(ref _isOverlayMode, value);
            OnPropertyChanged(nameof(IsFullHudMode));
        }
    }

    public bool IsFullHudMode => !_isOverlayMode;

    public bool IsInitializingModules
    {
        get => _isInitializingModules;
        set => SetField(ref _isInitializingModules, value);
    }

    public string InitializationStatus
    {
        get => _initializationStatus;
        set => SetField(ref _initializationStatus, value);
    }

    public void ToggleMode() => IsOverlayMode = !IsOverlayMode;
}
