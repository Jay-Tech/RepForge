using SkiaSharp;

// Repo root = two levels up from tools/IconGen, wherever the tool is run from.
var repoDir = new DirectoryInfo(AppContext.BaseDirectory);
while (repoDir is not null && !File.Exists(Path.Combine(repoDir.FullName, "RepForge.sln")))
    repoDir = repoDir.Parent;
string Repo = repoDir?.FullName ?? throw new InvalidOperationException("RepForge.sln not found above " + AppContext.BaseDirectory);
string Scratch = Path.Combine(Repo, "tools", "IconGen", "preview");

// All geometry is authored on a 1024×1024 reference canvas; u = size/1024.

static void DrawDumbbell(SKCanvas c, float cx, float cy, float u, float s)
{
    c.Save();
    c.Translate(cx, cy);
    c.RotateDegrees(-25);

    using var barPaint = new SKPaint
    {
        IsAntialias = true,
        Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, -46 * u * s), new SKPoint(0, 46 * u * s),
            [new SKColor(0xF2, 0xEF, 0xEA), new SKColor(0xC9, 0xC3, 0xBB)],
            null, SKShaderTileMode.Clamp),
    };
    c.DrawRoundRect(new SKRect(-400 * u * s, -42 * u * s, 400 * u * s, 42 * u * s),
        42 * u * s, 42 * u * s, barPaint);

    using var platePaint = new SKPaint
    {
        IsAntialias = true,
        Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, -205 * u * s), new SKPoint(0, 205 * u * s),
            [new SKColor(0xFF, 0xA8, 0x3C), new SKColor(0xFF, 0x4E, 0x0A)],
            null, SKShaderTileMode.Clamp),
    };
    foreach (var sign in new[] { -1f, 1f })
    {
        // inner (taller) then outer plate
        c.DrawRoundRect(new SKRect(sign * 190 * u * s - 62 * u * s, -205 * u * s,
                                   sign * 190 * u * s + 62 * u * s, 205 * u * s),
            40 * u * s, 40 * u * s, platePaint);
        c.DrawRoundRect(new SKRect(sign * 322 * u * s - 52 * u * s, -158 * u * s,
                                   sign * 322 * u * s + 52 * u * s, 158 * u * s),
            36 * u * s, 36 * u * s, platePaint);
    }
    c.Restore();
}

static void DrawGlow(SKCanvas c, float cx, float cy, float u, float radius, byte alpha)
{
    using var glow = new SKPaint
    {
        IsAntialias = true,
        Shader = SKShader.CreateRadialGradient(
            new SKPoint(cx, cy), radius * u,
            [new SKColor(0xFF, 0x7A, 0x18, alpha), new SKColor(0xFF, 0x7A, 0x18, 0)],
            null, SKShaderTileMode.Clamp),
    };
    c.DrawRect(new SKRect(0, 0, cx * 2, cy * 2), glow);
}

static void DrawBackground(SKCanvas c, float size, float u, float cornerRadius, float margin)
{
    using var bg = new SKPaint
    {
        IsAntialias = true,
        Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0), new SKPoint(0, size),
            [new SKColor(0x24, 0x26, 0x2E), new SKColor(0x12, 0x13, 0x18)],
            null, SKShaderTileMode.Clamp),
    };
    var m = margin * u;
    c.DrawRoundRect(new SKRect(m, m, size - m, size - m),
        cornerRadius * u, cornerRadius * u, bg);
}

static SKData Render(int size, Action<SKCanvas, float> draw)
{
    var info = new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
    using var surface = SKSurface.Create(info);
    surface.Canvas.Clear(SKColors.Transparent);
    draw(surface.Canvas, size / 1024f);
    surface.Canvas.Flush();
    using var img = surface.Snapshot();
    return img.Encode(SKEncodedImageFormat.Png, 100);
}

// Full icon: background + glow + dumbbell (for legacy launcher, splash, ico).
static SKData RenderFull(int size) => Render(size, (c, u) =>
{
    DrawBackground(c, size, u, 224, 8);
    DrawGlow(c, size / 2f, size / 2f, u, 520, 66);
    DrawDumbbell(c, size / 2f, size / 2f, u, 0.86f);
});

// Adaptive foreground: transparent, dumbbell sized for the 66/108dp safe zone.
static SKData RenderForeground(int size) => Render(size, (c, u) =>
{
    DrawGlow(c, size / 2f, size / 2f, u, 360, 60);
    DrawDumbbell(c, size / 2f, size / 2f, u, 0.56f);
});

static void Save(SKData png, string path)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllBytes(path, png.ToArray());
    Console.WriteLine($"wrote {path}");
}

static void WriteIco(string path, int[] sizes)
{
    var images = sizes.Select(sz => RenderFull(sz).ToArray()).ToList();
    using var w = new BinaryWriter(File.Create(path));
    w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
    var offset = 6 + 16 * sizes.Length;
    for (var i = 0; i < sizes.Length; i++)
    {
        var dim = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
        w.Write(dim); w.Write(dim); w.Write((byte)0); w.Write((byte)0);
        w.Write((short)1); w.Write((short)32);
        w.Write(images[i].Length); w.Write(offset);
        offset += images[i].Length;
    }
    foreach (var img in images)
        w.Write(img);
    Console.WriteLine($"wrote {path}");
}

// --- outputs ---

// Design preview for review
Save(RenderFull(256), Path.Combine(Scratch, "icon-preview.png"));
Save(RenderForeground(256), Path.Combine(Scratch, "icon-foreground-preview.png"));

// Android legacy launcher icons
(string Dir, int Size)[] densities =
    [("mdpi", 48), ("hdpi", 72), ("xhdpi", 96), ("xxhdpi", 144), ("xxxhdpi", 192)];
foreach (var (dir, size) in densities)
    Save(RenderFull(size), Path.Combine(Repo, "RepForge.Android", "Resources", $"mipmap-{dir}", "ic_launcher.png"));

// Android adaptive foreground layers (108dp base)
(string Dir, int Size)[] fgDensities =
    [("mdpi", 108), ("hdpi", 162), ("xhdpi", 216), ("xxhdpi", 324), ("xxxhdpi", 432)];
foreach (var (dir, size) in fgDensities)
    Save(RenderForeground(size), Path.Combine(Repo, "RepForge.Android", "Resources", $"mipmap-{dir}", "ic_launcher_foreground.png"));

// Splash image (replaces old drawable/icon usage)
Save(RenderFull(432), Path.Combine(Repo, "RepForge.Android", "Resources", "drawable", "icon.png"));

// Desktop window/exe icon
WriteIco(Path.Combine(Repo, "RepForge", "Assets", "repforge.ico"), [16, 24, 32, 48, 64, 128, 256]);

Console.WriteLine("done");
