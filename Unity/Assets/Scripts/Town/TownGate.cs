using UnityEngine;

namespace Game.Town
{
    /// <summary>A functional exit from Town (M32) -- unlike TownBuilding, walking up to
    /// one of these actually does something: loads another scene by name. Placed at the
    /// forest's edge rather than inside a district, since leaving town is conceptually
    /// different from visiting a building in it.</summary>
    public class TownGate : MonoBehaviour
    {
        public string DisplayName;
        public string TargetSceneName;

        public string PromptLine => $"Press E -- {DisplayName}";
    }
}
