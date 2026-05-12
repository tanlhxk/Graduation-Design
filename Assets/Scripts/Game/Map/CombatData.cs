using Game.RogueLike;

namespace Game.Combat
{
    public static class CombatData
    {
        public static NodeType CurrentNodeType { get; set; }
        public static void Setup(NodeType type)
        {
            CurrentNodeType = type;
        }
    }
}