using System.Collections.Generic;
using Ruka.Core.Resources;
using Ruka.Core.Symbols;

namespace Ruka.Core.StaticData
{
    internal sealed class StaticDataGroup : IStaticDataGroup
    {
        private readonly Dictionary<string, string> _files;

        public Symbol<AssetTag> Tag { get; }
        public IReadOnlyList<string> FileNames { get; }

        public StaticDataGroup(
            Symbol<AssetTag> tag,
            Dictionary<string, string> files,
            List<string> fileNames)
        {
            Tag = tag;
            _files = files;
            FileNames = fileNames;
        }

        public string ReadText(string fileName)
        {
            if (_files.TryGetValue(fileName, out var content))
                return content;

            throw new KeyNotFoundException(
                $"File '{fileName}' not found in static data tag '{Tag.Value}'.");
        }

        public bool TryReadText(string fileName, out string content)
        {
            return _files.TryGetValue(fileName, out content);
        }
    }
}
