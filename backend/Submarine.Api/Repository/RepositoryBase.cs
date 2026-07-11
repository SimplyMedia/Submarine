using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Models.Database;

namespace Submarine.Api.Repository;

/// <summary>
///     Repository Base implementation
/// </summary>
/// <typeparam name="T">Entity Type</typeparam>
public abstract class RepositoryBase<T> : IRepositoryBase<T> where T : class
{
	/// <summary>
	///     Database Context of this Repository
	/// </summary>
	protected SubmarineDatabaseContext DatabaseContext { get; }

	private DbSet<T> Set
		=> DatabaseContext.Set<T>();

	/// <summary>
	///     Creates a new instance of <see cref="RepositoryBase{T}" />
	/// </summary>
	/// <param name="databaseContext">database context</param>
	protected RepositoryBase(SubmarineDatabaseContext databaseContext)
		=> DatabaseContext = databaseContext;

	/// <inheritdoc />
	public Task<List<T>> FindAllAsync()
		=> Set.AsNoTracking().ToListAsync();

	/// <inheritdoc />
	public Task<List<T>> FindByConditionAsync(Expression<Func<T, bool>> expression)
		=> Set.Where(expression).AsNoTracking().ToListAsync();

	/// <inheritdoc />
	public Task<T?> FirstByConditionAsync(Expression<Func<T, bool>> expression)
		=> Set.Where(expression).AsNoTracking().FirstOrDefaultAsync();

	/// <inheritdoc />
	public async Task<T> CreateAsync(T entity)
	{
		await Set.AddAsync(entity);
		await SaveAsync();
		return entity;
	}

	/// <inheritdoc />
	public async Task<ICollection<T>> CreateAsync(ICollection<T> entities)
	{
		await Set.AddRangeAsync(entities);
		await SaveAsync();
		return entities;
	}

	/// <inheritdoc />
	public async Task UpdateAsync(T entity)
	{
		Set.Update(entity);
		await SaveAsync();
	}

	/// <inheritdoc />
	public async Task UpdateAsync(IEnumerable<T> entities)
	{
		Set.UpdateRange(entities);
		await SaveAsync();
	}

	/// <inheritdoc />
	public async Task DeleteAsync(T entity)
	{
		Set.Remove(entity);
		await SaveAsync();
	}

	/// <inheritdoc />
	public async Task DeleteAsync(IEnumerable<T> entities)
	{
		Set.RemoveRange(entities);
		await SaveAsync();
	}

	protected Task SaveAsync()
		=> DatabaseContext.SaveChangesAsync();
}
