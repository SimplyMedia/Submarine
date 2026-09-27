using Microsoft.EntityFrameworkCore;
using Submarine.Api.Features.Compat.Shared;
using Submarine.Api.Features.Compat.Shared.Realtime;
using Submarine.Core.Modules;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Compat.Radarr;

public sealed class RadarrRealtimeProjector(SubmarineDbContext db, CompatVersionSelection versions) : IRadarrCompatRealtimeProjector
{
	public async Task<object?> ProjectMovieAsync(int movieId, CancellationToken cancellationToken)
	{
		var module = new RadarrModule();
		var movie = await db.Movies.AsNoTracking().Include(x => x.Tags).SingleOrDefaultAsync(x => x.Id == movieId, cancellationToken);
		if (movie is null || await db.CompatLibraryBindings.AnyAsync(x => x.Facade == "radarr" && x.MovieId == movieId && x.Excluded, cancellationToken)) return null;
		return await module.ProjectForRealtimeAsync(db, versions, movie, cancellationToken);
	}
}

public sealed class RadarrRealtimeModule : IServiceModule
{
	public void Register(IServiceCollection services, IConfiguration configuration)
		=> services.AddScoped<IRadarrCompatRealtimeProjector, RadarrRealtimeProjector>();
}
