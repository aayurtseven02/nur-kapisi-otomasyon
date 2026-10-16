namespace AnkaStudio;

public static class WarningResolution
{
    public static void Resolve(Episode episode, string warning, string note)
    {
        if (episode.Plan == null || !episode.Plan.Warnings.Contains(warning)) throw new InvalidOperationException("Bu uyarı artık pakette bulunmuyor.");
        if (note.Trim().Length < 10) throw new InvalidOperationException("En az 10 karakterlik inceleme ve çözüm notu gerekli.");
        episode.Plan.Warnings.Remove(warning); episode.Approved = false; episode.ApprovalHash = "";
        episode.Log("Uyarı incelendi: " + warning + " | Çözüm notu: " + note.Trim());
    }
}
