using System.Threading;
using System.Threading.Tasks;

namespace Synchro
{
    public delegate Task<TResp> Handler<in TReq, TResp>(TReq request, CancellationToken token);
}