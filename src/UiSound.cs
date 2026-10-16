using System.Media;

namespace AnkaStudio;

public static class UiSound
{
    public static bool Enabled = true;
    public static double Volume = .25;
    static readonly object Gate = new();
    static SoundPlayer? player;
    static MemoryStream? stream;
    static double cachedVolume = -1;
    public static void Configure(Settings settings) {
        if (settings.UiClickSoundRevision < 1) {
            settings.UiSounds = true;
            settings.UiSoundVolume = Math.Max(.5, settings.UiSoundVolume);
            settings.UiClickSoundRevision = 1;
        }
        Enabled = settings.UiSounds;
        Volume = settings.UiSoundVolume;
    }
    public static byte[] Create(double volume)
    {
        const int rate = 44100, count = 1764;
        using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16); writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
        var random = new Random(17); volume = Math.Clamp(volume, 0, 1);
        for (int n = 0; n < count; n++) {
            var t = n / (double)rate; var envelope = Math.Min(1, n / 18.0) * Math.Exp(-t * 160) * Math.Min(1, (count - n) / 120.0);
            var tone = .48 * Math.Sin(2 * Math.PI * 1250 * t) + .32 * Math.Sin(2 * Math.PI * 2300 * t) + .2 * (random.NextDouble() * 2 - 1);
            writer.Write((short)(tone * envelope * volume * 20000));
        }
        return memory.ToArray();
    }
    public static void Click(bool force = false)
    {
        if ((!Enabled && !force) || Volume <= 0) return;
        lock (Gate) {
            try {
                if (player == null || cachedVolume != Volume) {
                    player?.Stop(); player?.Dispose(); stream?.Dispose();
                    stream = new MemoryStream(Create(Volume)); player = new SoundPlayer(stream); player.Load(); cachedVolume = Volume;
                }
                player.Play();
            } catch { try { SystemSounds.Beep.Play(); } catch { /* Audio feedback must never prevent a button action. */ } }
        }
    }
}
