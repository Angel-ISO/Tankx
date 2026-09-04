using System.Linq.Expressions;
using backend.domain.entities;
using backend.domain.interfaces;
using backend.persistence;
using Microsoft.EntityFrameworkCore;
using EFCore.BulkExtensions;

namespace backend.application.Repository;
public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
{
    protected readonly TankxContext Context;

    public GenericRepository(TankxContext context)
    {
        Context = context;
    }

    public virtual void Add(T entity)
    {
        Context.Set<T>().Add(entity);
    }

    public virtual void AddRange(IEnumerable<T> entities)
    {
        Context.Set<T>().AddRange(entities);
    }

    public virtual IEnumerable<T> Find(Expression<Func<T, bool>> expression)
    {
        return Context.Set<T>().Where(expression);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        return await Context.Set<T>().AsNoTracking().ToListAsync();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id)
    {
        return await Context.Set<T>().FindAsync(id);
    }
    
    public virtual void Remove(T entity)
    {
        Context.Set<T>().Remove(entity);
    }

    public virtual void RemoveRange(IEnumerable<T> entities)
    {
        Context.Set<T>().RemoveRange(entities);
    }

    public virtual void Update(T entity)
    {
        Context.Set<T>().Update(entity);
    }

    public virtual async Task<(int TotalRecords, IEnumerable<T> Records)> GetAllAsync(
        int pageIndex,
        int pageSize,
        string search)
    {
        var query = Context.Set<T>().AsNoTracking();
        var totalRecords = await query.CountAsync();
        var records = await query
            .OrderBy(entity => entity.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (totalRecords, records);
    }

    public virtual async Task BulkInsertAsync(IEnumerable<T> entities)
    {
        await Context.BulkInsertAsync(entities.ToList());
    }

    public virtual async Task BulkUpdateAsync(IEnumerable<T> entities)
    {
        await Context.BulkUpdateAsync(entities.ToList());
    }

    public virtual async Task BulkDeleteAsync(IEnumerable<T> entities)
    {
        await Context.BulkDeleteAsync(entities.ToList());
    }
}
