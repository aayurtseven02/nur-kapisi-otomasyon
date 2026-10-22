using System.Text.RegularExpressions;
namespace AnkaStudio;

public static class FilmMotionPolicy
{
    public const string Version="calm-continuous-film-v1";
    public static int Shots(double duration){SceneMotion.ShotCount(duration);return Math.Max(1,(int)Math.Ceiling(duration/7.5));}
    public static string Prompt(string motion)
    {
        var actions=Regex.Split(motion,@"(?<=[.!?])\s+").Where(s=>!Regex.IsMatch(s,@"\b(camera|zoom|dolly|pan|pans|panning|tracking|push.in|pull.back|close.up)\b",RegexOptions.IgnoreCase));
        return "A fixed tripod composition. The framing, camera distance and exposure stay constant throughout. "+string.Join(" ",actions)+" Restrained natural acting and gentle environmental movement. Preserve the same face, eyes, hands, two arms and clothing from the reference. Continue the current pose without restarting the action.";
    }
}
