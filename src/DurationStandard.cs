namespace AnkaStudio;



public readonly record struct NarrationBudget(int Seconds, int Minimum, int Target, int Maximum);

public static class DurationStandard

{

    public const int MinimumWordsPerMinute = 120;

    public const int TargetWordsPerMinute = 130;

    public const int MaximumWordsPerMinute = 140;

    public static NarrationBudget Budget(int seconds) => new(seconds,

        (int)Math.Ceiling(seconds * MinimumWordsPerMinute / 60d),

        (int)Math.Round(seconds * TargetWordsPerMinute / 60d, MidpointRounding.AwayFromZero),

        (int)Math.Ceiling(seconds * MaximumWordsPerMinute / 60d));

    public static int Seconds(Episode episode, bool shortVideo) => shortVideo ? episode.ShortSeconds : episode.LongMinutes * 60;

    public static double Tolerance(double seconds) => Math.Max(3, seconds * .15);

    public static int SceneCount(int seconds) => Math.Clamp((int)Math.Ceiling(seconds / 30d), 1, 60);

    public static string Instruction(Episode episode) {

        string For(bool vertical) {

            var b = Budget(Seconds(episode, vertical));

            return $"{(vertical ? "Her Shorts" : "Normal video")}: {b.Seconds} saniye; en az {b.Minimum}, hedef {b.Target}, en fazla {b.Maximum} kelime. {SceneCount(b.Seconds)} sahneye böl; her sahne ortalama yaklaşık {b.Target / SceneCount(b.Seconds)} kelime; sahneler farklı uzunlukta olabilir, toplam bütçe zorunludur. Giriş, açıklama/gelişme ve sonuç bütünlük oluştursun.";

        }

        return (episode.IncludeLong ? For(false) + "\n" : "") + (episode.ShortCount > 0 ? For(true) : "") + "\nSüreyi doldurmak için tekrar veya ilgisiz bilgi ekleme. Başlık ve açıklama kelimelerini anlatım hesabına katma.";

    }

}

