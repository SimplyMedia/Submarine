using Microsoft.EntityFrameworkCore;
using Submarine.Core.AutoTagging;
using Submarine.Core.Entities;
using Submarine.Core.Events;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.AutoTagging;

/// <summary>
///     Applies auto tagging rules whenever a series or movie is added or refreshed, adding matching rule tags and,
///     for rules with <see cref="AutoTaggingRule.RemoveTagsAutomatically" />, removing tags that no longer match.
/// </summary>
public sealed class AutoTaggingApplier(SubmarineDbContext db) :
	IEventHandler<SeriesAddedEvent>,
	IEventHandler<SeriesUpdatedEvent>,
	IEventHandler<MovieAddedEvent>,
	IEventHandler<MovieUpdatedEvent>
{
	/// <inheritdoc />
	public Task HandleAsync(SeriesAddedEvent @event, CancellationToken cancellationToken = default)
		=> ApplyToSeriesAsync(@event.SeriesId, cancellationToken);

	/// <inheritdoc />
	public Task HandleAsync(SeriesUpdatedEvent @event, CancellationToken cancellationToken = default)
		=> ApplyToSeriesAsync(@event.SeriesId, cancellationToken);

	/// <inheritdoc />
	public Task HandleAsync(MovieAddedEvent @event, CancellationToken cancellationToken = default)
		=> ApplyToMovieAsync(@event.MovieId, cancellationToken);

	/// <inheritdoc />
	public Task HandleAsync(MovieUpdatedEvent @event, CancellationToken cancellationToken = default)
		=> ApplyToMovieAsync(@event.MovieId, cancellationToken);

	private async Task ApplyToSeriesAsync(int seriesId, CancellationToken cancellationToken)
	{
		var rules = await db.AutoTaggingRules.Include(x => x.Tags).Where(x => x.Enable).ToListAsync(cancellationToken);
		if (rules.Count == 0)
		{
			return;
		}

		var series = await db.Series.Include(x => x.Tags).Include(x => x.Versions).FirstOrDefaultAsync(x => x.Id == seriesId, cancellationToken);
		if (series is null)
		{
			return;
		}

		var context = new AutoTaggingContext(
			series.Genres,
			[.. series.Versions.Select(x => x.RootFolderId).Distinct()],
			[.. series.Versions.Select(x => x.QualityProfileId).Distinct()],
			series.Type,
			series.Status.ToString(),
			series.Year,
			series.Monitored,
			series.Network,
			series.OriginalLanguage);

		if (Apply(series.Tags, rules, context))
		{
			await db.SaveChangesAsync(cancellationToken);
		}
	}

	private async Task ApplyToMovieAsync(int movieId, CancellationToken cancellationToken)
	{
		var rules = await db.AutoTaggingRules.Include(x => x.Tags).Where(x => x.Enable).ToListAsync(cancellationToken);
		if (rules.Count == 0)
		{
			return;
		}

		var movie = await db.Movies.Include(x => x.Tags).Include(x => x.Versions).FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken);
		if (movie is null)
		{
			return;
		}

		var context = new AutoTaggingContext(
			movie.Genres,
			[.. movie.Versions.Select(x => x.RootFolderId).Distinct()],
			[.. movie.Versions.Select(x => x.QualityProfileId).Distinct()],
			SeriesType: null,
			movie.Status.ToString(),
			movie.Year,
			movie.Monitored,
			movie.Studio,
			movie.OriginalLanguage,
			movie.Keywords);

		if (Apply(movie.Tags, rules, context))
		{
			await db.SaveChangesAsync(cancellationToken);
		}
	}

	private static bool Apply(ICollection<Tag> tags, IReadOnlyList<AutoTaggingRule> rules, AutoTaggingContext context)
	{
		var changed = false;

		foreach (var rule in rules)
		{
			if (AutoTaggingCalculator.Match(rule, context))
			{
				foreach (var tag in rule.Tags.Where(tag => tags.All(existing => existing.Id != tag.Id)))
				{
					tags.Add(tag);
					changed = true;
				}
			}
			else if (rule.RemoveTagsAutomatically)
			{
				foreach (var tag in rule.Tags)
				{
					var existing = tags.FirstOrDefault(t => t.Id == tag.Id);
					if (existing is not null)
					{
						tags.Remove(existing);
						changed = true;
					}
				}
			}
		}

		return changed;
	}
}
