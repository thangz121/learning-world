// A_World/BuildYard/BuildYardLandmark.cs — S3-P2Z14 GAMEPLAY #4.
// The hub-side progress landmark beside the build_yard gate: a mini block
// tower whose height reflects the last COMPLETED target (brief §25 — the gate
// reflects completed progression; never a redesign of the reviewed hub gate).
// Pure presentation: collider-free, bake-ignored, deterministic. Before any
// completion it shows a 3-block preview so the gate still reads "tower".
// C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BuildYardLandmark : MonoBehaviour {
  public const int MaxBlocks = BuildTowerBuilder.MaxTarget;
  public const int PreviewHeight = 3;
  const float BlockSize = 0.34f;

  static readonly Color Tan = new Color(0.78f, 0.66f, 0.46f);
  static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color Coral = new Color(0.90f, 0.52f, 0.38f);

  readonly List<GameObject> _blocks = new List<GameObject>();
  public int ShownHeight { get; private set; } = -1;

  // Called once by MathWorldBuilder after the gate is posed. The landmark
  // stands BESIDE the arch (local +x, slightly behind the beam) so the label
  // and the walk-in portal stay clear.
  public void Build(Transform gateRoot, Vector3 localPos) {
    transform.SetParent(gateRoot, false);
    transform.localPosition = localPos;
    GameObject basePad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    basePad.name = "MathBuildYardLandmarkBase";
    basePad.transform.SetParent(transform, false);
    basePad.transform.localPosition = new Vector3(0f, 0.03f, 0f);
    basePad.transform.localScale = new Vector3(1.0f, 0.03f, 1.0f);
    basePad.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.86f, 0.78f, 0.62f));
    Strip(basePad);
    Ignore(basePad);
    Color[] palette = { Tan, Gold, Coral };
    for (int i = 0; i < MaxBlocks; i++) {
      GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
      block.name = "MathBuildYardTower" + i;
      block.transform.SetParent(transform, false);
      block.transform.localPosition = new Vector3(0f, 0.06f + BlockSize * 0.5f + i * BlockSize, 0f);
      block.transform.localScale = Vector3.one * BlockSize;
      block.GetComponent<Renderer>().sharedMaterial = Lit(palette[i % palette.Length]);
      Strip(block);
      Ignore(block);
      block.SetActive(false);
      _blocks.Add(block);
    }
    ShowHeight(PreviewHeight);
  }

  // 0..MaxBlocks: the completed height (or the 3-block preview before any).
  public void ShowHeight(int n) {
    if (n < 0) n = PreviewHeight;
    if (n > MaxBlocks) n = MaxBlocks;
    if (n == ShownHeight) return;
    ShownHeight = n;
    for (int i = 0; i < _blocks.Count; i++) {
      GameObject block = _blocks[i];
      if (block != null) block.SetActive(i < n);
    }
    try { Debug.Log("[BuildYardLandmark] showing " + n + " blocks.", this); } catch (Exception) { }
  }

  static void Strip(GameObject go) {
    try {
      Collider c = go.GetComponent<Collider>();
      if (c != null) CharacterPresentation.DestroyNow(c);
    } catch (Exception) { }
  }

  static void Ignore(GameObject go) {
    if (go == null) return;
    try {
      Unity.AI.Navigation.NavMeshModifier mod = go.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
      mod.ignoreFromBuild = true;
    } catch (Exception) { }
  }

  static Material Lit(Color color) {
    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.SetColor("_BaseColor", color);
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
    mat.enableInstancing = true;
    return mat;
  }
}
