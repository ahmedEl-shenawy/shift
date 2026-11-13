using System.Collections.Generic;
using UnityEngine;

/// <summary>Procedural low-poly meshes (rounded boxes, torus, gems, spikes). Cached by parameters.</summary>
public static class MeshFactory
{
    static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

    static Mesh Finish(string key, List<Vector3> v, List<Vector3> n, List<int> t, List<Vector2> uv = null, bool recalcNormals = false)
    {
        var m = new Mesh { name = key };
        if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(v);
        m.SetTriangles(t, 0);
        if (uv != null) m.SetUVs(0, uv);
        if (recalcNormals || n == null) m.RecalculateNormals(); else m.SetNormals(n);
        m.RecalculateBounds();
        m.RecalculateTangents();
        cache[key] = m;
        return m;
    }

    static float[] Coords(float half, float r, int seg)
    {
        var list = new List<float>();
        for (int i = 0; i <= seg; i++)
        {
            float th = i / (float)seg * Mathf.PI * 0.5f;
            list.Add(-half + r * (1 - Mathf.Cos(th)));
        }
        for (int i = 0; i <= seg; i++)
        {
            float th = i / (float)seg * Mathf.PI * 0.5f;
            list.Add(half - r + r * Mathf.Sin(th));
        }
        return list.ToArray();
    }

    /// <summary>Box with rounded edges and corners, centred on the origin.</summary>
    public static Mesh RoundedBox(Vector3 size, float radius, int seg = 3)
    {
        string key = $"rb_{size.x:F3}_{size.y:F3}_{size.z:F3}_{radius:F3}_{seg}";
        if (cache.TryGetValue(key, out var cached)) return cached;

        Vector3 h = size * 0.5f;
        radius = Mathf.Min(radius, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.999f);
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        Vector3[] faces = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

        foreach (var n in faces)
        {
            Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
            Vector3 v = Vector3.Cross(n, u);
            float hu = Mathf.Abs(Vector3.Dot(h, u));
            float hv = Mathf.Abs(Vector3.Dot(h, new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z))));
            float hn = Mathf.Abs(Vector3.Dot(h, new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z))));
            float[] cu = Coords(hu, radius, seg);
            float[] cv = Coords(hv, radius, seg);
            int start = verts.Count;
            int w = cu.Length;
            for (int j = 0; j < cv.Length; j++)
            for (int i = 0; i < w; i++)
            {
                Vector3 p = n * hn + u * cu[i] + v * cv[j];
                Vector3 inner = new Vector3(
                    Mathf.Clamp(p.x, -h.x + radius, h.x - radius),
                    Mathf.Clamp(p.y, -h.y + radius, h.y - radius),
                    Mathf.Clamp(p.z, -h.z + radius, h.z - radius));
                Vector3 d = p - inner;
                Vector3 nn = d.sqrMagnitude > 1e-10f ? d.normalized : n;
                verts.Add(inner + nn * radius);
                norms.Add(nn);
                uvs.Add(new Vector2((cu[i] / hu + 1) * 0.5f, (cv[j] / hv + 1) * 0.5f));
            }
            for (int j = 0; j < cv.Length - 1; j++)
            for (int i = 0; i < w - 1; i++)
            {
                int a = start + j * w + i, b = a + 1, c = a + w, d = c + 1;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }
        }
        return Finish(key, verts, norms, tris, uvs);
    }

    /// <summary>Torus lying flat in the XZ plane.</summary>
    public static Mesh Torus(float R, float r, int segU = 40, int segV = 12)
    {
        string key = $"torus_{R:F3}_{r:F3}_{segU}_{segV}";
        if (cache.TryGetValue(key, out var cached)) return cached;
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var tris = new List<int>();
        for (int i = 0; i <= segU; i++)
        {
            float u = i / (float)segU * Mathf.PI * 2;
            for (int j = 0; j <= segV; j++)
            {
                float v = j / (float)segV * Mathf.PI * 2;
                Vector3 c = new Vector3(Mathf.Cos(u) * R, 0, Mathf.Sin(u) * R);
                Vector3 n = new Vector3(Mathf.Cos(u) * Mathf.Cos(v), Mathf.Sin(v), Mathf.Sin(u) * Mathf.Cos(v));
                verts.Add(c + n * r);
                norms.Add(n);
            }
        }
        int row = segV + 1;
        for (int i = 0; i < segU; i++)
        for (int j = 0; j < segV; j++)
        {
            int a = i * row + j, b = (i + 1) * row + j, c = a + 1, d = b + 1;
            AddTriFacing(tris, verts, a, b, c, norms[a]);
            AddTriFacing(tris, verts, b, d, c, norms[b]);
        }
        return Finish(key, verts, norms, tris);
    }

    static void AddTriFacing(List<int> tris, List<Vector3> v, int a, int b, int c, Vector3 n)
    {
        Vector3 cr = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (Vector3.Dot(cr, n) >= 0) { tris.Add(a); tris.Add(b); tris.Add(c); }
        else { tris.Add(a); tris.Add(c); tris.Add(b); }
    }

    /// <summary>Flat-shaded convex mesh from a list of triangles (outward fixed via reference point).</summary>
    static Mesh FlatConvex(string key, Vector3[][] faces, Vector3 centre)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        foreach (var f in faces)
        {
            Vector3 a = f[0], b = f[1], c = f[2];
            Vector3 cr = Vector3.Cross(b - a, c - a);
            Vector3 mid = (a + b + c) / 3f - centre;
            if (Vector3.Dot(cr, mid) < 0) { var tmp = b; b = c; c = tmp; }
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }
        return Finish(key, verts, null, tris, null, true);
    }

    public static Mesh Octahedron(float rx, float ry)
    {
        string key = $"oct_{rx:F3}_{ry:F3}";
        if (cache.TryGetValue(key, out var cached)) return cached;
        Vector3 U = Vector3.up * ry, D = Vector3.down * ry;
        Vector3[] ring = { Vector3.right * rx, Vector3.forward * rx, Vector3.left * rx, Vector3.back * rx };
        var faces = new List<Vector3[]>();
        for (int i = 0; i < 4; i++)
        {
            var a = ring[i]; var b = ring[(i + 1) % 4];
            faces.Add(new[] { U, a, b });
            faces.Add(new[] { D, a, b });
        }
        return FlatConvex(key, faces.ToArray(), Vector3.zero);
    }

    /// <summary>Square pyramid with base on y=0.</summary>
    public static Mesh Pyramid(float baseSize, float height)
    {
        string key = $"pyr_{baseSize:F3}_{height:F3}";
        if (cache.TryGetValue(key, out var cached)) return cached;
        float h = baseSize * 0.5f;
        Vector3 apex = new Vector3(0, height, 0);
        Vector3 a = new Vector3(-h, 0, -h), b = new Vector3(h, 0, -h), c = new Vector3(h, 0, h), d = new Vector3(-h, 0, h);
        var faces = new[]
        {
            new[] { apex, a, b }, new[] { apex, b, c }, new[] { apex, c, d }, new[] { apex, d, a },
            new[] { a, b, c }, new[] { a, c, d }
        };
        return FlatConvex(key, faces, new Vector3(0, height * 0.3f, 0));
    }

    /// <summary>Low-poly UV sphere (smooth shaded).</summary>
    public static Mesh Sphere(float r, int seg = 16)
    {
        string key = $"sph_{r:F3}_{seg}";
        if (cache.TryGetValue(key, out var cached)) return cached;
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var tris = new List<int>();
        int rings = seg / 2;
        for (int y = 0; y <= rings; y++)
        {
            float phi = y / (float)rings * Mathf.PI;
            for (int x = 0; x <= seg; x++)
            {
                float th = x / (float)seg * Mathf.PI * 2;
                Vector3 n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                verts.Add(n * r);
                norms.Add(n);
            }
        }
        int row = seg + 1;
        for (int y = 0; y < rings; y++)
        for (int x = 0; x < seg; x++)
        {
            int a = y * row + x, b = a + 1, c = a + row, d = c + 1;
            AddTriFacing(tris, verts, a, b, c, (norms[a] + norms[b] + norms[c]).normalized);
            AddTriFacing(tris, verts, b, d, c, (norms[b] + norms[d] + norms[c]).normalized);
        }
        return Finish(key, verts, norms, tris);
    }

    /// <summary>Creates a child GameObject with a mesh + material (no collider).</summary>
    public static GameObject Part(string name, Transform parent, Mesh mesh, Material mat, Vector3 localPos, bool shadows = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }
}
