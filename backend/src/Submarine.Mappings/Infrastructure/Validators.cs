using FluentValidation;
using Submarine.Contracts.Mappings;

namespace Submarine.Mappings.Infrastructure;

public sealed class CreateSceneMappingRequestValidator : AbstractValidator<CreateSceneMappingRequest>
{
	public CreateSceneMappingRequestValidator()
	{
		RuleFor(x => x.TvdbId).GreaterThan(0);
		RuleFor(x => x.Title).NotEmpty();
		RuleFor(x => x.SeasonNumber).GreaterThanOrEqualTo(0).When(x => x.SeasonNumber.HasValue);
		RuleFor(x => x.SceneSeasonNumber).GreaterThanOrEqualTo(0).When(x => x.SceneSeasonNumber.HasValue);
	}
}

public sealed class UpdateSceneMappingRequestValidator : AbstractValidator<UpdateSceneMappingRequest>
{
	public UpdateSceneMappingRequestValidator()
	{
		RuleFor(x => x.TvdbId).GreaterThan(0);
		RuleFor(x => x.Title).NotEmpty();
		RuleFor(x => x.SeasonNumber).GreaterThanOrEqualTo(0).When(x => x.SeasonNumber.HasValue);
		RuleFor(x => x.SceneSeasonNumber).GreaterThanOrEqualTo(0).When(x => x.SceneSeasonNumber.HasValue);
	}
}

public sealed class CreateSceneEpisodeMappingRequestValidator : AbstractValidator<CreateSceneEpisodeMappingRequest>
{
	public CreateSceneEpisodeMappingRequestValidator()
	{
		RuleFor(x => x.TvdbId).GreaterThan(0);
		RuleFor(x => x.SeasonNumber).GreaterThanOrEqualTo(0);
		RuleFor(x => x.EpisodeNumber).GreaterThanOrEqualTo(0);
		RuleFor(x => x.SceneSeasonNumber).GreaterThanOrEqualTo(0);
		RuleFor(x => x.SceneEpisodeNumber).GreaterThanOrEqualTo(0);
	}
}

public sealed class UpdateSceneEpisodeMappingRequestValidator : AbstractValidator<UpdateSceneEpisodeMappingRequest>
{
	public UpdateSceneEpisodeMappingRequestValidator()
	{
		RuleFor(x => x.TvdbId).GreaterThan(0);
		RuleFor(x => x.SeasonNumber).GreaterThanOrEqualTo(0);
		RuleFor(x => x.EpisodeNumber).GreaterThanOrEqualTo(0);
		RuleFor(x => x.SceneSeasonNumber).GreaterThanOrEqualTo(0);
		RuleFor(x => x.SceneEpisodeNumber).GreaterThanOrEqualTo(0);
	}
}

public sealed class CreateSceneNameRequestValidator : AbstractValidator<CreateSceneNameRequest>
{
	public CreateSceneNameRequestValidator()
	{
		RuleFor(x => x.TvdbId).GreaterThan(0);
		RuleFor(x => x.SceneName).NotEmpty();
		RuleFor(x => x.SeasonNumber).GreaterThanOrEqualTo(0).When(x => x.SeasonNumber.HasValue);
	}
}

public sealed class UpdateSceneNameRequestValidator : AbstractValidator<UpdateSceneNameRequest>
{
	public UpdateSceneNameRequestValidator()
	{
		RuleFor(x => x.TvdbId).GreaterThan(0);
		RuleFor(x => x.SceneName).NotEmpty();
		RuleFor(x => x.SeasonNumber).GreaterThanOrEqualTo(0).When(x => x.SeasonNumber.HasValue);
	}
}

public sealed class CreateAniListMappingRequestValidator : AbstractValidator<CreateAniListMappingRequest>
{
	public CreateAniListMappingRequestValidator()
	{
		RuleFor(x => x.AniListId).GreaterThan(0);
		RuleFor(x => x.TvdbId).GreaterThan(0);
		RuleFor(x => x.Title).NotEmpty();
		RuleFor(x => x.TvdbSeason).GreaterThanOrEqualTo(0);
		RuleFor(x => x.EpisodeStart).GreaterThanOrEqualTo(1);
		RuleFor(x => x.EpisodeCount).GreaterThanOrEqualTo(1).When(x => x.EpisodeCount.HasValue);
	}
}

public sealed class UpdateAniListMappingRequestValidator : AbstractValidator<UpdateAniListMappingRequest>
{
	public UpdateAniListMappingRequestValidator()
	{
		RuleFor(x => x.AniListId).GreaterThan(0);
		RuleFor(x => x.TvdbId).GreaterThan(0);
		RuleFor(x => x.Title).NotEmpty();
		RuleFor(x => x.TvdbSeason).GreaterThanOrEqualTo(0);
		RuleFor(x => x.EpisodeStart).GreaterThanOrEqualTo(1);
		RuleFor(x => x.EpisodeCount).GreaterThanOrEqualTo(1).When(x => x.EpisodeCount.HasValue);
	}
}
