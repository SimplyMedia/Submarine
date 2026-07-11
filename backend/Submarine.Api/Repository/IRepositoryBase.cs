using System.Linq.Expressions;

namespace Submarine.Api.Repository;

/// <summary>
///     Base Repository abstraction
/// </summary>
/// <typeparam name="T">Entity Type</typeparam>
public interface IRepositoryBase<T>
{
	/// <summary>
	///     Finds all entities in this Repository
	/// </summary>
	/// <returns>List of entities</returns>
	Task<List<T>> FindAllAsync();

	/// <summary>
	///     Finds entities by condition
	/// </summary>
	/// <param name="expression">condition of entities to find</param>
	/// <returns>List of matching entities</returns>
	Task<List<T>> FindByConditionAsync(Expression<Func<T, bool>> expression);

	/// <summary>
	///     Finds first entity matching condition
	/// </summary>
	/// <param name="expression">condition of entity to find</param>
	/// <returns>Entity matching condition if found</returns>
	Task<T?> FirstByConditionAsync(Expression<Func<T, bool>> expression);

	/// <summary>
	///     Creates an entity in the Database
	/// </summary>
	/// <param name="entity">entity to create</param>
	/// <returns>created entity</returns>
	Task<T> CreateAsync(T entity);

	/// <summary>
	///     Creates collection of entities in the Database
	/// </summary>
	/// <param name="entities">entities to create</param>
	/// <returns>collection of created entities</returns>
	Task<ICollection<T>> CreateAsync(ICollection<T> entities);

	/// <summary>
	///     Updates an entity in the Database
	/// </summary>
	/// <param name="entity">entity to update</param>
	/// <returns>updated entity</returns>
	Task UpdateAsync(T entity);

	/// <summary>
	///     Updates enumerable of entities in the Database
	/// </summary>
	/// <param name="entities">entities to update</param>
	/// <returns></returns>
	Task UpdateAsync(IEnumerable<T> entities);

	/// <summary>
	///     Deletes entity from the Database
	/// </summary>
	/// <param name="entity">entity to delete</param>
	/// <returns></returns>
	Task DeleteAsync(T entity);

	/// <summary>
	///     Deletes enumerable of entities from the Database
	/// </summary>
	/// <param name="entities">entities to delete</param>
	/// <returns></returns>
	Task DeleteAsync(IEnumerable<T> entities);
}
