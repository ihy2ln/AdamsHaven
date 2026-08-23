using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// A skill's own impact-effect flipbook -- independent of any character's body
    /// animation clip (ClipSet/CharacterDefinition.clips). BattleVisuals.PlayImpactFx
    /// overlays this on the target when a skill has one, falling back to the map's
    /// generic fxImpactSheet otherwise (see MapDefinition), so authoring one is optional
    /// per skill. Deliberately its own asset rather than fields on SkillDefinition: an
    /// effect should be attachable to more than one skill (and later, per the project
    /// owner's "skill orbs" direction, to a skill an orb grants at runtime) without
    /// duplicating art references per skill.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Combat/Skill Effect", fileName = "Effect_")]
    public class SkillEffect : ScriptableObject
    {
        [Tooltip("Packed flipbook sheet + per-frame pixel rects (x,y,w,h), same convention "
            + "as MapDefinition.fxImpactSheet/fxImpactFrameRects.")]
        public Sprite sheet;
        public List<Vector4> frameRects = new();

        [Min(0.01f)] public float worldHeight = 1.8f;
        [Min(0.001f)] public float frameSeconds = 0.045f;
    }
}
