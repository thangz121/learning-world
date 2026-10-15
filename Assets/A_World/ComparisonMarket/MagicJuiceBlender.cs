// A_World/ComparisonMarket/MagicJuiceBlender.cs — The Rainbow Juice Blender reward machine.
// Crushes won fruits into sparkling colorful smoothies with celebratory animations.
// C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class MagicJuiceBlender : MonoBehaviour {
  public Transform BlenderJar;
  public Transform LiquidInJar;
  public Transform CupRoot;
  public Transform CupLiquid;
  public Transform Straw;

  float _blendT;
  float _liquidLevel;
  Color _currentColor = new Color(0.95f, 0.45f, 0.25f);
  Material _liquidMat;
  Material _cupLiquidMat;

  void Update() {
    // NOTE: driven by ComparisonMarket.Tick (single source of truth) — an internal Update()
    // would double-rate the blending animation during play. Keep Tick() public for tests.
  }

  public void Tick(float dt) {
    if (dt <= 0f) return;
    if (_blendT > 0f) {
      _blendT -= dt;
      if (BlenderJar != null) {
        float shake = Mathf.Sin(Time.time * 40f) * 0.02f * (_blendT / 1.5f);
        BlenderJar.localPosition = new Vector3(shake, 0.42f, 0f);
      }
      _liquidLevel = Mathf.Min(1f, _liquidLevel + dt * 0.8f);
      UpdateLiquidVisuals();
    }
  }

  void UpdateLiquidVisuals() {
    if (LiquidInJar != null) {
      LiquidInJar.localScale = new Vector3(0.36f, Mathf.Max(0.01f, 0.42f * _liquidLevel), 0.36f);
      LiquidInJar.localPosition = new Vector3(0f, 0.18f + 0.21f * _liquidLevel, 0f);
    }
    if (CupLiquid != null && _liquidLevel > 0.4f) {
      float cupFill = (_liquidLevel - 0.4f) / 0.6f;
      CupLiquid.localScale = new Vector3(0.24f, Mathf.Max(0.01f, 0.28f * cupFill), 0.24f);
      CupLiquid.localPosition = new Vector3(0f, 0.14f * cupFill, 0f);
    }
  }

  public void TriggerJuice(Color fruitColor, Vector3 fruitSourcePos) {
    _currentColor = fruitColor;
    _blendT = 1.6f;
    _liquidLevel = 0.1f;
    if (_liquidMat != null) _liquidMat.SetColor("_BaseColor", _currentColor);
    if (_cupLiquidMat != null) _cupLiquidMat.SetColor("_BaseColor", _currentColor);

    // Parabol arc jumping fruit
    SpawnJumpingFruit(fruitSourcePos, transform.position + new Vector3(0f, 1.1f, 0f), fruitColor);

    if (CupRoot != null) {
      GameJuice.Pop(CupRoot, 0.2f, 0.4f);
    }
  }

  void SpawnJumpingFruit(Vector3 fromWorld, Vector3 toWorld, Color color) {
    GameObject f = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    f.name = "FlyingFruit";
    f.transform.position = fromWorld;
    f.transform.localScale = Vector3.one * 0.26f;
    Renderer r = f.GetComponent<Renderer>();
    if (r != null) {
      Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
      m.SetColor("_BaseColor", color);
      r.sharedMaterial = m;
    }
    Collider c = f.GetComponent<Collider>();
    if (c != null) CharacterPresentation.DestroyNow(c);

    ArcMotion arc = f.AddComponent<ArcMotion>();
    arc.Init(fromWorld, toWorld, 0.65f, () => {
      CharacterPresentation.DestroyNow(f);
      if (BlenderJar != null) GameJuice.Wobble(BlenderJar, 12f, 0.4f);
    });
  }

  public static MagicJuiceBlender Build(Transform parent, Vector3 localPos) {
    GameObject root = new GameObject("CMMagicBlender");
    root.transform.SetParent(parent, false);
    root.transform.localPosition = localPos;

    MagicJuiceBlender b = root.AddComponent<MagicJuiceBlender>();

    // Blender Machine Base (Đế máy ép kim loại)
    GameObject machineBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    machineBase.name = "BlenderMachineBase";
    machineBase.transform.SetParent(root.transform, false);
    machineBase.transform.localPosition = new Vector3(0f, 0.15f, 0f);
    machineBase.transform.localScale = new Vector3(0.5f, 0.15f, 0.5f);
    SetLitMat(machineBase, new Color(0.85f, 0.88f, 0.92f), 0.5f);
    Strip(machineBase);

    // Glass Jar (Cối xay thủy tinh trong suốt)
    GameObject jar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    jar.name = "Jar";
    jar.transform.SetParent(root.transform, false);
    jar.transform.localPosition = new Vector3(0f, 0.42f, 0f);
    jar.transform.localScale = new Vector3(0.42f, 0.26f, 0.42f);
    SetLitMat(jar, new Color(0.9f, 0.95f, 1f, 0.45f), 0.8f);
    Strip(jar);
    b.BlenderJar = jar.transform;

    // Jar Cap (Nắp cối xay)
    GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    cap.name = "Cap";
    cap.transform.SetParent(jar.transform, false);
    cap.transform.localPosition = new Vector3(0f, 1.05f, 0f);
    cap.transform.localScale = new Vector3(1.15f, 0.12f, 1.15f);
    SetLitMat(cap, new Color(0.95f, 0.35f, 0.35f), 0.3f);
    Strip(cap);

    // Liquid in Jar (Nước sinh tố dâng lên trong cối)
    GameObject liquid = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    liquid.name = "Liquid";
    liquid.transform.SetParent(jar.transform, false);
    liquid.transform.localPosition = new Vector3(0f, 0.2f, 0f);
    liquid.transform.localScale = new Vector3(0.36f, 0.05f, 0.36f);
    b._liquidMat = SetLitMat(liquid, new Color(0.95f, 0.5f, 0.2f), 0.2f);
    Strip(liquid);
    b.LiquidInJar = liquid.transform;

    // Smoothie Glass Cup (Ly sinh tố bên cạnh)
    GameObject cup = new GameObject("SmoothieCup");
    cup.transform.SetParent(root.transform, false);
    cup.transform.localPosition = new Vector3(0.55f, 0f, 0f);
    b.CupRoot = cup.transform;

    GameObject cupGlass = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    cupGlass.name = "Glass";
    cupGlass.transform.SetParent(cup.transform, false);
    cupGlass.transform.localPosition = new Vector3(0f, 0.2f, 0f);
    cupGlass.transform.localScale = new Vector3(0.3f, 0.2f, 0.3f);
    SetLitMat(cupGlass, new Color(0.92f, 0.96f, 1f, 0.4f), 0.7f);
    Strip(cupGlass);

    // Cup Liquid
    GameObject cupLiq = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    cupLiq.name = "CupLiquid";
    cupLiq.transform.SetParent(cup.transform, false);
    cupLiq.transform.localPosition = new Vector3(0f, 0.15f, 0f);
    cupLiq.transform.localScale = new Vector3(0.24f, 0.14f, 0.24f);
    b._cupLiquidMat = SetLitMat(cupLiq, new Color(0.95f, 0.5f, 0.2f), 0.2f);
    Strip(cupLiq);
    b.CupLiquid = cupLiq.transform;

    // Twisted Straw (Ống hút xoắn)
    GameObject straw = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    straw.name = "Straw";
    straw.transform.SetParent(cup.transform, false);
    straw.transform.localPosition = new Vector3(0.06f, 0.35f, 0f);
    straw.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
    straw.transform.localScale = new Vector3(0.035f, 0.24f, 0.035f);
    SetLitMat(straw, new Color(0.98f, 0.95f, 0.25f), 0.2f);
    Strip(straw);
    b.Straw = straw.transform;

    return b;
  }

  static void Strip(GameObject go) {
    try {
      Collider c = go.GetComponent<Collider>();
      if (c != null) CharacterPresentation.DestroyNow(c);
    } catch (System.Exception) { }
  }

  static Material SetLitMat(GameObject go, Color color, float smoothness = 0.2f) {
    Renderer r = go.GetComponent<Renderer>();
    if (r == null) return null;
    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.SetColor("_BaseColor", color);
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
    r.sharedMaterial = mat;
    return mat;
  }

  sealed class ArcMotion : MonoBehaviour {
    Vector3 _start;
    Vector3 _end;
    float _dur;
    float _t;
    bool _done;
    System.Action _onDone;

    public void Init(Vector3 start, Vector3 end, float dur, System.Action onDone) {
      _start = start;
      _end = end;
      _dur = Mathf.Max(0.1f, dur);
      _onDone = onDone;
    }

    void Update() {
      _t += Time.deltaTime;
      float p = Mathf.Clamp01(_t / _dur);
      Vector3 curr = Vector3.Lerp(_start, _end, p);
      curr.y += Mathf.Sin(p * Mathf.PI) * 1.5f;
      transform.position = curr;
      if (p >= 1f && !_done) {
        _done = true;
        _onDone?.Invoke();
      }
    }
  }
}
