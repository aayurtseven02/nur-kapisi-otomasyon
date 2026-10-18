using System.Net;

namespace AnkaStudio;



public sealed class TransferContent(Stream source, Action<long, long> report) : HttpContent

{

    protected override bool TryComputeLength(out long length) { length = source.Length; return true; }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Copy(stream, CancellationToken.None);

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken ct) => Copy(stream, ct);

    async Task Copy(Stream output, CancellationToken ct) {

        byte[] buffer = new byte[81920]; long sent = 0; int count;

        while ((count = await source.ReadAsync(buffer, ct)) > 0) { await output.WriteAsync(buffer.AsMemory(0, count), ct); sent += count; report(sent, source.Length); }

    }

}

public sealed class GreenTransferBar : Control

{

    public int Value;

    public GreenTransferBar() { DoubleBuffered = true; Height = 28; BackColor = Theme.Elevated; }

    protected override void OnPaint(PaintEventArgs e) {

        base.OnPaint(e); using var brush = new SolidBrush(Theme.Teal);

        e.Graphics.FillRectangle(brush, 0, 0, Width * Math.Clamp(Value, 0, 100) / 100, Height);

        TextRenderer.DrawText(e.Graphics, Value + "%", Font, ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

    }

}

