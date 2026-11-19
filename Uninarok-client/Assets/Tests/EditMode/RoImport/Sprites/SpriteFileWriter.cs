using System.IO;
using System.Text;
using Uninarok.RoImport.Core.Sprites;

namespace Uninarok.RoImport.Tests.Sprites
{
    /// <summary>
    /// Test-only writer of SPR, ACT and PAL bytes from the synthetic samples (TASKS-0007 T5.1), so the converter tests run
    /// through the real GRF adapters without the data set. SPR 2.0 (frames uncompressed; true colour stored A, B, G, R
    /// bottom row first, as the format does), ACT 2.5, PAL 1,024 bytes. Production code never writes these formats; the
    /// round trip through the readers is pinned by <c>SpriteTestFilesWriterTests</c>.
    /// </summary>
    public static class SpriteFileWriter
    {
        public static byte[] Spr(RoSpr spr, int major = 2, int minor = 0)
        {
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream))
            {
                w.Write((byte)'S');
                w.Write((byte)'P');
                w.Write((byte)minor);
                w.Write((byte)major);
                w.Write((ushort)spr.Indexed.Count);
                if (major >= 2) w.Write((ushort)spr.TrueColor.Count);
                foreach (var f in spr.Indexed)
                {
                    w.Write((ushort)f.Width);
                    w.Write((ushort)f.Height);
                    w.Write(f.Indices);
                }
                if (major >= 2)
                    foreach (var f in spr.TrueColor)
                    {
                        w.Write((ushort)f.Width);
                        w.Write((ushort)f.Height);
                        for (int y = f.Height - 1; y >= 0; y--)
                            for (int x = 0; x < f.Width; x++)
                            {
                                int i = (y * f.Width + x) * 4;
                                w.Write(f.Rgba[i + 3]);
                                w.Write(f.Rgba[i + 2]);
                                w.Write(f.Rgba[i + 1]);
                                w.Write(f.Rgba[i]);
                            }
                    }
                var palette = spr.Palette ?? SpriteTestData.Palette();
                w.Write(Pal(palette));
                return stream.ToArray();
            }
        }

        public static byte[] Pal(RoPal pal)
        {
            var bytes = new byte[1024];
            for (int i = 0; i < 256; i++)
            {
                bytes[i * 4] = pal.Colors[i].r;
                bytes[i * 4 + 1] = pal.Colors[i].g;
                bytes[i * 4 + 2] = pal.Colors[i].b;
                bytes[i * 4 + 3] = 0;   // the files leave it 0 (T0.1)
            }
            return bytes;
        }

        public static byte[] Act(RoAct act)
        {
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream))
            {
                w.Write((byte)'A');
                w.Write((byte)'C');
                w.Write((byte)5);   // minor
                w.Write((byte)2);   // major
                w.Write((ushort)act.Actions.Count);
                w.Write(new byte[10]);
                foreach (var action in act.Actions)
                {
                    w.Write((uint)action.Frames.Count);
                    foreach (var frame in action.Frames)
                    {
                        w.Write(new byte[32]);   // attack and fit ranges, unused
                        w.Write((uint)frame.Layers.Count);
                        foreach (var l in frame.Layers)
                        {
                            w.Write(l.OffsetX);
                            w.Write(l.OffsetY);
                            w.Write(l.SpriteIndex);
                            w.Write(l.Mirror ? 1 : 0);
                            w.Write(l.Tint.r);
                            w.Write(l.Tint.g);
                            w.Write(l.Tint.b);
                            w.Write(l.Tint.a);
                            w.Write(l.ScaleX);
                            w.Write(l.ScaleY);
                            w.Write(l.Rotation);
                            w.Write(l.SpriteType);
                            w.Write(0);   // width (derived, GRF fills it)
                            w.Write(0);   // height
                        }
                        w.Write(frame.EventId);
                        w.Write(frame.Anchors.Count);
                        foreach (var a in frame.Anchors)
                        {
                            w.Write(0);
                            w.Write(a.x);
                            w.Write(a.y);
                            w.Write(0);
                        }
                    }
                }
                w.Write(act.Events.Count);
                foreach (var e in act.Events)
                {
                    var name = new byte[40];
                    var text = Encoding.ASCII.GetBytes(e);
                    System.Array.Copy(text, name, System.Math.Min(text.Length, 39));
                    w.Write(name);
                }
                foreach (var action in act.Actions) w.Write(action.AnimationSpeed);
                return stream.ToArray();
            }
        }
    }
}
