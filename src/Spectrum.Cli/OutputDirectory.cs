using System.Text;
namespace Spectrum.Cli;
public static class OutputDirectory
{
    public static void Prepare(string path, bool overwrite)
    {
        if (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any() && !overwrite) throw new IOException("Output directory is nonempty; use --overwrite.");
        Directory.CreateDirectory(path);
    }
    public static void Write(string path, string name, string content)
    {
        string target = Path.Combine(path, name), temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, content.Replace("\r\n", "\n") + (content.EndsWith('\n') ? "" : "\n"), new UTF8Encoding(false)); File.Move(temporary, target, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static void RemoveGenerated(string path, string name)
    {
        if (name is not "snapshots.svg" and not "matrix.json" and not "eigenvectors.csv") throw new ArgumentException("Not an optional generated file.");
        string target = Path.Combine(path, name); if (File.Exists(target)) File.Delete(target);
    }
}
