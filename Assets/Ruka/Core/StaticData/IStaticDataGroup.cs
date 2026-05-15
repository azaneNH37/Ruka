using System.Collections.Generic;
using Ruka.Core.Resources;
using Ruka.Core.Symbols;

namespace Ruka.Core.StaticData
{
    public interface IStaticDataGroup
    {
        Symbol<AssetTag> Tag { get; }
        IReadOnlyList<string> FileNames { get; }

        string ReadText(string fileName);
        bool TryReadText(string fileName, out string content);
    }
}
