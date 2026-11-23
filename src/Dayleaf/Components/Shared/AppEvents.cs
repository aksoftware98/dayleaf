namespace Dayleaf.Components.Shared;

/// <summary>Per-circuit notifications so the sidebar refreshes when spaces or pages change.</summary>
public sealed class AppEvents
{
    public event Action? SpacesChanged;
    public void NotifySpacesChanged() => SpacesChanged?.Invoke();
}
