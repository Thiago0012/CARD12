using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArcaneArena.StoryRoguelite
{
    [CreateAssetMenu(
        fileName = "StoryNpcCatalog",
        menuName = "Arcane Duel/Story Roguelite/NPC Catalog")]
    public sealed class StoryNpcCatalog : ScriptableObject
    {
        [SerializeField] private List<StoryNpcDefinition> definitions = new();
        public IReadOnlyList<StoryNpcDefinition> All => definitions;

        public StoryNpcDefinition Resolve(string npcId) => definitions
            .FirstOrDefault(item => item != null && string.Equals(
                item.NpcId, npcId, StringComparison.Ordinal));

        public void Initialize(IEnumerable<StoryNpcDefinition> values)
        {
            definitions = values?.Where(value => value != null).ToList() ?? new();
        }
    }
}
