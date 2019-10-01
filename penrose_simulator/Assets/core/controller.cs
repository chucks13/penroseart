using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class controller : MonoBehaviour {

    public Effect[] effects;
    float timeleft = 0;
    public float effectTime;
    int effectNumber = 0;
    public tileDefinitions geometry;


    private Penrose penrose;

    private void Awake() {
      // find the penrose game object in the current scene
      penrose = GameObject.Find("Penrose").GetComponent<Penrose>();
    }

    // Use this for initialization
    void Start () {
        effects = new Effect[3];
        geometry = ScriptableObject.CreateInstance< tileDefinitions>();
        geometry.Init();
        effects[0] = ScriptableObject.CreateInstance<NoiseEffect>();
        effects[1] = ScriptableObject.CreateInstance<colorNibblerEffect>();
        effects[2] = ScriptableObject.CreateInstance < colorSparkleEffect>();
    }
	
	// Update is called once per frame
	void Update () {
        // if we are out of time
        if (timeleft <= 0)
        {
            // reset the timer
            timeleft = effectTime;
            // pick a different effect
            effectNumber += Random.Range(1, effects.Length);
            effectNumber %= effects.Length;
            // initialize it
 //           effectNumber = 0;
            effects[effectNumber].Init(this);
        }
        // update the effect
        effects[effectNumber].Draw(this);
    // copy the effect buffer into the grid object
    penrose.buffer = effects[effectNumber].buffer;
        timeleft -= Time.deltaTime;
	}
}
