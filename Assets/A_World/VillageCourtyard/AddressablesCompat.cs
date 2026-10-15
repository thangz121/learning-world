// A_World/VillageCourtyard/AddressablesCompat.cs — Local asset-reference stand-ins.
// HONEST NOTE (1.9.48): these are NOT the Addressables package and they do NOT load anything
// at runtime — they are serializable editor-reference holders for SpeakingPromptSO until a real
// asset-loading pipeline lands. The previous file declared these types inside the
// UnityEngine.AddressableAssets namespace, which would collide the moment the real package is
// added (compile landmine) and wrongly read as "chuẩn Addressables". Renamed to local types.
// C# 9.0 only. Pure UnityEngine.
using System;
using UnityEngine;

namespace LweAssets {
  [Serializable]
  public class SpriteAssetRef {
    [SerializeField] Sprite _editorAsset;
    [SerializeField] string _assetGUID = "";

    public Sprite EditorAsset => _editorAsset;
    public string AssetGUID => _assetGUID;
    public bool IsValid => _editorAsset != null || !string.IsNullOrEmpty(_assetGUID);

    public SpriteAssetRef() { }
    public SpriteAssetRef(Sprite sprite, string guid = "") {
      _editorAsset = sprite;
      _assetGUID = guid;
    }

    public void ReleaseAsset() {
      _editorAsset = null; // drops the cached reference only (owner keeps the asset)
    }
  }

  [Serializable]
  public class AssetRef<T> where T : UnityEngine.Object {
    [SerializeField] T _editorAsset;
    [SerializeField] string _assetGUID = "";

    public T EditorAsset => _editorAsset;
    public string AssetGUID => _assetGUID;
    public bool IsValid => _editorAsset != null || !string.IsNullOrEmpty(_assetGUID);

    public AssetRef() { }
    public AssetRef(T asset, string guid = "") {
      _editorAsset = asset;
      _assetGUID = guid;
    }

    public void ReleaseAsset() {
      _editorAsset = null;
    }
  }
}
