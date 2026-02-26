using System.Globalization;
using SixLabors.ImageSharp.Drawing;

namespace EPaperDashboard.Utilities;

/// <summary>
/// Parses a subset of SVG path "d" data into an ImageSharp <see cref="IPath"/>.
/// Supports: M/m, L/l, H/h, V/v, C/c, S/s, Q/q, A/a, Z/z commands.
/// This covers all commands used by Font Awesome solid icons.
/// </summary>
public static class SvgPathParser
{
    public static IPath Parse(string pathData)
    {
        var builder = new PathBuilder();
        var tokens = Tokenize(pathData);
        int i = 0;
        float cx = 0, cy = 0; // current point
        float sx = 0, sy = 0; // start of current sub-path
        float lastCx2 = 0, lastCy2 = 0; // last control point for smooth curves
        char lastCmd = ' ';

        while (i < tokens.Count)
        {
            var token = tokens[i];
            if (token.IsCommand)
            {
                char cmd = token.Command;
                i++;

                switch (cmd)
                {
                    case 'M':
                    case 'm':
                    {
                        bool first = true;
                        while (i < tokens.Count && !tokens[i].IsCommand)
                        {
                            var x = tokens[i++].Value;
                            var y = i < tokens.Count && !tokens[i].IsCommand ? tokens[i++].Value : 0;
                            if (cmd == 'm') { x += cx; y += cy; }
                            if (first)
                            {
                                builder.MoveTo(new System.Numerics.Vector2(x, y));
                                sx = x; sy = y;
                                first = false;
                            }
                            else
                            {
                                // Subsequent coordinate pairs after M are implicit LineTo
                                builder.LineTo(new System.Numerics.Vector2(x, y));
                            }
                            cx = x; cy = y;
                        }
                        lastCmd = cmd;
                        break;
                    }
                    case 'L':
                    case 'l':
                    {
                        while (i < tokens.Count && !tokens[i].IsCommand)
                        {
                            var x = tokens[i++].Value;
                            var y = i < tokens.Count && !tokens[i].IsCommand ? tokens[i++].Value : 0;
                            if (cmd == 'l') { x += cx; y += cy; }
                            builder.LineTo(new System.Numerics.Vector2(x, y));
                            cx = x; cy = y;
                        }
                        lastCmd = cmd;
                        break;
                    }
                    case 'H':
                    case 'h':
                    {
                        while (i < tokens.Count && !tokens[i].IsCommand)
                        {
                            var x = tokens[i++].Value;
                            if (cmd == 'h') x += cx;
                            builder.LineTo(new System.Numerics.Vector2(x, cy));
                            cx = x;
                        }
                        lastCmd = cmd;
                        break;
                    }
                    case 'V':
                    case 'v':
                    {
                        while (i < tokens.Count && !tokens[i].IsCommand)
                        {
                            var y = tokens[i++].Value;
                            if (cmd == 'v') y += cy;
                            builder.LineTo(new System.Numerics.Vector2(cx, y));
                            cy = y;
                        }
                        lastCmd = cmd;
                        break;
                    }
                    case 'C':
                    case 'c':
                    {
                        while (i + 5 < tokens.Count && !tokens[i].IsCommand)
                        {
                            var x1 = tokens[i++].Value;
                            var y1 = tokens[i++].Value;
                            var x2 = tokens[i++].Value;
                            var y2 = tokens[i++].Value;
                            var x = tokens[i++].Value;
                            var y = tokens[i++].Value;
                            if (cmd == 'c') { x1 += cx; y1 += cy; x2 += cx; y2 += cy; x += cx; y += cy; }
                            builder.CubicBezierTo(
                                new System.Numerics.Vector2(x1, y1),
                                new System.Numerics.Vector2(x2, y2),
                                new System.Numerics.Vector2(x, y));
                            lastCx2 = x2; lastCy2 = y2;
                            cx = x; cy = y;
                        }
                        lastCmd = cmd;
                        break;
                    }
                    case 'S':
                    case 's':
                    {
                        while (i + 3 < tokens.Count && !tokens[i].IsCommand)
                        {
                            // Reflect previous control point
                            float x1, y1;
                            if (lastCmd is 'C' or 'c' or 'S' or 's')
                            {
                                x1 = 2 * cx - lastCx2;
                                y1 = 2 * cy - lastCy2;
                            }
                            else
                            {
                                x1 = cx;
                                y1 = cy;
                            }

                            var x2 = tokens[i++].Value;
                            var y2 = tokens[i++].Value;
                            var x = tokens[i++].Value;
                            var y = tokens[i++].Value;
                            if (cmd == 's') { x2 += cx; y2 += cy; x += cx; y += cy; }
                            builder.CubicBezierTo(
                                new System.Numerics.Vector2(x1, y1),
                                new System.Numerics.Vector2(x2, y2),
                                new System.Numerics.Vector2(x, y));
                            lastCx2 = x2; lastCy2 = y2;
                            cx = x; cy = y;
                            lastCmd = cmd;
                        }
                        break;
                    }
                    case 'Q':
                    case 'q':
                    {
                        while (i + 3 < tokens.Count && !tokens[i].IsCommand)
                        {
                            var x1 = tokens[i++].Value;
                            var y1 = tokens[i++].Value;
                            var x = tokens[i++].Value;
                            var y = tokens[i++].Value;
                            if (cmd == 'q') { x1 += cx; y1 += cy; x += cx; y += cy; }
                            builder.QuadraticBezierTo(
                                new System.Numerics.Vector2(x1, y1),
                                new System.Numerics.Vector2(x, y));
                            lastCx2 = x1; lastCy2 = y1;
                            cx = x; cy = y;
                        }
                        lastCmd = cmd;
                        break;
                    }
                    case 'A':
                    case 'a':
                    {
                        while (i + 6 < tokens.Count && !tokens[i].IsCommand)
                        {
                            var rx = tokens[i++].Value;
                            var ry = tokens[i++].Value;
                            var rotation = tokens[i++].Value;
                            var largeArc = tokens[i++].Value != 0;
                            var sweep = tokens[i++].Value != 0;
                            var x = tokens[i++].Value;
                            var y = tokens[i++].Value;
                            if (cmd == 'a') { x += cx; y += cy; }
                            // Approximate arc with cubic bezier segments
                            ApproximateArc(builder, cx, cy, rx, ry, rotation, largeArc, sweep, x, y);
                            cx = x; cy = y;
                        }
                        lastCmd = cmd;
                        break;
                    }
                    case 'Z':
                    case 'z':
                        builder.CloseFigure();
                        cx = sx; cy = sy;
                        lastCmd = cmd;
                        break;
                }
            }
            else
            {
                // Shouldn't happen with well-formed paths, skip
                i++;
            }
        }

        return builder.Build();
    }

    // =========================================================================
    // TOKENIZER
    // =========================================================================

    private readonly struct PathToken
    {
        public bool IsCommand { get; init; }
        public char Command { get; init; }
        public float Value { get; init; }
    }

    private static List<PathToken> Tokenize(string d)
    {
        var tokens = new List<PathToken>(d.Length / 3);
        int i = 0;
        while (i < d.Length)
        {
            char c = d[i];

            // Skip whitespace and commas
            if (c is ' ' or '\t' or '\n' or '\r' or ',')
            {
                i++;
                continue;
            }

            // Command letter (but not start of exponent like 'e' in "1.5e2")
            if (char.IsLetter(c) && c is not 'e' and not 'E')
            {
                tokens.Add(new PathToken { IsCommand = true, Command = c });
                i++;
                continue;
            }

            // Number
            int start = i;
            if (c is '-' or '+') i++;
            bool hasDot = false;
            while (i < d.Length)
            {
                c = d[i];
                if (c is '.' && !hasDot) { hasDot = true; i++; }
                else if (char.IsDigit(c)) i++;
                else if (c is 'e' or 'E')
                {
                    i++;
                    if (i < d.Length && d[i] is '-' or '+') i++;
                }
                else break;
            }

            if (i > start && float.TryParse(d.AsSpan(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
            {
                tokens.Add(new PathToken { IsCommand = false, Value = val });
            }
            else if (i == start)
            {
                i++; // skip unrecognized character
            }
        }

        return tokens;
    }

    // =========================================================================
    // ARC APPROXIMATION (endpoint → center parameterization → cubic beziers)
    // =========================================================================

    private static void ApproximateArc(
        PathBuilder builder,
        float x1, float y1,
        float rxIn, float ryIn,
        float rotationDeg,
        bool largeArc,
        bool sweep,
        float x2, float y2)
    {
        // Degenerate cases
        if (Math.Abs(x1 - x2) < 0.001f && Math.Abs(y1 - y2) < 0.001f) return;
        if (rxIn == 0 || ryIn == 0)
        {
            builder.LineTo(new System.Numerics.Vector2(x2, y2));
            return;
        }

        var rx = Math.Abs(rxIn);
        var ry = Math.Abs(ryIn);
        var phi = rotationDeg * MathF.PI / 180f;
        var cosPhi = MathF.Cos(phi);
        var sinPhi = MathF.Sin(phi);

        // Step 1: compute (x1', y1')
        var dx2 = (x1 - x2) / 2f;
        var dy2 = (y1 - y2) / 2f;
        var x1p = cosPhi * dx2 + sinPhi * dy2;
        var y1p = -sinPhi * dx2 + cosPhi * dy2;

        // Ensure radii are large enough
        var x1p2 = x1p * x1p;
        var y1p2 = y1p * y1p;
        var rx2 = rx * rx;
        var ry2 = ry * ry;
        var lambda = x1p2 / rx2 + y1p2 / ry2;
        if (lambda > 1)
        {
            var lambdaSqrt = MathF.Sqrt(lambda);
            rx *= lambdaSqrt;
            ry *= lambdaSqrt;
            rx2 = rx * rx;
            ry2 = ry * ry;
        }

        // Step 2: compute (cx', cy')
        var num = Math.Max(0, rx2 * ry2 - rx2 * y1p2 - ry2 * x1p2);
        var den = rx2 * y1p2 + ry2 * x1p2;
        var sq = den > 0 ? MathF.Sqrt((float)(num / den)) : 0;
        if (largeArc == sweep) sq = -sq;
        var cxp = sq * rx * y1p / ry;
        var cyp = -sq * ry * x1p / rx;

        // Step 3: compute (cx, cy)
        var centerX = cosPhi * cxp - sinPhi * cyp + (x1 + x2) / 2f;
        var centerY = sinPhi * cxp + cosPhi * cyp + (y1 + y2) / 2f;

        // Step 4: compute angles
        var theta1 = AngleBetween(1, 0, (x1p - cxp) / rx, (y1p - cyp) / ry);
        var dtheta = AngleBetween((x1p - cxp) / rx, (y1p - cyp) / ry, (-x1p - cxp) / rx, (-y1p - cyp) / ry);

        if (!sweep && dtheta > 0) dtheta -= 2 * MathF.PI;
        if (sweep && dtheta < 0) dtheta += 2 * MathF.PI;

        // Split into segments of at most π/2
        var segments = (int)Math.Ceiling(Math.Abs(dtheta) / (MathF.PI / 2));
        if (segments < 1) segments = 1;
        var segAngle = dtheta / segments;

        for (int seg = 0; seg < segments; seg++)
        {
            var a1 = theta1 + seg * segAngle;
            var a2 = theta1 + (seg + 1) * segAngle;
            ArcSegmentToCubic(builder, centerX, centerY, rx, ry, cosPhi, sinPhi, a1, a2);
        }
    }

    private static void ArcSegmentToCubic(
        PathBuilder builder,
        float cx, float cy,
        float rx, float ry,
        float cosPhi, float sinPhi,
        float a1, float a2)
    {
        var alpha = 4f / 3f * MathF.Tan((a2 - a1) / 4f);
        var cos1 = MathF.Cos(a1);
        var sin1 = MathF.Sin(a1);
        var cos2 = MathF.Cos(a2);
        var sin2 = MathF.Sin(a2);

        var e1x = rx * cos1;
        var e1y = ry * sin1;
        var e2x = rx * cos2;
        var e2y = ry * sin2;

        var cp1x = e1x - alpha * rx * sin1;
        var cp1y = e1y + alpha * ry * cos1;
        var cp2x = e2x + alpha * rx * sin2;
        var cp2y = e2y - alpha * ry * cos2;

        // Transform back
        var p1x = cosPhi * cp1x - sinPhi * cp1y + cx;
        var p1y = sinPhi * cp1x + cosPhi * cp1y + cy;
        var p2x = cosPhi * cp2x - sinPhi * cp2y + cx;
        var p2y = sinPhi * cp2x + cosPhi * cp2y + cy;
        var ex = cosPhi * e2x - sinPhi * e2y + cx;
        var ey = sinPhi * e2x + cosPhi * e2y + cy;

        builder.CubicBezierTo(
            new System.Numerics.Vector2(p1x, p1y),
            new System.Numerics.Vector2(p2x, p2y),
            new System.Numerics.Vector2(ex, ey));
    }

    private static float AngleBetween(float ux, float uy, float vx, float vy)
    {
        var uLen = MathF.Sqrt(ux * ux + uy * uy);
        var vLen = MathF.Sqrt(vx * vx + vy * vy);
        if (uLen < 1e-10 || vLen < 1e-10) return 0;
        var cos = Math.Clamp((ux * vx + uy * vy) / (uLen * vLen), -1, 1);
        var angle = MathF.Acos((float)cos);
        if (ux * vy - uy * vx < 0) angle = -angle;
        return angle;
    }
}
