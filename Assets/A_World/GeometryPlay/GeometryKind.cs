// A_World/GeometryPlay/GeometryKind.cs — LẮP HÌNH VUI NHỘN (shape_builder).
// Four shapes only. Color is never identity. C# 9.0 only.
using UnityEngine;

public enum GeometryKind {
  Circle = 0,
  Square = 1,
  Triangle = 2,
  Rectangle = 3,
}

public static class GeometryShapes {
  public static readonly GeometryKind[] All = {
    GeometryKind.Circle, GeometryKind.Square, GeometryKind.Triangle, GeometryKind.Rectangle
  };

  public static readonly Color[] Palette = {
    new Color(0.92f, 0.32f, 0.34f),
    new Color(0.28f, 0.52f, 0.92f),
    new Color(0.98f, 0.82f, 0.22f),
    new Color(0.28f, 0.72f, 0.38f),
  };

  public static string NameEn(GeometryKind k) {
    if (k == GeometryKind.Circle) return "circle";
    if (k == GeometryKind.Square) return "square";
    if (k == GeometryKind.Triangle) return "triangle";
    return "rectangle";
  }

  public static string NameVi(GeometryKind k) {
    if (k == GeometryKind.Circle) return "tròn";
    if (k == GeometryKind.Square) return "vuông";
    if (k == GeometryKind.Triangle) return "tam giác";
    return "chữ nhật";
  }

  public static int Corners(GeometryKind k) {
    if (k == GeometryKind.Circle) return 0;
    if (k == GeometryKind.Triangle) return 3;
    return 4;
  }

  public static bool EqualSides(GeometryKind k) { return k == GeometryKind.Square; }
  public static bool LongAndShort(GeometryKind k) { return k == GeometryKind.Rectangle; }
  public static bool Round(GeometryKind k) { return k == GeometryKind.Circle; }

  public static GeometryKind FromProperty(string id) {
    if (id == "corners3") return GeometryKind.Triangle;
    if (id == "nocorners") return GeometryKind.Circle;
    if (id == "equal4") return GeometryKind.Square;
    if (id == "longshort") return GeometryKind.Rectangle;
    return GeometryKind.Circle;
  }

  public static bool OrientationOk(GeometryKind k, float pieceYaw, float socketYaw) {
    if (k == GeometryKind.Circle) return true;
    return Mathf.Abs(Mathf.DeltaAngle(pieceYaw, socketYaw)) <= 40f;
  }

  public static Color ColorAt(int i) {
    return Palette[Mathf.Abs(i) % Palette.Length];
  }
}
