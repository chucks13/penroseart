﻿﻿﻿
using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class fluid : ScreenEffect
{
    private float[] state1;
    private float[] state2;
    int slower = 0;
    public float fdamping = 0.95f;
    public float impulse = 1f;
    public int activity = 50;
    public float scale = 10f;
    public float fneighbors = 2f;

    int beatVariant;
    public override string DebugText() { return $""; }

    public override void Init()
    {
        base.Init();
    }
    public override void OnStart()
    {
        base.OnStart();
        state1 = new float[Penrose.Total];
        state2 = new float[Penrose.Total];
        for (int i = 0; i < state1.Length; i++)
        {
            state1[i] = 0f;
            state2[i] = 0f;
        }
    }

    public override void OnEnd() { }

    void generate()
    {
        for (int i = 0; i < state1.Length; i++)
        {
            float total = 0;
            float count = 0;
            for (int j = 0; j < tiles[i].neighbors.Length; j++)
            {
                int n=tiles[i].neighbors[j].tileIdx; 
                if (n >= 0)
                {
                    total += state1[n];
                    count++;
                }
            }
            float neighbors = (total / count);      // average target
            neighbors *= fneighbors;                // weight of neighbors factor    
            float x = neighbors - state2[i];        // how far from current valur we are
            state2[i] = x * fdamping;               // dampening factor
        }
        float[] swap = state1;
        state1 = state2;
        state2 = swap;
    }
    void inject()
    {
        if (Random.Range(0, activity) == 0)
        {
            state1[Random.Range(0, state1.Length)] = impulse;
        }
        //        for(int i=0;i<2460;i+=820)
        //        state1[455+i] = 20f;
    }
    public override void Draw()
    {
        slower++;
        if ((slower % 2) == 0)
            generate();
        inject();
        float beatBrightness = beatManager.GetBeatBrightness(beatVariant, 1.0f, 0.5f, beatEnable);
        for (int i = 0; i < state1.Length; i++)
        {
            float v = state1[i] * scale;
            v += 1000.5f;
            v %= 1f;
            buffer[i] = APalette.read(v) * beatBrightness;
        }
    }

}