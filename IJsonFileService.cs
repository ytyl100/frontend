using System.Collections.Generic;
using System.Threading.Tasks;

namespace CarPartsInventory.API.Services
{
    public interface IJsonFileService<T>
    {
        Task<List<T>> GetAllAsync();
        Task<T?> GetByIdAsync(string id);
        Task<T> CreateAsync(T item);
        Task<T?> UpdateAsync(string id, T item);
        Task<bool> DeleteAsync(string id);
        Task ReplaceAllAsync(List<T> items);
    }
}
