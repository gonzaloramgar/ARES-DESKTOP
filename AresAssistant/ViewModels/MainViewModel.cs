namespace AresAssistant.ViewModels;

public class MainViewModel : ViewModelBase
{
    private bool _isOverlayMode = true;

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

    public void ToggleMode() => IsOverlayMode = !IsOverlayMode;
}
