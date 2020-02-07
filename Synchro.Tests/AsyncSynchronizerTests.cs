using NUnit.Framework;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Synchro.Tests
{
    public class AsyncSynchronizerTests
    {
        [TestCase(1000)]
        public async Task synchronize_should_not_parallelize_calls(int numberOfCalls)
        {
            int value = 0;

            var target = new AsyncSynchronizer();
            Task _ = target.RunAsync(CancellationToken.None);

            ConcurrentBag<Task> tasks = new ConcurrentBag<Task>();

            Parallel.For(0, numberOfCalls, i =>
            {
                Task task = target.SynchronizeAsync(async t =>
                {
                    value++;
                    await Task.Yield();

                    return 0;
                }, CancellationToken.None);

                tasks.Add(task);
            });

            await Task.WhenAll(tasks);

            Assert.AreEqual(numberOfCalls, value);
        }

        [TestCase(1000)]
        public async Task wrap_should_not_parallelize_calls(int numberOfCalls)
        {
            int value = 0;

            var target = new AsyncSynchronizer();
            Task _ = target.RunAsync(CancellationToken.None);

            ConcurrentBag<Task> tasks = new ConcurrentBag<Task>();

            Handler<int, int> wrappedMethod = target.WrapSynchronized(async (int r, CancellationToken c) =>
            {
                value++;
                await Task.Yield();

                return 0;
            });

            
            Parallel.For(0, numberOfCalls, i =>
            {
                Task task = Task.Run(() => wrappedMethod(0, CancellationToken.None));

                tasks.Add(task);
            });

            await Task.WhenAll(tasks);

            Assert.AreEqual(numberOfCalls, value);
        }
    }
}