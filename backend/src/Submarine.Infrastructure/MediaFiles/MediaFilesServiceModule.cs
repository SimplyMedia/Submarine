using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Submarine.Core.Modules;

namespace Submarine.Infrastructure.MediaFiles;

/// <summary>
///     Registers the ffprobe based media info service.
/// </summary>
public sealed class MediaFilesServiceModule : IServiceModule
{
	/// <inheritdoc />
	public void Register(IServiceCollection services, IConfiguration configuration)
		=> services.AddSingleton<IMediaInfoService, FfprobeMediaInfoService>();
}
