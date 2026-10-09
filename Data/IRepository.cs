using System.Linq.Expressions;

namespace AccountingSystem.Data
{
	public interface IRepository<T> where T : class
	{
		Task<T?> GetByIdAsync(int id);
		Task<IReadOnlyList<T>> GetAllAsync();
		Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate);
		Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
		Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);
		Task<int> CountAsync(Expression<Func<T, bool>> predicate);
		Task AddAsync(T entity);
		void Update(T entity);
		void Remove(T entity);
	}
}
