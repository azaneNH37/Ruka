using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Ruka.Core.StaticData;
using Ruka.Core.Symbols;
using VContainer.Unity;

namespace Ruka.Samples.LubanIntegration
{
    public abstract class LubanTableService<TTables> : IAsyncStartable
        where TTables : class
    {
        private readonly IStaticDataService _staticData;
        private readonly UniTaskCompletionSource _tcs = new();

        public TTables Tables { get; private set; }
        public UniTask WaitUntilReady => _tcs.Task;

        protected abstract Symbol<AssetTag> DataTag { get; }
        protected abstract TTables Assemble(IStaticDataGroup group);

        public LubanTableService(IStaticDataService staticData)
        {
            _staticData = staticData;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            try
            {
                var group = await _staticData.LoadGroupAsync(DataTag, ct);
                Tables = Assemble(group);
                _tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                _tcs.TrySetException(ex);
                throw;
            }
        }
    }
}
