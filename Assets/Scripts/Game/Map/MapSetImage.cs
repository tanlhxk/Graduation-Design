using UnityEngine;
using Game.RogueLike;

namespace Game.RogueLike
{
    [CreateAssetMenu(fileName = "MapSet", menuName = "Data/MapSet")]
    public class MapSetImage : ScriptableObject
    {
        [System.Serializable]
        public struct MapImage
        {
            public NodeType type;
            public Sprite icon;
        }
        public MapImage[] images;

        public Sprite GetSprite(NodeType type)
        {
            foreach (var image in images)
            {
                if (image.type == type) return image.icon;
            }
            return null;
        }
    }
}