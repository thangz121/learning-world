// A_World/LwGrass.cs — lightweight LW grass-clump system (brief §6 fallback).
// The evaluated external grass meshes carry no usable normals (render black
// under URP/Lit regardless of importer mode), so tufts are generated here:
// 3 deterministic star-clump variants (6 tapered blades each) with explicit
// up-biased normals, shared matte double-sided materials, GPU instancing.
// Same palette/scale language as the Quaternius set — no style soup.
using System.Collections.Generic;
using UnityEngine;

public static class LwGrass {
  static readonly List<Mesh> Variants = new List<Mesh>();
  static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

  public static GameObject SpawnTuft(Transform parent, Vector3 pos, float yawDeg, float height, Color leaf) {
    Mesh mesh = Variant((int)(Mathf.Abs(pos.x * 13.7f + pos.z * 7.3f)) % 3);
    if (mesh == null) return null;
    GameObject go = new GameObject("GrassTuft");
    go.transform.SetParent(parent);
    go.transform.position = pos;
    go.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
    go.transform.localScale = Vector3.one * (height / 0.30f);
    MeshFilter filter = go.AddComponent<MeshFilter>();
    filter.sharedMesh = mesh;
    MeshRenderer renderer = go.AddComponent<MeshRenderer>();
    renderer.sharedMaterial = LeafMat(leaf);
    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    renderer.receiveShadows = false;
    go.AddComponent<NatureSway>().amplitudeDeg = 1.4f;
    return go;
  }

  static Mesh Variant(int index) {
    while (Variants.Count <= index) Variants.Add(BuildVariant(Variants.Count, 20260918 + Variants.Count * 101));
    return Variants[index];
  }

  // One clump: 6 tapered quads in a star, tilted outward, up-biased normals.
  static Mesh BuildVariant(int index, int seed) {
    var rng = new System.Random(seed + index * 57);
    var verts = new List<Vector3>();
    var normals = new List<Vector3>();
    var tris = new List<int>();
    for (int b = 0; b < 6; b++) {
      float ang = b * 60f * Mathf.Deg2Rad + (float)(rng.NextDouble() - 0.5) * 0.5f;
      float tilt = 0.16f + (float)rng.NextDouble() * 0.22f;
      float h = 0.22f + (float)rng.NextDouble() * 0.12f;
      float w = 0.030f + (float)rng.NextDouble() * 0.014f;
      Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
      Vector3 side = new Vector3(-dir.z, 0f, dir.x);
      Vector3 baseC = dir * 0.02f;
      Vector3 tipC = baseC + dir * tilt * h + Vector3.up * h;
      Vector3 n = (Vector3.up * 0.8f + dir * 0.35f).normalized;
      int v0 = verts.Count;
      verts.Add(baseC - side * w * 0.5f); normals.Add(n);
      verts.Add(baseC + side * w * 0.5f); normals.Add(n);
      verts.Add(tipC - side * w * 0.12f); normals.Add(n);
      verts.Add(tipC + side * w * 0.12f); normals.Add(n);
      tris.Add(v0); tris.Add(v0 + 2); tris.Add(v0 + 1);
      tris.Add(v0 + 1); tris.Add(v0 + 2); tris.Add(v0 + 3);
    }
    Mesh mesh = new Mesh();
    mesh.name = "LwGrassClump" + index;
    mesh.SetVertices(verts);
    mesh.SetNormals(normals);
    mesh.SetTriangles(tris, 0);
    mesh.RecalculateBounds();
    mesh.UploadMeshData(true);
    return mesh;
  }

  static Material LeafMat(Color leaf) {
    string key = "lw" + leaf.r.ToString("F2") + leaf.g.ToString("F2") + leaf.b.ToString("F2");
    if (Mats.TryGetValue(key, out Material hit) && hit != null) return hit;
    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.SetColor("_BaseColor", leaf);
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
    mat.SetInt("_Cull", 0); // thin blades: lit both sides
    mat.enableInstancing = true;
    Mats[key] = mat;
    return mat;
  }
}
