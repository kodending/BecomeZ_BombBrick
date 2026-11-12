using System;
using System.IO;
using System.Text;

/// <summary>Stages a fresh profile and archives the entire old save tree before publishing it.</summary>
public sealed class PlayerProgressReset
{
    public string RootPath { get; }
    public string BackupDirectory => RootPath + ".backups";
    public string PendingPath => RootPath + ".reset";

    public PlayerProgressReset(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("A save directory is required.");
        RootPath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrEmpty(Path.GetDirectoryName(RootPath)))
            throw new ArgumentException("The save directory cannot be a filesystem root.");
    }

    public string StartNewGame(MonsterDatabase database, bool playTutorial = true)
    {
        if (database == null) throw new ArgumentNullException(nameof(database));
        RecoverPending();
        if (Exists(PendingPath)) throw new IOException("The previous reset marker cannot be cleared.");
        string token = Guid.NewGuid().ToString("N");
        string archive = Path.Combine(BackupDirectory, token);
        string fresh = Path.Combine(archive, "Fresh");
        string previous = Path.Combine(archive, "Previous");
        var staged = new PlayerProfileStore(fresh);
        PlayerProfileSave profile = staged.LoadOrCreate(database);
        new PlayerProfileStore(RootPath).RestoreMemorialChundongi(profile, database, true);
        if (profile.monsters.Count < PlayerProfileStore.ExpeditionRosterSize)
            throw new InvalidOperationException("Starter monsters are missing from the database.");
        if (!playTutorial)
        {
            profile.firstExpeditionGuideStep = profile.firstExpeditionCaptureGuideStep = 8;
            staged.Save(profile); // Put the choice in the fallback too, before publishing either file.
        }
        staged.Save(profile); // Both the new primary and its fallback belong to the new game.
        File.WriteAllText(Path.Combine(archive, "RESTORE.txt"),
            "BecomeZ: Wild Crew - " + DateTime.UtcNow.ToString("u") + "\n\n" +
            "Previous 폴더에 초기화 직전의 저장 기록 전체가 보관됩니다.\n" +
            "복구하려면 게임을 완전히 종료하세요. 현재 저장 폴더와 .reset/.reset.tmp 파일이 있으면 별도로 옮겨 보관하세요.\n" +
            "Previous 폴더 전체를 아래 저장 경로로 복사하세요. 기존 폴더에 파일을 섞어 덮어쓰지 마세요.\n" +
            "Fresh 폴더는 완료되지 않은 초기화의 준비 데이터이며 이전 진행 기록이 아닙니다.\n\n" +
            "Previous contains the complete save tree from before the reset.\n" +
            "To restore, fully close the game. Move the current save folder and any .reset/.reset.tmp files aside first.\n" +
            "Copy the entire Previous folder to the save path below; do not merge files into an existing save folder.\n" +
            "Fresh, if present, is unused new-game staging data, not your previous progress.\n\n" + RootPath,
            new UTF8Encoding(false));
        byte[] marker = Encoding.UTF8.GetBytes(token);
        using (var stream = new FileStream(PendingPath + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(marker, 0, marker.Length);
            stream.Flush(true);
        }
        File.Move(PendingPath + ".tmp", PendingPath);
        try
        {
            if (Exists(RootPath)) Directory.Move(RootPath, previous);
            Directory.Move(fresh, RootPath); // Commit: no old expedition/backup can leak into the new profile.
        }
        catch
        {
            RecoverPending();
            throw;
        }
        ClearMarker();
        return archive;
    }

    public void RecoverPending()
    {
        if (!Exists(PendingPath)) return;
        string token = File.ReadAllText(PendingPath);
        Guid parsed;
        if (!Guid.TryParseExact(token, "N", out parsed))
            throw new InvalidDataException("Invalid new-game reset marker. Save data was not replaced.");
        string archive = Path.Combine(BackupDirectory, token);
        if (!Exists(archive)) throw new InvalidDataException("The reset recovery folder is missing.");
        bool freshExists = Exists(Path.Combine(archive, "Fresh"));
        string previous = Path.Combine(archive, "Previous");
        if (freshExists && Exists(previous))
        {
            if (Exists(RootPath)) throw new InvalidDataException("Conflicting save folders during reset recovery.");
            Directory.Move(previous, RootPath); // Interrupted before commit: restore the whole old tree.
        }
        else if (!Exists(RootPath) && !freshExists)
            throw new InvalidDataException("No active or staged profile exists for reset recovery.");
        ClearMarker();
    }

    private void ClearMarker()
    {
        // The tree is already consistent. A locked marker can be retried on the next load.
        try { File.Delete(PendingPath); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static bool Exists(string path)
    {
        try { File.GetAttributes(path); return true; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }
}
