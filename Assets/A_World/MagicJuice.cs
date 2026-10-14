// A_World/MagicJuice.cs — Magical Princess Fairy sparkles, swirling stars, rising bubbles, and rainbow steam!
// Specially tailored for adorable magic moments that girls and kids love:
//   - Twinkling Star Sparks (pastel pink, lavender, gold, soft cyan)
//   - Swirling Fairy Ring orbiting around an object
//   - Rainbow Fairy Steam & Floating Bubbles
// Zero dependencies, fully procedural, batch-safe and auto-destroying.
// C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;

public static class MagicJuice {
  public static readonly Color MagicPink = new Color(1f, 0.55f, 0.78f);
  public static readonly Color MagicLavender = new Color(0.82f, 0.62f, 1f);
  public static readonly Color MagicGold = new Color(1f, 0.88f, 0.35f);
  public static readonly Color MagicMint = new Color(0.55f, 0.98f, 0.82f);
  public static readonly Color MagicSky = new Color(0.65f, 0.85f, 1f);
  public static readonly Color MagicPeach = new Color(1f, 0.72f, 0.62f);

  public static readonly Color[] MagicPalette = {
    MagicPink, MagicLavender, MagicGold, MagicMint, MagicSky, MagicPeach
  };

  // Spawns a magical swirling burst of fairy stars around a transform
  public static void FairySparkleBurst(Transform parent, Vector3 localCenter, int starCount = 14) {
    if (parent == null) return;
    for (int i = 0; i < starCount; i++) {
      GameObject star = GameObject.CreatePrimitive(PrimitiveType.Cube);
      star.name = "MagicStar_" + i;
      star.transform.SetParent(parent, false);
      star.transform.localPosition = localCenter;
      
      StripCollider(star);
      FairyStarBit bit = star.AddComponent<FairyStarBit>();
      float angle = (i / (float)starCount) * Mathf.PI * 2f;
      bit.Setup(angle, MagicPalette[i % MagicPalette.Length]);
    }
  }

  // Spawns a floating magical heart / sparkle ring that floats up and expands
  public static void MagicHaloRing(Transform parent, Vector3 localPos, Color color, float maxRadius = 1.6f) {
    if (parent == null) return;
    GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    ring.name = "MagicHaloRing";
    ring.transform.SetParent(parent, false);
    ring.transform.localPosition = localPos + new Vector3(0f, 0.05f, 0f);
    ring.transform.localScale = new Vector3(0.1f, 0.005f, 0.1f);
    
    StripCollider(ring);
    Renderer rend = ring.GetComponent<Renderer>();
    if (rend != null) rend.sharedMaterial = Lit(color);
    
    MagicHaloAnimator halo = ring.AddComponent<MagicHaloAnimator>();
    halo.Setup(maxRadius);
  }

  // Spawns procedural floating fairy bubbles that wobble upward and pop
  public static void FairyBubbles(Transform parent, Vector3 localCenter, int bubbleCount = 6) {
    if (parent == null) return;
    for (int i = 0; i < bubbleCount; i++) {
      GameObject bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      bubble.name = "MagicBubble_" + i;
      bubble.transform.SetParent(parent, false);
      bubble.transform.localPosition = localCenter + new Vector3(
        Random.Range(-0.35f, 0.35f),
        Random.Range(0f, 0.2f),
        Random.Range(-0.35f, 0.35f)
      );

      StripCollider(bubble);
      FairyBubbleBit bit = bubble.AddComponent<FairyBubbleBit>();
      bit.Setup(MagicPalette[i % MagicPalette.Length]);
    }
  }

  // Spawns puffing locomotive / dish steam puffs that expand and rise
  public static void MagicPuffSteam(Transform parent, Vector3 localCenter, Color color, float scale = 0.35f) {
    if (parent == null) return;
    GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    puff.name = "MagicSteamPuff";
    puff.transform.SetParent(parent, false);
    puff.transform.localPosition = localCenter;

    StripCollider(puff);
    MagicSteamAnimator anim = puff.AddComponent<MagicSteamAnimator>();
    anim.Setup(color, scale);
  }

  static void StripCollider(GameObject go) {
    try {
      Collider c = go.GetComponent<Collider>();
      if (c != null) CharacterPresentation.DestroyNow(c);
    } catch (System.Exception) { }
  }

  static readonly Dictionary<Color, Material> _mats = new Dictionary<Color, Material>();

  public static Material Lit(Color color) {
    Material cached;
    if (_mats.TryGetValue(color, out cached) && cached != null) return cached;
    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.SetColor("_BaseColor", color);
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
    mat.enableInstancing = true;
    _mats[color] = mat;
    return mat;
  }
}

// Procedural swirling fairy star: spirals upward, twinkles scale, auto-destroys
public class FairyStarBit : MonoBehaviour {
  Color _color;
  float _angle;
  float _speed;
  float _radius;
  float _life = 1.1f;
  float _maxLife = 1.1f;
  float _y;
  Vector3 _baseScale;

  public void Setup(float startAngle, Color color) {
    _angle = startAngle;
    _color = color;
    _speed = UnityEngine.Random.Range(3.5f, 6.0f);
    _radius = UnityEngine.Random.Range(0.25f, 0.65f);
    _y = 0.1f;
    _baseScale = Vector3.one * UnityEngine.Random.Range(0.09f, 0.14f);

    Renderer rend = GetComponent<Renderer>();
    if (rend != null) rend.sharedMaterial = MagicJuice.Lit(_color);
    transform.localScale = _baseScale;
  }

  void Update() {
    float dt = Time.deltaTime;
    if (dt <= 0f) return;
    _life -= dt;
    if (_life <= 0f) {
      Destroy(gameObject);
      return;
    }

    _angle += _speed * dt;
    _radius += dt * 0.35f;
    _y += dt * 0.9f;

    transform.localPosition = new Vector3(
      Mathf.Cos(_angle) * _radius,
      _y,
      Mathf.Sin(_angle) * _radius
    );
    transform.localRotation *= Quaternion.Euler(220f * dt, 400f * dt, 150f * dt);

    float t = Mathf.Clamp01(_life / _maxLife);
    float twinkle = 0.75f + 0.35f * Mathf.Sin(_life * 28f);
    transform.localScale = _baseScale * (t * twinkle);
  }
}

// Expanding soft magic halo ring
public class MagicHaloAnimator : MonoBehaviour {
  float _targetRadius = 1.6f;
  float _life = 0.65f;
  float _maxLife = 0.65f;
  Vector3 _startPos;

  public void Setup(float targetRadius) {
    _targetRadius = targetRadius;
    _startPos = transform.localPosition;
  }

  void Update() {
    float dt = Time.deltaTime;
    if (dt <= 0f) return;
    _life -= dt;
    if (_life <= 0f) {
      Destroy(gameObject);
      return;
    }

    float progress = 1f - (_life / _maxLife);
    float smooth = Mathf.SmoothStep(0f, 1f, progress);
    float r = Mathf.Lerp(0.2f, _targetRadius, smooth);
    transform.localScale = new Vector3(r, 0.005f, r);
    transform.localPosition = _startPos + new Vector3(0f, smooth * 0.45f, 0f);
  }
}

// Floating fairy bubble: sways laterally, floats up, pops
public class FairyBubbleBit : MonoBehaviour {
  Color _color;
  float _life = 1.4f;
  float _maxLife = 1.4f;
  float _wobbleSpeed;
  float _wobbleOffset;
  Vector3 _startPos;
  float _baseRadius;

  public void Setup(Color color) {
    _color = color;
    _wobbleSpeed = Random.Range(3.0f, 6.0f);
    _wobbleOffset = Random.Range(0f, Mathf.PI * 2f);
    _baseRadius = Random.Range(0.12f, 0.22f);
    _startPos = transform.localPosition;

    Renderer rend = GetComponent<Renderer>();
    if (rend != null) rend.sharedMaterial = MagicJuice.Lit(_color);
    transform.localScale = Vector3.one * _baseRadius;
  }

  void Update() {
    float dt = Time.deltaTime;
    if (dt <= 0f) return;
    _life -= dt;
    if (_life <= 0f) {
      Destroy(gameObject);
      return;
    }

    float progress = 1f - (_life / _maxLife);
    float y = progress * 1.5f;
    float x = Mathf.Sin(progress * _wobbleSpeed + _wobbleOffset) * 0.18f;
    float z = Mathf.Cos(progress * _wobbleSpeed + _wobbleOffset) * 0.18f;

    transform.localPosition = _startPos + new Vector3(x, y, z);
    float scale = _baseRadius * (1f + 0.15f * Mathf.Sin(progress * 12f));
    transform.localScale = Vector3.one * scale;
  }
}

// Rising and expanding steam puff
public class MagicSteamAnimator : MonoBehaviour {
  float _life = 0.9f;
  float _maxLife = 0.9f;
  Vector3 _startPos;
  float _maxScale = 0.45f;

  public void Setup(Color color, float maxScale) {
    _maxScale = maxScale;
    _startPos = transform.localPosition;
    Renderer rend = GetComponent<Renderer>();
    if (rend != null) rend.sharedMaterial = MagicJuice.Lit(color);
    transform.localScale = Vector3.one * 0.08f;
  }

  void Update() {
    float dt = Time.deltaTime;
    if (dt <= 0f) return;
    _life -= dt;
    if (_life <= 0f) {
      Destroy(gameObject);
      return;
    }

    float p = 1f - (_life / _maxLife);
    transform.localPosition = _startPos + new Vector3(0f, p * 1.2f, p * 0.2f);
    float curScale = Mathf.Lerp(0.1f, _maxScale, Mathf.Sqrt(p));
    if (p > 0.7f) {
      float fade = (1f - p) / 0.3f;
      curScale *= fade;
    }
    transform.localScale = Vector3.one * curScale;
  }
}
