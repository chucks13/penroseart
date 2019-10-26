using Random = UnityEngine.Random;
using UnityEngine;

public class Flock : EffectBase {
  private Settings setting;
  private Boid[] flock;

  public override string DebugText() { return$""; }

  public override void OnStart() {
    setting = new Settings();
    var min = controller.penrose.bounds.min;
    var max = controller.penrose.bounds.max;

    if(controller.flockSettings.Length > 0)
      setting = controller.flockSettings[Random.Range(0, controller.juliaSettings.Length)];
    else
      setting.Randomize();

    flock = new Boid[setting.total];
    for(int i = 0; i < setting.total; i++) {
      flock[i] = new Boid(min, max) {
                                      color = Color.HSVToRGB((float)i / setting.total % 1f, 1f, 1f),
                                      boids = flock, setting = setting
                                    };
    }

    buffer.Clear();
  }

  public override void OnEnd() { }

  public override void Draw() {
    buffer.Fade(0.925f);
    for(int i = 0; i < flock.Length; i++) {
      var f = flock[i];
      f.Update();
      buffer[controller.penrose.GetIndexFromPosition(f.position)] = f.color;
    }
  }

  [System.Serializable]
  public class Settings {
    public int total = 80;

    [Range(0f, 10)]
    public float alignment = 0.9f;

    [Range(0f, 10)]
    public float cohesion = 1f;

    [Range(0f, 10)]
    public float separation = 1.25f;

    public void Randomize() { }
  }

  public class Boid {
    public Vector2 position;
    public Vector2 velocity;
    public Vector2 acceleration;
    public Color color;
    public float perception = 3f;
    public float maxSpeed = 20f;
    public float maxForce = 0.25f;
    public Boid[] boids;
    public Settings setting;
    private Vector2 min;
    private Vector2 max;
    private Vector2 alignment;
    private Vector2 cohesion;
    private Vector2 separation;

    public Boid(Vector2 min, Vector2 max) {
      this.min = min;
      this.max = max;
      velocity = new Vector2(Random.Range(-maxSpeed, maxSpeed), Random.Range(-maxSpeed, maxSpeed));
      position = new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y));

      //maxSpeed = Random.Range(5f, 20f);
    }

    public void Update() {
      if(boids == null) return;

      position += Time.deltaTime * velocity;

      UpdateFlock();
      acceleration = (alignment * setting.alignment) + (cohesion * setting.cohesion) +
                     (separation * setting.separation);

      velocity += acceleration;
      velocity = Vector2.ClampMagnitude(velocity, maxSpeed);

      CheckEdges();
    }

    private void CheckEdges() {
      if(position.x > max.x)
        position.x = min.x;
      else if(position.x < min.x) position.x = max.x;

      if(position.y > max.y)
        position.y = min.y;
      else if(position.y < min.y) position.y = max.y;
    }

    private void UpdateFlock() {
      alignment = Vector2.zero;
      cohesion = Vector2.zero;
      separation = Vector2.zero;
      int total = 0;
      for(int i = 0; i < boids.Length; i++) {
        var distance = Vector2.Distance(position, boids[i].position);
        if(boids[i] == this || distance > perception) continue;

        alignment += boids[i].velocity;
        cohesion += boids[i].position;

        var diff = position - boids[i].position;
        diff /= distance * distance;
        separation += diff;

        total++;
      }

      if(total > 0) {
        alignment /= total;
        alignment = alignment.SetMagnitude(maxSpeed);
        alignment -= velocity;
        alignment = Vector2.ClampMagnitude(alignment, maxForce);

        cohesion /= total;
        cohesion -= position;
        cohesion = cohesion.SetMagnitude(maxSpeed);
        cohesion -= velocity;
        cohesion = Vector2.ClampMagnitude(cohesion, maxForce);

        separation /= total;
        separation = separation.SetMagnitude(maxSpeed);
        separation -= velocity;
        separation = Vector2.ClampMagnitude(separation, maxForce);
      }
    }
  }
}