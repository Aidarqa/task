using Microsoft.AspNetCore.Components;

namespace TrelloClone.Client.Services;

public abstract class LocalizedLayoutBase : LayoutComponentBase, IDisposable
{
    [Inject] protected LocalizationService L { get; set; } = null!;

    private bool _disposed;

    protected override void OnInitialized() => L.Changed += OnLangChanged;

    private void OnLangChanged()
    {
        if (_disposed) return;
        _ = InvokeAsync(StateHasChanged);
    }

    public virtual void Dispose()
    {
        _disposed = true;
        L.Changed -= OnLangChanged;
    }
}
