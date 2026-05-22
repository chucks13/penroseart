using UnityEngine;
using Random = UnityEngine.Random;
/// <summary>
/// Wipes from source to destination by projecting tile positions along a randomized direction.
/// </summary>
public class DirectionalWipe : TransitionBase
{

    float angle;
    float diagonalsize;

    /// <summary>
    /// Performs one-time transition setup after reflection creates this instance.
    /// </summary>
    public override void Init()
    {
        base.Init();
        CacheGeometry();
    }

    /// <summary>
    /// Initializes per-run transition state before effect-to-effect blending begins.
    /// </summary>
    public override void OnStart()
    {
        buffer.Clear();
        angle = Random.value * Mathf.PI * 2f;
    }

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// Cache Penrose bounds once so transition-as-blender usage does not depend
    /// on a previous transition activation calling OnStart().
    /// </summary>
    private void CacheGeometry()
    {
        var width = (int)controller.penrose.Bounds.size.x.Round();
        var height = (int)controller.penrose.Bounds.size.y.Round();
        Vector2 diagonal = new Vector2(width, height);
        diagonalsize = diagonal.magnitude;
    }

    /// <summary>
    /// Rotates a 2D point by delta radians for directional wipe projection.
    /// </summary>
    public static Vector2 rotate(Vector2 v, float delta)
    {
        return new Vector2(
            v.x * Mathf.Cos(delta) - v.y * Mathf.Sin(delta),
            v.x * Mathf.Sin(delta) + v.y * Mathf.Cos(delta)
        );
    }

    /// <summary>
    /// Draws source and destination effects and writes the transition frame into buffer.
    /// </summary>
    public override void Draw()
    {
        controller.effects[A].Draw();
        controller.effects[B].Draw();
        Draw2(buffer, controller.effects[A].buffer, controller.effects[B].buffer, V, angle);
    }

    /// <summary>
    /// Shared directional wipe implementation for normal transitions and external blending.
    /// </summary>
    private void Draw2(Color[] dest, Color[] src1, Color[] src2, float V2, float Angle2)
    {

        for (int i = 0; i < buffer.Length; i++)
        {
            Vector2 point = rotate(controller.penrose.tiles[i].position, Angle2);

            if ((point.x + diagonalsize / 2) * 1f >= diagonalsize * V2)
            {
                dest[i] = src1[i];
            }
            else
            {
                dest[i] = src2[i];
            }
        }
    }

    /// <summary>
    /// Uses this transition algorithm as an external-source blender.
    /// </summary>
    public override void Blend(Color[] dest, Color[] src1, Color[] src2)
    {
        float V2 = 0.5f;
        float Angle2 = 0;
        if (settings.Length > 0)
            V2 = settings[0];
        if (settings.Length > 1)
            Angle2 = settings[1];
        Draw2(dest, src1, src2, V2, Angle2);
    }
    /// <summary>
    /// Returns the external-blender fader argument format for this transition.
    /// </summary>
    public override string Usage()
    {
        return "[ratio] [angle (radians)]";
    }


}