namespace Dayleaf.Components.Shared;

public enum ToastKind { Info, Success, Error }

public sealed record ToastMessage(Guid Id, string Text, ToastKind Kind);

/// <summary>Per-circuit toast queue. The ToastHost in the layout renders it.</summary>
public sealed class ToastService
{
    private readonly List<ToastMessage> _items = [];
    public IReadOnlyList<ToastMessage> Items => _items;
    public event Action? Changed;

    public void Show(string text, ToastKind kind = ToastKind.Info, int ms = 3200)
    {
        var t = new ToastMessage(Guid.NewGuid(), text, kind);
        _items.Add(t);
        if (_items.Count > 4) _items.RemoveAt(0);
        Changed?.Invoke();
        _ = Task.Delay(kind == ToastKind.Error ? ms * 2 : ms).ContinueWith(_ => Dismiss(t.Id));
    }

    public void Success(string text) => Show(text, ToastKind.Success);
    public void Error(string text) => Show(text, ToastKind.Error);

    public void Dismiss(Guid id)
    {
        if (_items.RemoveAll(x => x.Id == id) > 0) Changed?.Invoke();
    }
}
