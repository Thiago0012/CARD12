using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Serialized mission definitions. This must live in its own file, named
    /// after the ScriptableObject, so Unity resolves the asset's script as
    /// MissionCatalog rather than the MissionDefinitionData helper class.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MissionCatalog",
        menuName = "Arcane Arena/Missões/Catálogo")]
    public sealed class MissionCatalog : ScriptableObject
    {
        public const int CurrentCatalogVersion = 1;
        private const string ResourcePath = "Missions/MissionCatalog";
        private static MissionCatalog _runtime;

        [SerializeField] private int catalogVersion = CurrentCatalogVersion;
        [SerializeField] private List<MissionDefinitionData> definitions =
            new();

        public int CatalogVersion => Math.Max(1, catalogVersion);
        public IReadOnlyList<MissionDefinitionData> Definitions => definitions;

        public MissionDefinitionData Find(string missionId)
        {
            return definitions?.FirstOrDefault(item => item != null &&
                string.Equals(item.missionId, missionId,
                    StringComparison.Ordinal));
        }

        public static MissionCatalog LoadRuntime()
        {
            if (_runtime != null)
                return _runtime;

            _runtime = Resources.Load<MissionCatalog>(ResourcePath);
            if (_runtime != null)
            {
                _runtime.Normalize();
                return _runtime;
            }

            // A clean runtime fallback protects both players and builds if a
            // catalog asset is ever missing. It does not conceal a malformed
            // asset: the catalog is still validated in the Editor tests.
            _runtime = CreateInstance<MissionCatalog>();
            _runtime.name = "Catálogo de Missões (Runtime)";
            _runtime.definitions = MissionCatalogDefaults.Create();
            _runtime.Normalize();
            return _runtime;
        }

        public void ReplaceDefinitionsForTests(
            IEnumerable<MissionDefinitionData> values)
        {
            definitions = values?.Where(item => item != null).ToList() ??
                          new List<MissionDefinitionData>();
            Normalize();
        }

        private void OnValidate() => Normalize();

        private void Normalize()
        {
            catalogVersion = Math.Max(1, catalogVersion);
            definitions ??= new List<MissionDefinitionData>();
            definitions.RemoveAll(item => item == null);
            foreach (MissionDefinitionData item in definitions)
                item.Normalize();
            definitions = definitions
                .Where(item => !string.IsNullOrWhiteSpace(item.missionId))
                .GroupBy(item => item.missionId, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
        }
    }
}
