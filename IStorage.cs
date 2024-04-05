using System;
using System.Threading.Tasks;

namespace MyFirstConsoleApp
{
    public interface IStorage<T>
    {
        Task<T> ReadAsync();
        Task WriteAsync(T value);
        bool CanWrite { get; }
        bool IsExpired { get; }
    }
}