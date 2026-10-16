using System.Text;

namespace BatoceraMovieScraper.Win;

/// <summary>把刮削器的日志流写进 TextBox（跨线程经 sink 派发到 UI 线程）。</summary>
internal sealed class TextBoxWriter : TextWriter
{
    readonly Action<string> _sink;
    public TextBoxWriter(Action<string> sink) => _sink = sink;

    public override Encoding Encoding => Encoding.UTF8;
    public override void Write(char value) => _sink(value.ToString());
    public override void Write(string? value) { if (!string.IsNullOrEmpty(value)) _sink(value); }
    public override void WriteLine(string? value) => _sink((value ?? "") + Environment.NewLine);
    public override void WriteLine() => _sink(Environment.NewLine);
}
