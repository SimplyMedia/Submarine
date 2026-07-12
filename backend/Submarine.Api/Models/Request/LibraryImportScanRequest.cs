using Submarine.Core.Library;

namespace Submarine.Api.Models.Request;

/// <summary>
///     Request to scan an existing organized library for importable media folders
/// </summary>
public record LibraryImportScanRequest
{
	/// <summary>
	///     Path to scan, mutually exclusive with <see cref="RootFolderId" />
	/// </summary>
	public string? Path { get; set; }

	/// <summary>
	///     Id of a Root Folder to scan, mutually exclusive with <see cref="Path" />
	/// </summary>
	public int? RootFolderId { get; set; }

	/// <summary>
	///     Kind of media stored under the scanned path
	/// </summary>
	public MediaKind MediaKind { get; set; }
}
