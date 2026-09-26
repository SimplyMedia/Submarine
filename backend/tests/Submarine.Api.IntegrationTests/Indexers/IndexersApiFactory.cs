using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.IntegrationTests.Indexers;

/// <summary>
///     Hosts the real API against a temporary Sqlite database for indexer/search/grab/newznab integration tests.
/// </summary>
public sealed class IndexersApiFactory : SubmarineApiFactory
{
}
