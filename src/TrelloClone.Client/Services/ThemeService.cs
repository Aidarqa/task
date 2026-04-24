namespace TrelloClone.Client.Services;

public class ThemeService
{
    public bool IsDark { get; private set; } = true;
    public event Action? Changed;

    public void Toggle() { IsDark = !IsDark; Changed?.Invoke(); }
    public void Set(bool dark) { IsDark = dark; Changed?.Invoke(); }
}
