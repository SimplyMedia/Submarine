namespace Submarine.Core.MediaFile.Naming;

/// <summary>
///     The result of rendering a naming template
/// </summary>
/// <param name="Name">The rendered, filesystem-safe name</param>
/// <param name="UsedPlaceholderTitle">If a placeholder was substituted for a missing Episode title</param>
public record RenderedName(string Name, bool UsedPlaceholderTitle);
