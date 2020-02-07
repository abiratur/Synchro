using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Synchro
{
    public class AsyncSynchronizer
    {
        private interface IContext
        {
            Task RunAsync();
        }

        private class Context<TResponse> : IContext
        {
            private readonly TaskCompletionSource<TResponse> _tcs = new TaskCompletionSource<TResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly Func<CancellationToken, Task<TResponse>> _handler;
            private readonly CancellationToken _token;

            public Context(Func<CancellationToken, Task<TResponse>> handler, CancellationToken token)
            {
                _handler = handler;
                _token = token;
            }

            public Task<TResponse> Completion => _tcs.Task;

            public async Task RunAsync()
            {
                try
                {
                    TResponse resp = await _handler(_token);
                    _tcs.TrySetResult(resp);
                }
                catch (OperationCanceledException)
                {
                    _tcs.TrySetCanceled(_token);
                }
                catch (Exception ex)
                {
                    _tcs.TrySetException(ex);
                }
            }
        }

        private class Context<TReq, TResp> : IContext
        {
            private readonly TaskCompletionSource<TResp> _tcs = new TaskCompletionSource<TResp>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly Handler<TReq, TResp> _handler;
            private readonly TReq _request;
            private readonly CancellationToken _token;

            public Context(Handler<TReq, TResp> handler, TReq request, in CancellationToken token)
            {
                _handler = handler;
                _request = request;
                _token = token;
            }

            public Task<TResp> Completion => _tcs.Task;

            public async Task RunAsync()
            {
                try
                {
                    TResp resp = await _handler(_request, _token);
                    _tcs.TrySetResult(resp);
                }
                catch (OperationCanceledException)
                {
                    _tcs.TrySetCanceled(_token);
                }
                catch (Exception ex)
                {
                    _tcs.TrySetException(ex);
                }
            }
        }

        private readonly Channel<IContext> _channel;

        public AsyncSynchronizer()
            :this(1)
        {
        }

        public AsyncSynchronizer(int internalQueueSize)
        {
            _channel = Channel.CreateBounded<IContext>(internalQueueSize);
        }

        public async Task<TResponse> SynchronizeAsync<TResponse>(Func<CancellationToken, Task<TResponse>> handler, CancellationToken token)
        {
            var ctx = new Context<TResponse>(handler, token);

            await _channel.Writer.WriteAsync(ctx, token);

            return await ctx.Completion;
        }

        public Handler<TRequest, TResponse> WrapSynchronized<TRequest, TResponse>(Handler<TRequest, TResponse> handler)
        {
            async Task<TResponse> Handler(TRequest req, CancellationToken token)
            {
                var ctx = new Context<TRequest, TResponse>(handler, req, token);

                await _channel.Writer.WriteAsync(ctx, token);

                return await ctx.Completion;
            }

            return Handler;
        }

        public async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                IContext ctx = await _channel.Reader.ReadAsync(token);

                await ctx.RunAsync();
            }
        }
    }
}