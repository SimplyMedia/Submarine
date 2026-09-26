using Microsoft.EntityFrameworkCore;
using Submarine.Core.Commands;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Import;

/// <summary>
///     Re-imports a series' version folders in place, adopting untracked files and dropping rows for
///     files that vanished from disk.
/// </summary>
public sealed class RescanSeriesCommandHandler(SubmarineDbContext db, IImportService importService) : ICommandHandler<RescanSeriesCommand>
{
	/// <inheritdoc />
	public async Task ExecuteAsync(RescanSeriesCommand command, ICommandContext context, CancellationToken cancellationToken = default)
	{
		var seriesIds = command.SeriesId is { } id ? [id] : await db.Series.Select(x => x.Id).ToListAsync(cancellationToken);
		var total = seriesIds.Count;
		for (var index = 0; index < total; index++)
		{
			await importService.RescanSeriesAsync(seriesIds[index], cancellationToken);
			await context.ReportProgressAsync((index + 1) * 100 / Math.Max(total, 1), cancellationToken: cancellationToken);
		}
	}
}

/// <summary>
///     Re-imports a movie's version folders in place, adopting untracked files and dropping rows for
///     files that vanished from disk.
/// </summary>
public sealed class RescanMovieCommandHandler(SubmarineDbContext db, IImportService importService) : ICommandHandler<RescanMovieCommand>
{
	/// <inheritdoc />
	public async Task ExecuteAsync(RescanMovieCommand command, ICommandContext context, CancellationToken cancellationToken = default)
	{
		var movieIds = command.MovieId is { } id ? [id] : await db.Movies.Select(x => x.Id).ToListAsync(cancellationToken);
		var total = movieIds.Count;
		for (var index = 0; index < total; index++)
		{
			await importService.RescanMovieAsync(movieIds[index], cancellationToken);
			await context.ReportProgressAsync((index + 1) * 100 / Math.Max(total, 1), cancellationToken: cancellationToken);
		}
	}
}
