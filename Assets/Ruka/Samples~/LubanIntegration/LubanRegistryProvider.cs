using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Ruka.Core.Registry;

namespace Ruka.Samples.LubanIntegration
{
    public abstract class LubanRegistryProvider<TTables, TCfg, TKey, TProfile>
        : IRegistryProvider<TKey, TProfile>
        where TTables : class
        where TProfile : IKeyed<TKey>
    {
        private readonly LubanTableService<TTables> _tableService;

        protected LubanRegistryProvider(LubanTableService<TTables> tableService)
        {
            _tableService = tableService;
        }

        protected abstract IEnumerable<TCfg> GetConfigs(TTables tables);
        protected abstract TProfile Convert(TCfg config);

        public async UniTask<IEnumerable<TProfile>> GetAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            await _tableService.WaitUntilReady;
            return GetConfigs(_tableService.Tables).Select(Convert).ToList();
        }
    }
}
