using Dayleaf.Domain;

namespace Dayleaf.Components.Editor;

public enum ElementAction { Front, Back, Duplicate, Delete }

/// <summary>One step of the editor's undo stack (kept in .NET, per session).</summary>
public sealed record UndoEntry(string Label, PageElement? Before, Guid? AddedId);
