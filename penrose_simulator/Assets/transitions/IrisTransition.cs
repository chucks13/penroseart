using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
public class IrisTransition : TransitionBase
{

    int direction;
    float diagonalsize;
    public override void OnStart()
    {
        buffer.Clear();
        direction = Random.Range(0, 2);
        var width = (int)controller.penrose.Bounds.size.x.Round();
        var height = (int)controller.penrose.Bounds.size.y.Round();
        Vector2 diagonal = new Vector2(width, height);
        diagonalsize = diagonal.magnitude/2f;
    }

    public override void OnEnd() { }

    public override void Draw()
    {
        controller.effects[A].Draw();
        controller.effects[B].Draw();

        int a1 = A;
        int b1 = B;
        float v1 = V;

        if(direction == 0)
        {
            a1 = B;
            b1 = A;
            v1 = 1f - V;
        }

        float size = v1 * diagonalsize;
        size = size * size;             // squared is faster

        for (int i = 0; i < buffer.Length; i++)
        {
            float dist = controller.penrose.tiles[i].position.sqrMagnitude;
 
            if (dist>size)
            {
                buffer[i] = controller.effects[a1].buffer[i];
            }
            else
            {
                buffer[i] = controller.effects[b1].buffer[i];
            }
        }
    }

}
