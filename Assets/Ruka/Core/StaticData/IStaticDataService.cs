using System.Threading;
using Cysharp.Threading.Tasks;
using Ruka.Core.Resources;
using Ruka.Core.Symbols;

namespace Ruka.Core.StaticData
{
    public interface IStaticDataService
    {
        UniTask<IStaticDataGroup> LoadGroupAsync(Symbol<AssetTag> tag, CancellationToken ct = default);
    }
}
