using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ImageResizer.Caching.Core.Extensions
{
    public static class EnumerableExtensions
    {

        /// <summary>
        /// Method for parallelism in async, initially written by Stephen Toub,
        /// https://devblogs.microsoft.com/pfxteam/implementing-a-simple-foreachasync/
        /// </summary>
        public static Task ForEachAsync<TSource, TResult>(this IEnumerable<TSource> source, int maxParallelism, Func<TSource, Task<TResult>> taskSelector, Action<TSource, TResult> resultProcessor)
        {
            var waitHandle = new SemaphoreSlim(maxParallelism);
            return Task.WhenAll(
                from item in source
                select ProcessAsync(item, taskSelector, resultProcessor, waitHandle));
        }

        public static Task ForEachAsync<TSource>(this IEnumerable<TSource> source, int maxParallelism, Func<TSource, Task> taskProcessor)
        {
            var waitHandle = new SemaphoreSlim(maxParallelism);
            return Task.WhenAll(
                from item in source
                select ProcessAsync(item, taskProcessor, waitHandle));
        }
        
        private static async Task ProcessAsync<TSource>(TSource item, Func<TSource, Task> resultProcessor, SemaphoreSlim waitHandle)
        {
            await waitHandle.WaitAsync();

            try
            {
                await resultProcessor(item);
            }
            finally
            {
                waitHandle.Release();
            }
        }

        private static async Task ProcessAsync<TSource, TResult>(TSource item, Func<TSource, Task<TResult>> taskSelector, Action<TSource, TResult> resultProcessor, SemaphoreSlim waitHandle)
        {
            TResult result;

            try 
            { 
                result = await taskSelector(item); 
            }
            catch (Exception) 
            { 
                return; 
            }
            
            await waitHandle.WaitAsync();

            try 
            { 
                resultProcessor(item, result); 
            }
            finally 
            { 
                waitHandle.Release(); 
            }
        }
    }
}
