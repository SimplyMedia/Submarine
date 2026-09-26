using Submarine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Submarine.Core.Commands;
using Submarine.Infrastructure.Commands;

namespace Submarine.Infrastructure.Library;

/// <summary>
///     Refreshes one series or all monitored series.
/// </summary>
public sealed class RefreshSeriesCommandHandler(LibraryRefresher refresher, SubmarineDbContext db)
	: ICommandHandler<RefreshSeriesCommand>
{
	/// <inheritdoc />
	public async Task ExecuteAsync(RefreshSeriesCommand command, ICommandContext context, CancellationToken cancellationToken = default)
	{
		if (command.SeriesId is { } seriesId)
		{
			await refresher.RefreshSeriesAsync(seriesId, cancellationToken);
			await context.ReportProgressAsync(100, null, cancellationToken);
			return;
		}

		var ids = await db.Series.Where(x => x.Monitored).Select(x => x.Id).ToListAsync(cancellationToken);
		var done = 0;
		foreach (var id in ids)
		{
			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				await refresher.RefreshSeriesAsync(id, cancellationToken);
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				// One broken series must not stop the refresh of the rest.
			}

			done++;
			await context.ReportProgressAsync(done * 100 / Math.Max(ids.Count, 1), $"Refreshed {done}/{ids.Count}", cancellationToken);
		}
	}
}

/// <summary>
///     Refreshes one movie or all monitored movies.
/// </summary>
public sealed class RefreshMovieCommandHandler(LibraryRefresher refresher, SubmarineDbContext db)
	: ICommandHandler<RefreshMovieCommand>
{
	/// <inheritdoc />
	public async Task ExecuteAsync(RefreshMovieCommand command, ICommandContext context, CancellationToken cancellationToken = default)
	{
		if (command.MovieId is { } movieId)
		{
			await refresher.RefreshMovieAsync(movieId, cancellationToken);
			await context.ReportProgressAsync(100, null, cancellationToken);
			return;
		}

		var ids = await db.Movies.Where(x => x.Monitored).Select(x => x.Id).ToListAsync(cancellationToken);
		var done = 0;
		foreach (var id in ids)
		{
			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				await refresher.RefreshMovieAsync(id, cancellationToken);
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				// One broken movie must not stop the refresh of the rest.
			}

			done++;
			await context.ReportProgressAsync(done * 100 / Math.Max(ids.Count, 1), $"Refreshed {done}/{ids.Count}", cancellationToken);
		}
	}
}

/// <summary>
///     Refreshes all monitored series and movies. Scheduled by the default task set.
/// </summary>
public sealed class RefreshMetadataCommandHandler(LibraryRefresher refresher)
	: ICommandHandler<RefreshMetadataCommand>
{
	/// <inheritdoc />
	public async Task ExecuteAsync(RefreshMetadataCommand command, ICommandContext context, CancellationToken cancellationToken = default)
	{
		var (series, movies) = await refresher.RefreshAllAsync(
			async (done, total) => await context.ReportProgressAsync(done * 100 / Math.Max(total, 1), $"Refreshed {done}/{total}", cancellationToken),
			cancellationToken);
		await context.ReportProgressAsync(100, $"Refreshed {series} series and {movies} movies", cancellationToken);
	}
}
