using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AccountingSystem.Data
{
	public class Repository<T> : IRepository<T> where T : class
	{
		protected readonly ApplicationDbContext _context;
		protected readonly DbSet<T> _dbSet;

		public Repository(ApplicationDbContext context)
		{
			_context = context;
			_dbSet = context.Set<T>();
		}

		public async Task<T?> GetByIdAsync(int id)
		{
			return await _dbSet.FindAsync(id);
		}

		public async Task<IReadOnlyList<T>> GetAllAsync()
		{
			return await _dbSet.AsNoTracking().ToListAsync();
		}

		public async Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate)
		{
			return await _dbSet.AsNoTracking().Where(predicate).ToListAsync();
		}

		public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
		{
			return await _dbSet.AsNoTracking().FirstOrDefaultAsync(predicate);
		}

		public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
		{
			return await _dbSet.AsNoTracking().AnyAsync(predicate);
		}

		public async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
		{
			return await _dbSet.AsNoTracking().CountAsync(predicate);
		}

		public async Task AddAsync(T entity)
		{
			await _dbSet.AddAsync(entity);
		}

		public void Update(T entity)
		{
			_dbSet.Update(entity);
		}

		public void Remove(T entity)
		{
			_dbSet.Remove(entity);
		}
	}
}
