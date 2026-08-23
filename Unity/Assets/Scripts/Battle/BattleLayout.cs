using UnityEngine;

namespace Game.Battle
{
    /// <summary>Three-panel side-view layout: allies dock left, enemies dock right, and
    /// the gap between the two front ranks is the empty centre "stage" where
    /// BattleVisuals.MoveToStage/ReturnToDock actually stage a turn's action -- see
    /// BattleController.RunBattle. Column is still the only spatial axis that matters
    /// for targeting/range (TargetResolver) -- only the *presentation* X position
    /// changed from a single continuous line to two clustered docks.</summary>
    public static class BattleLayout
    {
        // DockColumnSpacing must stay comfortably above the widest expected sprite's
        // world-space width or adjacent units overlap -- confirmed visually via a real
        // build, not assumed (a 3-unit-wide sprite at 2.4 spacing physically overlapped
        // its neighbour, and a first pass at 1.9 for this three-panel layout overlapped
        // Husk/Stinger the same way -- see M8's visual verification). Units normalize to
        // TargetUnitHeight regardless of source resolution/aspect (see BattleVisuals)
        // instead of a flat scale multiplier, so a square-ish sprite at that height is
        // ~TargetUnitHeight wide in the worst case -- 3.6 (the original single-line
        // layout's proven-safe spacing) leaves comfortable margin above that.
        public const float TargetUnitHeight = 3.0f;
        public const float DockColumnSpacing = 3.6f;

        // Distance of each side's front rank from screen centre. Equal to
        // DockColumnSpacing so the three panels read as roughly equal thirds: each
        // dock's own front-to-back span is 2*DockColumnSpacing wide, and the reserved
        // centre stage (2*DockFrontOffset wide) matches that. ApplyBattleCamera widens
        // orthographicSize to fit the back rank (front + 2*spacing) comfortably inside
        // the view at a typical widescreen aspect.
        public const float DockFrontOffset = 3.6f;

        public const float GroundY = -2.1f;

        // Player columns 0(back)..2(front) cluster left of centre, front nearest centre.
        // Enemy columns 3(front)..5(back) mirror on the right, so the two front-liners
        // (2 vs 3) are still the pair closest to screen centre.
        public static float ColumnToWorldX(int column) => column <= 2
            ? -DockFrontOffset - (2 - column) * DockColumnSpacing
            : DockFrontOffset + (column - 3) * DockColumnSpacing;

        public static Vector3 UnitPosition(int column) => new(ColumnToWorldX(column), GroundY, 0f);

        // True screen centre, regardless of faction or the acting unit's rank -- a
        // shallow offset (the old StageOffset = 2.0, well short of centre) read as
        // barely moving at all, especially for an already-frontline attacker, and didn't
        // land as "meeting in the middle" (confirmed by the project owner watching it
        // play out). BattleVisuals.MoveToStage moves only the acting unit here; the
        // target stays on its own dock throughout.
        public static Vector3 StagePosition() => new(0f, GroundY, 0f);

        public static void ApplyBattleCamera(Camera cam)
        {
            cam.orthographic = true;
            cam.orthographicSize = 7.0f;
            cam.transform.position = new Vector3(0f, -0.2f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 40f;
            cam.allowMSAA = false;
        }
    }
}
