using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ruka.Core.Saves
{
    internal sealed class MigrationRunner<TChannel>
    {
        private readonly Dictionary<int, List<ISaveMigration<TChannel>>> _migrationsByFrom;

        public MigrationRunner(IEnumerable<ISaveMigration<TChannel>> migrations)
        {
            _migrationsByFrom = migrations
                .GroupBy(m => m.FromVersion)
                .ToDictionary(g => g.Key, g => g.OrderBy(m => m.ToVersion).ToList());
        }

        public bool TryMigrate(SaveContainer container, int targetVersion, out string migrationPath, out string errorMessage)
        {
            migrationPath = string.Empty;
            errorMessage = null;

            if (container == null)
            {
                errorMessage = "Container is null.";
                return false;
            }

            if (container.Version == targetVersion)
                return true;

            if (container.Version > targetVersion)
            {
                errorMessage = $"Save version {container.Version} is newer than target {targetVersion}.";
                return false;
            }

            var currentVersion = container.Version;
            var visited = new HashSet<int>();
            var pathBuilder = new StringBuilder();

            while (currentVersion < targetVersion)
            {
                if (!visited.Add(currentVersion))
                {
                    errorMessage = $"Migration loop detected at version {currentVersion}.";
                    return false;
                }

                if (!_migrationsByFrom.TryGetValue(currentVersion, out var candidates) || candidates.Count == 0)
                {
                    errorMessage = $"Missing migration from version {currentVersion} to {targetVersion}.";
                    return false;
                }

                var step = candidates.FirstOrDefault(m => m.ToVersion > currentVersion && m.ToVersion <= targetVersion);
                if (step == null)
                {
                    errorMessage = $"No valid migration step from version {currentVersion} toward target {targetVersion}.";
                    return false;
                }

                step.Migrate(container);
                if (container.Version == currentVersion)
                    container.Version = step.ToVersion;

                if (pathBuilder.Length > 0)
                    pathBuilder.Append("->");
                pathBuilder.Append($"{currentVersion}to{container.Version}");
                currentVersion = container.Version;
            }

            migrationPath = pathBuilder.ToString();
            return true;
        }
    }
}
