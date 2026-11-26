using System;
using UnityEngine;

namespace PoliceChase.UI
{
    /// <summary>
    /// Hình vẽ bằng toán (không cần ảnh): icon một màu, ảnh màu (logo đồng hồ tốc độ, đồng xu)
    /// và hàm khoảng cách cho khung bo góc / vát góc. Toạ độ 0..1, gốc ở dưới-trái.
    /// </summary>
    public static class UIShapes
    {
        // ───────────────────────── Icon một màu ─────────────────────────

        public static Func<float, float, bool> Get(string name)
        {
            switch (name)
            {
                case "chevron": return (x, y) => InPoly(x, y, 0.32f, 0.1f, 0.52f, 0.1f, 0.8f, 0.5f, 0.52f, 0.9f, 0.32f, 0.9f, 0.6f, 0.5f);
                case "chevronLeft": return (x, y) => InPoly(1f - x, y, 0.32f, 0.1f, 0.52f, 0.1f, 0.8f, 0.5f, 0.52f, 0.9f, 0.32f, 0.9f, 0.6f, 0.5f);

                case "info":
                    return (x, y) =>
                    {
                        float d = Dist(x, y, 0.5f, 0.5f);
                        bool dot = Dist(x, y, 0.5f, 0.73f) < 0.075f;
                        bool stem = x > 0.435f && x < 0.565f && y > 0.2f && y < 0.6f;
                        return d < 0.47f && !dot && !stem;
                    };

                case "checkCircle":
                    return (x, y) => Dist(x, y, 0.5f, 0.5f) < 0.47f &&
                                     !InPoly(x, y, 0.22f, 0.5f, 0.32f, 0.6f, 0.44f, 0.48f, 0.7f, 0.74f, 0.8f, 0.64f, 0.44f, 0.28f);

                case "gear":
                    return (x, y) =>
                    {
                        float d = Dist(x, y, 0.5f, 0.5f);
                        float a = Mathf.Atan2(y - 0.5f, x - 0.5f);
                        float tooth = Mathf.Cos(a * 8f) > 0.25f ? 0.47f : 0.37f;
                        return d < tooth && d > 0.15f;
                    };

                case "wrench":
                    return (x, y) =>
                    {
                        // Xoay 45°: u dọc theo cán, v ngang cán.
                        float cx = x - 0.5f, cy = y - 0.5f;
                        float u = (cx + cy) * 0.7071f, v = (cy - cx) * 0.7071f;
                        bool handle = u > -0.42f && u < 0.18f && Mathf.Abs(v) < 0.075f;
                        bool tail = Dist(u, v, -0.42f, 0f) < 0.075f;
                        float hd = Dist(u, v, 0.27f, 0f);
                        bool head = hd < 0.19f;
                        bool jaw = u > 0.27f && Mathf.Abs(v) < 0.075f;   // khe miệng cờ lê
                        bool hole = Dist(u, v, -0.36f, 0f) < 0.03f;
                        return (handle || tail || head) && !jaw && !hole;
                    };

                case "flag":
                    return (x, y) =>
                    {
                        bool pole = x > 0.1f && x < 0.17f && y > 0.04f && y < 0.96f;
                        float wave = Mathf.Sin((x - 0.17f) * 9f) * 0.04f;
                        float fy = y - wave;
                        bool inFlag = x > 0.17f && x < 0.92f && fy > 0.46f && fy < 0.92f;
                        if (!inFlag) return pole;
                        bool edge = x > 0.89f || fy < 0.49f || fy > 0.89f;
                        int cxI = Mathf.FloorToInt((x - 0.17f) / 0.1875f);
                        int cyI = Mathf.FloorToInt((fy - 0.46f) / 0.1467f);
                        return edge || ((cxI + cyI) & 1) == 0;
                    };

                case "engine":
                    return (x, y) =>
                    {
                        bool block = x > 0.2f && x < 0.8f && y > 0.2f && y < 0.64f;
                        bool cover = x > 0.3f && x < 0.7f && y > 0.64f && y < 0.74f;
                        bool pipe = x > 0.45f && x < 0.55f && y > 0.74f && y < 0.84f;
                        bool cap = x > 0.34f && x < 0.66f && y > 0.84f && y < 0.91f;
                        bool left = x > 0.08f && x < 0.2f && y > 0.32f && y < 0.54f;
                        bool leftEnd = x > 0.04f && x < 0.1f && y > 0.26f && y < 0.6f;
                        bool right = x > 0.8f && x < 0.9f && y > 0.36f && y < 0.5f;
                        bool rightEnd = x > 0.88f && x < 0.96f && y > 0.28f && y < 0.58f;
                        bool feet = (x > 0.26f && x < 0.38f || x > 0.62f && x < 0.74f) && y > 0.1f && y < 0.2f;
                        bool bolt = InPoly(x, y, 0.55f, 0.6f, 0.38f, 0.4f, 0.49f, 0.4f, 0.44f, 0.24f, 0.63f, 0.45f, 0.52f, 0.45f);
                        return (block || cover || pipe || cap || left || leftEnd || right || rightEnd || feet) && !bolt;
                    };

                case "spring":
                    return (x, y) =>
                    {
                        bool rod = x > 0.46f && x < 0.54f && y > 0.03f && y < 0.97f;
                        bool topCap = x > 0.26f && x < 0.74f && y > 0.82f && y < 0.9f;
                        bool botCap = x > 0.26f && x < 0.74f && y > 0.1f && y < 0.18f;
                        bool eyeT = Dist(x, y, 0.5f, 0.93f) < 0.06f, eyeB = Dist(x, y, 0.5f, 0.07f) < 0.06f;
                        bool coil = false;
                        if (x > 0.3f && x < 0.7f && y > 0.18f && y < 0.82f)
                        {
                            float t = (y - 0.18f) / 0.64f * 5f + (x - 0.5f) * 2.2f;
                            coil = Mathf.Repeat(t, 1f) < 0.42f;
                        }
                        return rod || topCap || botCap || coil || eyeT || eyeB;
                    };

                case "disc":
                    return (x, y) =>
                    {
                        float d = Dist(x, y, 0.5f, 0.5f);
                        float a = Mathf.Atan2(y - 0.5f, x - 0.5f) * Mathf.Rad2Deg;
                        bool ring = d < 0.43f && d > 0.2f;
                        for (int i = 0; i < 10; i++)
                        {
                            float ang = i * 36f * Mathf.Deg2Rad;
                            if (Dist(x, y, 0.5f + Mathf.Cos(ang) * 0.32f, 0.5f + Mathf.Sin(ang) * 0.32f) < 0.035f) ring = false;
                        }
                        bool hub = d < 0.15f && d > 0.06f;
                        bool caliper = a > 20f && a < 80f && d > 0.34f && d < 0.5f;
                        bool gap = a > 20f && a < 80f && d > 0.43f && d < 0.46f;
                        return (ring || hub || caliper) && !gap;
                    };

                case "bottle":
                    return (x, y) =>
                    {
                        // Bình nitro nghiêng 30°.
                        float cx = x - 0.5f, cy = y - 0.5f;
                        float c = Mathf.Cos(-0.52f), s = Mathf.Sin(-0.52f);
                        float u = cx * c - cy * s + 0.5f, v = cx * s + cy * c + 0.5f;
                        bool body = u > 0.35f && u < 0.65f && v > 0.14f && v < 0.64f;
                        bool bottom = Dist(u, v, 0.5f, 0.14f) < 0.15f && v < 0.14f;
                        bool shoulder = Dist(u, v, 0.5f, 0.62f) < 0.15f && v > 0.62f;
                        bool neck = u > 0.44f && u < 0.56f && v > 0.7f && v < 0.82f;
                        bool valve = u > 0.37f && u < 0.63f && v > 0.8f && v < 0.88f;
                        bool knob = u > 0.6f && u < 0.72f && v > 0.82f && v < 0.86f;
                        bool band = u > 0.35f && u < 0.65f && ((v > 0.3f && v < 0.34f) || (v > 0.52f && v < 0.56f));
                        return (body || bottom || shoulder || neck || valve || knob) && !band;
                    };

                case "rocket":
                    return (x, y) =>
                    {
                        float cx = x - 0.5f, cy = y - 0.5f;
                        float c = Mathf.Cos(-0.785f), s = Mathf.Sin(-0.785f);
                        float u = cx * c - cy * s + 0.5f, v = cx * s + cy * c + 0.5f;
                        float ex = (u - 0.5f) / 0.13f, ey = (v - 0.55f) / 0.36f;
                        bool body = ex * ex + ey * ey < 1f && v > 0.28f;
                        bool window = Dist(u, v, 0.5f, 0.62f) < 0.055f;
                        bool finL = InPoly(u, v, 0.38f, 0.42f, 0.24f, 0.2f, 0.38f, 0.28f);
                        bool finR = InPoly(u, v, 0.62f, 0.42f, 0.76f, 0.2f, 0.62f, 0.28f);
                        bool flame = InPoly(u, v, 0.42f, 0.24f, 0.5f, 0.04f, 0.58f, 0.24f);
                        return (body && !window) || finL || finR || flame;
                    };

                case "steering":
                    return (x, y) =>
                    {
                        float d = Dist(x, y, 0.5f, 0.5f);
                        bool ring = d < 0.46f && d > 0.35f;
                        bool hub = d < 0.13f;
                        bool spokeL = y > 0.44f && y < 0.56f && d < 0.4f && x < 0.5f;
                        bool spokeR = y > 0.44f && y < 0.56f && d < 0.4f && x > 0.5f;
                        bool spokeD = x > 0.44f && x < 0.56f && y < 0.5f && d < 0.4f;
                        return ring || hub || spokeL || spokeR || spokeD;
                    };

                case "speed":
                    return (x, y) =>
                    {
                        float d = Dist(x, y, 0.5f, 0.42f);
                        float a = Mathf.Atan2(y - 0.42f, x - 0.5f) * Mathf.Rad2Deg;
                        bool arc = d < 0.46f && d > 0.36f && (a > -25f || a < -155f) && y > 0.2f;
                        bool needle = InPoly(x, y, 0.45f, 0.38f, 0.55f, 0.46f, 0.78f, 0.7f);
                        bool hub = d < 0.08f;
                        return arc || needle || hub;
                    };

                case "plus": return (x, y) => (Mathf.Abs(x - 0.5f) < 0.08f && Mathf.Abs(y - 0.5f) < 0.38f) || (Mathf.Abs(y - 0.5f) < 0.08f && Mathf.Abs(x - 0.5f) < 0.38f);
                case "close":
                    return (x, y) =>
                    {
                        float u = (x - 0.5f + y - 0.5f) * 0.7071f, v = (y - 0.5f - (x - 0.5f)) * 0.7071f;
                        return (Mathf.Abs(u) < 0.07f && Mathf.Abs(v) < 0.4f) || (Mathf.Abs(v) < 0.07f && Mathf.Abs(u) < 0.4f);
                    };
                case "power":
                    return (x, y) =>
                    {
                        float d = Dist(x, y, 0.5f, 0.46f);
                        bool ring = d < 0.4f && d > 0.29f && !(y > 0.6f && Mathf.Abs(x - 0.5f) < 0.16f);
                        bool bar = Mathf.Abs(x - 0.5f) < 0.06f && y > 0.42f && y < 0.94f;
                        return ring || bar;
                    };

                default: return (x, y) => false;
            }
        }

        // ───────────────────────── Ảnh màu ─────────────────────────

        /// <summary>Logo đồng hồ tốc độ nhiều màu (xanh → vàng → đỏ) như game đua xe.</summary>
        public static void SpeedLogo(float x, float y, out float r, out float g, out float b, out float a)
        {
            r = g = b = a = 0f;
            float cx = 0.5f, cy = 0.36f;
            float d = Dist(x, y, cx, cy);
            float ang = Mathf.Atan2(y - cy, x - cx) * Mathf.Rad2Deg;   // -180..180
            // Cung từ 200° (trái-dưới) qua 90° (trên) tới -20° (phải-dưới). t: 0 ở trái → 1 ở phải.
            float deg = ang < -90f ? ang + 360f : ang;                   // -90..270
            float t = Mathf.InverseLerp(200f, -20f, deg);
            bool inArc = deg >= -20f && deg <= 200f;
            if (inArc && d > 0.37f && d < 0.48f)
            {
                // Vạch chia: khe nhỏ mỗi 22°.
                float seg = Mathf.Repeat((200f - deg) / 22f, 1f);
                if (seg > 0.12f)
                {
                    Ramp(t, out r, out g, out b);
                    float shade = 0.75f + 0.25f * Mathf.InverseLerp(0.37f, 0.48f, d);
                    r *= shade; g *= shade; b *= shade;
                    a = 1f;
                }
            }
            // Vạch nhỏ bên trong
            if (inArc && d > 0.3f && d < 0.34f && Mathf.Repeat((200f - deg) / 11f, 1f) < 0.25f)
            {
                r = g = b = 0.85f; a = 0.9f;
            }
            // Kim
            float na = 55f * Mathf.Deg2Rad;
            float px = x - cx, py = y - cy;
            float along = px * Mathf.Cos(na) + py * Mathf.Sin(na);
            float across = -px * Mathf.Sin(na) + py * Mathf.Cos(na);
            if (along > -0.04f && along < 0.36f && Mathf.Abs(across) < 0.028f * (1f - along / 0.42f) + 0.004f)
            {
                r = 1f; g = 0.95f; b = 0.85f; a = 1f;
            }
            if (d < 0.075f) { r = 0.9f; g = 0.93f; b = 1f; a = 1f; }
            if (d < 0.04f) { r = 0.2f; g = 0.35f; b = 0.7f; a = 1f; }
        }

        static void Ramp(float t, out float r, out float g, out float b)
        {
            // xanh dương → xanh lơ → vàng → cam → đỏ
            Color3(t, new[] { 0f, 0.3f, 0.45f, 0.6f, 0.8f, 1f },
                   new[] { 0.15f, 0.2f, 0.35f, 1f, 1f, 1f },
                   new[] { 0.45f, 0.85f, 1f, 0.88f, 0.5f, 0.15f },
                   new[] { 1f, 1f, 0.55f, 0.2f, 0.1f, 0.2f }, out r, out g, out b);
        }

        static void Color3(float t, float[] k, float[] R, float[] G, float[] B, out float r, out float g, out float b)
        {
            int i = 0;
            while (i < k.Length - 2 && t > k[i + 1]) i++;
            float f = Mathf.InverseLerp(k[i], k[i + 1], t);
            r = Mathf.Lerp(R[i], R[i + 1], f);
            g = Mathf.Lerp(G[i], G[i + 1], f);
            b = Mathf.Lerp(B[i], B[i + 1], f);
        }

        /// <summary>Đồng xu vàng bóng có viền và chữ "X" khắc nổi.</summary>
        public static void GoldCoin(float x, float y, out float r, out float g, out float b, out float a)
        {
            r = g = b = a = 0f;
            float d = Dist(x, y, 0.5f, 0.5f);
            if (d > 0.47f) return;
            a = Mathf.Clamp01((0.47f - d) * 60f);
            float light = Mathf.Clamp01(0.55f + (y - x) * 0.6f);     // sáng ở trên-trái
            if (d > 0.38f)
            {
                r = 0.85f + 0.15f * light; g = 0.55f + 0.2f * light; b = 0.08f;               // viền
            }
            else
            {
                r = 1f; g = 0.72f + 0.18f * light; b = 0.18f + 0.2f * light;                   // mặt
                if (d > 0.33f && d < 0.36f) { r = 0.9f; g = 0.6f; b = 0.1f; }                  // rãnh
                // Ký hiệu "$" đơn giản: chữ S + vạch đứng
                float sx = x - 0.5f, sy = y - 0.5f;
                bool bar = Mathf.Abs(sx) < 0.025f && Mathf.Abs(sy) < 0.22f;
                float tTop = Dist(x, y, 0.5f, 0.57f), tBot = Dist(x, y, 0.5f, 0.43f);
                bool sTop = tTop < 0.1f && tTop > 0.05f && !(sx > 0f && sy < 0.07f && sy > 0.03f) && !(sx > 0.02f && sy < 0.07f);
                bool sBot = tBot < 0.1f && tBot > 0.05f && !(sx < -0.02f && sy > -0.07f);
                if (bar || sTop || sBot) { r = 0.82f; g = 0.5f; b = 0.05f; }
            }
            // Vệt sáng
            float hl = Dist(x, y, 0.36f, 0.68f);
            if (hl < 0.1f) { float k = (0.1f - hl) / 0.1f * 0.5f; r += k; g += k; b += k; }
            r = Mathf.Min(r, 1f); g = Mathf.Min(g, 1f); b = Mathf.Min(b, 1f);
        }

        // ───────────────────────── Khung ─────────────────────────

        /// <summary>
        /// Khoảng cách có dấu tới khung chữ nhật bo góc, có thể vát góc trên-phải và dưới-trái (kiểu game).
        /// Âm = bên trong. Đơn vị pixel.
        /// </summary>
        public static float PanelSdf(float px, float py, float cx, float cy, float hx, float hy, float radius, float chamfer)
        {
            float qx = Mathf.Abs(px - cx) - (hx - radius);
            float qy = Mathf.Abs(py - cy) - (hy - radius);
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            float d = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
            if (chamfer > 0f)
            {
                float k = hx + hy - chamfer;
                float tr = ((px - cx) + (py - cy) - k) * 0.7071f;
                float bl = (-(px - cx) - (py - cy) - k) * 0.7071f;
                d = Mathf.Max(d, Mathf.Max(tr, bl));
            }
            return d;
        }

        public static float Dist(float x, float y, float cx, float cy) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));

        public static bool InPoly(float x, float y, params float[] p)
        {
            bool inside = false;
            int n = p.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = p[i * 2], yi = p[i * 2 + 1], xj = p[j * 2], yj = p[j * 2 + 1];
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi + 1e-6f) + xi) inside = !inside;
            }
            return inside;
        }
    }
}
