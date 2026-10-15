// A_World/GeometryPlay/LivingToyAnimator.cs — Drives procedural living animations
// for the 6 assembled toys: spinning wheels, steam puffs, blinking eyes, waving sails, rocket thrusters.
// Implements IClickTarget for joyful play-testing!
// C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class LivingToyAnimator : MonoBehaviour, IClickTarget {
  public ToyKind Toy;
  public Transform[] Wheels;
  public Transform Smokestack;
  public Transform Headlight;
  public Transform FlameCone;
  public Transform Tail;
  public Transform EarLeft;
  public Transform EarRight;
  public Transform EyeLeft;
  public Transform EyeRight;
  public Transform WaterRipples;
  public Transform Flag;
  public Transform Cherry;

  Vector3 _baseLocalPos;
  Quaternion _baseLocalRot;
  float _time;
  float _steamTimer;
  float _blinkTimer;

  void Start() {
    _baseLocalPos = transform.localPosition;
    _baseLocalRot = transform.localRotation;
  }

  void Update() {
    float dt = Time.deltaTime;
    if (dt <= 0f) return;
    _time += dt;

    if (Toy == ToyKind.Train) {
      // Spinning train wheels
      if (Wheels != null) {
        for (int i = 0; i < Wheels.Length; i++) {
          if (Wheels[i] != null) Wheels[i].Rotate(Vector3.up, 450f * dt, Space.Self);
        }
      }
      // Chugging motion
      float chugY = Mathf.Abs(Mathf.Sin(_time * 9f)) * 0.04f;
      transform.localPosition = _baseLocalPos + new Vector3(0f, chugY, 0f);

      // Steam puffs from smokestack
      _steamTimer += dt;
      if (_steamTimer >= 0.4f && Smokestack != null) {
        _steamTimer = 0f;
        MagicJuice.MagicPuffSteam(Smokestack, Smokestack.position + Vector3.up * 0.35f, Color.white, 0.45f);
      }
    } else if (Toy == ToyKind.Rocket) {
      // Rocket hovering & gentle spin
      float hoverY = 0.55f + Mathf.Sin(_time * 3.2f) * 0.12f;
      transform.localPosition = _baseLocalPos + new Vector3(0f, hoverY, 0f);
      transform.Rotate(Vector3.up, 30f * dt, Space.World);

      // Flickering thruster flame
      if (FlameCone != null) {
        float flicker = 1.0f + Mathf.Sin(_time * 28f) * 0.25f;
        FlameCone.localScale = new Vector3(1f, flicker, 1f);
      }

      _steamTimer += dt;
      if (_steamTimer >= 0.2f && FlameCone != null) {
        _steamTimer = 0f;
        MagicJuice.FairySparkleBurst(FlameCone, FlameCone.position, 4);
      }
    } else if (Toy == ToyKind.Cat) {
      // Ear twitching
      if (EarLeft != null)
        EarLeft.localRotation = Quaternion.Euler(Mathf.Sin(_time * 5f) * 12f, 0f, 0f);
      if (EarRight != null)
        EarRight.localRotation = Quaternion.Euler(Mathf.Cos(_time * 5.5f) * 12f, 0f, 0f);

      // Tail swishing
      if (Tail != null)
        Tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(_time * 5f) * 22f, 0f);

      // Eye blinking
      _blinkTimer += dt;
      if (_blinkTimer >= 2.5f) {
        float blinkT = _blinkTimer - 2.5f;
        float eyeScaleY = (blinkT < 0.12f) ? 0.1f : 1.0f;
        if (EyeLeft != null) EyeLeft.localScale = new Vector3(EyeLeft.localScale.x, eyeScaleY, EyeLeft.localScale.z);
        if (EyeRight != null) EyeRight.localScale = new Vector3(EyeRight.localScale.x, eyeScaleY, EyeRight.localScale.z);
        if (blinkT >= 0.25f) _blinkTimer = 0f;
      }

      // Joyful cat bounce
      float hopY = Mathf.Abs(Mathf.Sin(_time * 3.5f)) * 0.08f;
      transform.localPosition = _baseLocalPos + new Vector3(0f, hopY, 0f);
    } else if (Toy == ToyKind.Sailboat) {
      // Rocking on sea waves
      float rockX = Mathf.Sin(_time * 2.2f) * 4.5f;
      float rockZ = Mathf.Cos(_time * 1.8f) * 5.0f;
      float heaveY = Mathf.Sin(_time * 2.2f) * 0.04f;
      transform.localRotation = _baseLocalRot * Quaternion.Euler(rockX, 0f, rockZ);
      transform.localPosition = _baseLocalPos + new Vector3(0f, heaveY, 0f);

      if (WaterRipples != null) WaterRipples.Rotate(Vector3.up, 20f * dt, Space.Self);
      if (Flag != null) Flag.localRotation = Quaternion.Euler(0f, Mathf.Sin(_time * 6f) * 18f, 0f);
    } else if (Toy == ToyKind.House) {
      // Gentle chimney smoke
      _steamTimer += dt;
      if (_steamTimer >= 0.6f && Smokestack != null) {
        _steamTimer = 0f;
        MagicJuice.MagicPuffSteam(Smokestack, Smokestack.position + Vector3.up * 0.3f, new Color(0.9f, 0.9f, 0.95f), 0.5f);
      }
      float breathe = 1.0f + Mathf.Sin(_time * 2.0f) * 0.015f;
      transform.localScale = Vector3.one * breathe;
    } else if (Toy == ToyKind.IceCream) {
      // Bouncy ice cream wiggle
      float bounce = 1.0f + Mathf.Sin(_time * 4f) * 0.04f;
      transform.localScale = Vector3.one * bounce;
      if (Cherry != null) {
        Cherry.localPosition = new Vector3(0f, 0.95f + Mathf.Abs(Mathf.Sin(_time * 4f)) * 0.05f, 0f);
      }
    }
  }

  public void OnClicked() {
    GameJuice.Pop(transform, 0.25f, 0.4f);
    MagicJuice.FairySparkleBurst(transform, transform.position + Vector3.up * 0.5f, 12);
    // Per-toy joyful reaction (visual only — no invented sound claims).
    if (Toy == ToyKind.Train && Smokestack != null) {
      MagicJuice.MagicPuffSteam(Smokestack, Smokestack.position + Vector3.up * 0.35f, Color.white, 0.5f);
    } else if (Toy == ToyKind.Rocket && FlameCone != null) {
      MagicJuice.FairySparkleBurst(FlameCone, FlameCone.position, 10);
    } else if (Toy == ToyKind.Cat) {
      MagicJuice.FairySparkleBurst(transform, transform.position + Vector3.up * 1.1f, 8);
    } else if (Toy == ToyKind.IceCream && Cherry != null) {
      MagicJuice.FairySparkleBurst(Cherry, Cherry.position, 8);
    }
  }
}
