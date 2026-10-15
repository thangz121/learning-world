// A_World/ClassificationCity/CityExpressTrain.cs — The Magical City Express Train.
// Little toy locomotive that chugs around the sorting plaza upon completed rounds.
// C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class CityExpressTrain : MonoBehaviour {
  public Transform EngineRoot;
  public Transform Funnel;
  public Transform CarRoot;

  float _runT;
  float _angle;
  const float Radius = 4.2f;
  const float Speed = 2.4f;

  void Update() {
    // NOTE: driven by ClassificationCity.Tick (single source of truth) — an internal Update()
    // would double-rate the train run during play. Keep Tick() public for tests.
  }

  public void Tick(float dt) {
    if (dt <= 0f) return;
    if (_runT > 0f) {
      _runT -= dt;
      _angle += Speed * dt;
      float x = Mathf.Cos(_angle) * Radius;
      float z = 2.6f + Mathf.Sin(_angle) * Radius * 0.7f;
      transform.localPosition = new Vector3(x, 0.08f, z);

      // Facing tangential to oval
      float nextX = Mathf.Cos(_angle + 0.05f) * Radius;
      float nextZ = 2.6f + Mathf.Sin(_angle + 0.05f) * Radius * 0.7f;
      Vector3 fwd = new Vector3(nextX - x, 0f, nextZ - z);
      if (fwd.sqrMagnitude > 0.0001f) {
        transform.localRotation = Quaternion.LookRotation(fwd);
      }

      // Funnel puff bobbing
      if (Funnel != null) {
        float bob = Mathf.Sin(Time.time * 25f) * 0.04f;
        Funnel.localPosition = new Vector3(0f, 0.65f + bob, 0.35f);
      }
    }
  }

  public void TriggerTrainRun() {
    _runT = 3.5f;
    if (EngineRoot != null) GameJuice.Pop(EngineRoot, 0.2f, 0.4f);
  }

  public static CityExpressTrain Build(Transform parent, Vector3 localPos) {
    GameObject root = new GameObject("CCExpressTrain");
    root.transform.SetParent(parent, false);
    root.transform.localPosition = localPos;

    CityExpressTrain train = root.AddComponent<CityExpressTrain>();

    // 1. Locomotive Engine Body (Đầu tàu hỏa đỏ rực)
    GameObject engine = new GameObject("Engine");
    engine.transform.SetParent(root.transform, false);
    train.EngineRoot = engine.transform;

    GameObject cab = GameObject.CreatePrimitive(PrimitiveType.Cube);
    cab.name = "Cab";
    cab.transform.SetParent(engine.transform, false);
    cab.transform.localPosition = new Vector3(0f, 0.45f, -0.2f);
    cab.transform.localScale = new Vector3(0.5f, 0.55f, 0.45f);
    SetLitMat(cab, new Color(0.92f, 0.32f, 0.34f));
    Strip(cab);

    GameObject boiler = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    boiler.name = "Boiler";
    boiler.transform.SetParent(engine.transform, false);
    boiler.transform.localPosition = new Vector3(0f, 0.35f, 0.2f);
    boiler.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    boiler.transform.localScale = new Vector3(0.42f, 0.32f, 0.42f);
    SetLitMat(boiler, new Color(0.28f, 0.52f, 0.92f));
    Strip(boiler);

    // Smokestack Funnel (Ống khói bốc hơi)
    GameObject funnel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    funnel.name = "Funnel";
    funnel.transform.SetParent(engine.transform, false);
    funnel.transform.localPosition = new Vector3(0f, 0.65f, 0.35f);
    funnel.transform.localScale = new Vector3(0.12f, 0.18f, 0.12f);
    SetLitMat(funnel, new Color(0.98f, 0.82f, 0.22f));
    Strip(funnel);
    train.Funnel = funnel.transform;

    // Cowcatcher (Mũi gạt tàu)
    GameObject cow = GameObject.CreatePrimitive(PrimitiveType.Cube);
    cow.name = "Cowcatcher";
    cow.transform.SetParent(engine.transform, false);
    cow.transform.localPosition = new Vector3(0f, 0.18f, 0.46f);
    cow.transform.localScale = new Vector3(0.52f, 0.15f, 0.12f);
    SetLitMat(cow, new Color(0.98f, 0.82f, 0.22f));
    Strip(cow);

    // Wheels (4 bánh xe đỏ)
    for (int i = 0; i < 4; i++) {
      float x = (i % 2 == 0 ? -0.28f : 0.28f);
      float z = (i < 2 ? -0.25f : 0.25f);
      GameObject w = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      w.name = "TrainWheel" + i;
      w.transform.SetParent(engine.transform, false);
      w.transform.localPosition = new Vector3(x, 0.16f, z);
      w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
      w.transform.localScale = new Vector3(0.26f, 0.06f, 0.26f);
      SetLitMat(w, new Color(0.2f, 0.2f, 0.25f));
      Strip(w);
    }

    // 2. Trailing Freight Car (Toa chở hàng vàng)
    GameObject car = new GameObject("Car");
    car.transform.SetParent(root.transform, false);
    car.transform.localPosition = new Vector3(0f, 0f, -0.9f);
    train.CarRoot = car.transform;

    GameObject bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
    bed.name = "CarBed";
    bed.transform.SetParent(car.transform, false);
    bed.transform.localPosition = new Vector3(0f, 0.32f, 0f);
    bed.transform.localScale = new Vector3(0.52f, 0.35f, 0.65f);
    SetLitMat(bed, new Color(0.98f, 0.82f, 0.22f));
    Strip(bed);

    // Car Wheels
    for (int i = 0; i < 4; i++) {
      float x = (i % 2 == 0 ? -0.28f : 0.28f);
      float z = (i < 2 ? -0.2f : 0.2f);
      GameObject cw = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      cw.name = "CarWheel" + i;
      cw.transform.SetParent(car.transform, false);
      cw.transform.localPosition = new Vector3(x, 0.16f, z);
      cw.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
      cw.transform.localScale = new Vector3(0.24f, 0.05f, 0.24f);
      SetLitMat(cw, new Color(0.2f, 0.2f, 0.25f));
      Strip(cw);
    }

    return train;
  }

  static void Strip(GameObject go) {
    try {
      Collider c = go.GetComponent<Collider>();
      if (c != null) CharacterPresentation.DestroyNow(c);
    } catch (System.Exception) { }
  }

  static void SetLitMat(GameObject go, Color color) {
    Renderer r = go.GetComponent<Renderer>();
    if (r == null) return;
    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.SetColor("_BaseColor", color);
    mat.SetFloat("_Smoothness", 0.3f);
    r.sharedMaterial = mat;
  }
}
