# Regenera icon.ico y big_icon.png (logo "mi" de la barra de título).
# Uso desde la raíz del proyecto:
#   powershell -ExecutionPolicy Bypass -File tools\GenerateAppIcon.ps1

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$iconPath = Join-Path $root 'Assets\icon.ico'
$pngPath = Join-Path $root 'big_icon.png'

Add-Type -AssemblyName System.Drawing

$source = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

public static class XiaomiAppIconGenerator
{
    public static void Generate(string root)
    {
        string iconPath = Path.Combine(root, "icon.ico");
        string pngPath = Path.Combine(root, "big_icon.png");
        SaveIco(iconPath, new[] { 16, 24, 32, 48, 64, 128, 256 });
        using (Bitmap png = Render(256))
            png.Save(pngPath, ImageFormat.Png);
    }

    static Bitmap Render(int size)
    {
        Bitmap bmp = new Bitmap(size, size);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Black);

            float m = size * 0.12f;
            float w = size - m * 2f;
            float radius = size * 0.18f;

            using (Pen pen = new Pen(Color.White, Math.Max(1.5f, size / 14f)))
            using (GraphicsPath path = new GraphicsPath())
            {
                float d = radius * 2f;
                path.AddArc(m, m, d, d, 180, 90);
                path.AddArc(m + w - d, m, d, d, 270, 90);
                path.AddArc(m + w - d, m + w - d, d, d, 0, 90);
                path.AddArc(m, m + w - d, d, d, 90, 90);
                path.CloseFigure();
                g.DrawPath(pen, path);
            }

            using (Font font = new Font("Segoe UI", size * 0.34f, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                RectangleF rect = new RectangleF(m, m + size * 0.02f, w, w);
                using (StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                })
                {
                    g.DrawString("mi", font, Brushes.White, rect, sf);
                }
            }
        }

        return bmp;
    }

    static void SaveIco(string path, int[] sizes)
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write((ushort)0);
            bw.Write((ushort)1);
            bw.Write((ushort)sizes.Length);

            byte[][] images = new byte[sizes.Length][];
            for (int i = 0; i < sizes.Length; i++)
            {
                using (Bitmap bmp = Render(sizes[i]))
                using (MemoryStream pngMs = new MemoryStream())
                {
                    bmp.Save(pngMs, ImageFormat.Png);
                    images[i] = pngMs.ToArray();
                }
            }

            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                bw.Write((byte)(s >= 256 ? 0 : s));
                bw.Write((byte)(s >= 256 ? 0 : s));
                bw.Write((byte)0);
                bw.Write((byte)0);
                bw.Write((ushort)1);
                bw.Write((ushort)32);
                bw.Write(images[i].Length);
                bw.Write(offset);
                offset += images[i].Length;
            }

            for (int i = 0; i < sizes.Length; i++)
                bw.Write(images[i]);

            File.WriteAllBytes(path, ms.ToArray());
        }
    }
}
'@

if (-not ([System.Management.Automation.PSTypeName]'XiaomiAppIconGenerator').Type) {
    Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing
}

[XiaomiAppIconGenerator]::Generate($root)
Write-Host "OK: $iconPath"
Write-Host "OK: $pngPath"
Write-Host "Recompila el proyecto (Release) para aplicar el icono al .exe."
