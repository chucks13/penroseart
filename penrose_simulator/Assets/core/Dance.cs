using System;
using UnityEngine;

[Serializable]
public class Dance 
{
    public float beat;                // On the beat, time since last beat, otherwise zero
    public float fixedTime;       // use instead of fixedtime
    public float deltaTime;  // use instead of deltatime
    public float decay;         // ramps down from 1 to zero across beat;
    private float lastTime;
    private float duration;
    private bool trigger;

    // Start is called before the first frame update
    public void Init()
    {
        beat = 0f;
        lastTime = Time.fixedTime;
        fixedTime = lastTime;
        duration = 0f;
        decay = 0f;
        trigger = false;

    }

    public void markBeat()
    {
        trigger = true;
    }

    // Update is called once per frame
    public void Update()
    {
        // simple interface, when is the beat, and how long since last beat.
        float now = Time.fixedTime;
        float delta = Time.fixedDeltaTime;
        if (trigger)
        {
            duration = now - lastTime;
            lastTime = now;
            fixedTime = lastTime;
            beat = duration;
        }
        else
            beat = 0f;
        trigger = false;
        // dance interface, speed up time after the beat, then slow it down after
        if (duration != 0f)
        {
            float progress = now - lastTime;                // how long since beat started
            float split = lastTime + (duration * 0.25f); // when time boost ends
            float fraction = progress / duration;
            if (now < split)                                    // are we in the boost part
            {
                deltaTime = delta * (50.0f / 25.0f);    // compress time
                fixedTime += deltaTime;
            }
            else if (progress < 1.0)
            {
                deltaTime = delta * (50.0f / 75.0f);
                fixedTime += deltaTime;
            }
            else
            {
                deltaTime = delta;    // beat is over
                fixedTime = now;
                duration = 0f;
                decay = 0f;
            }
            decay = 1.0f - fraction;
        }
        else
        {
            deltaTime = delta;    // no beats
            fixedTime = now;
            decay = 0f;
        }

    }
}
