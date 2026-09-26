namespace Submarine.Core.Download;

/// <summary>
///     Capabilities and paths reported by a download client
/// </summary>
/// <param name="OutputRootFolders">Folders the client places completed downloads into</param>
public record DownloadClientStatus(IReadOnlyList<string> OutputRootFolders);
