using System.Runtime.InteropServices;
using System.Text;
using AppWatch.Core.Abstractions;

namespace AppWatch.Infrastructure.System;

public sealed class ShortcutService : IShortcutService
{
    public bool CreateStartMenuShortcut(string appName, string executablePath, string? description = null)
    {
        var startMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        return CreateShortcutInternal(startMenu, appName, executablePath, description);
    }

    public bool CreateDesktopShortcut(string appName, string executablePath, string? description = null)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        return CreateShortcutInternal(desktop, appName, executablePath, description);
    }

    public string? ResolveShortcutTarget(string shortcutPath)
    {
        try
        {
            if (!File.Exists(shortcutPath)) return null;
            var link = (IShellLinkW)new ShellLink();
            var persist = (IPersistFile)link;
            persist.Load(shortcutPath, 0);
            var sb = new StringBuilder(260);
            link.GetPath(sb, sb.Capacity, out _, 0);
            var target = sb.ToString();
            return File.Exists(target) ? target : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool CreateShortcutInternal(string directory, string appName, string targetPath, string? description)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(targetPath) || !File.Exists(targetPath))
                return false;

            Directory.CreateDirectory(directory);
            var safeName = SanitizeFileName(appName);
            if (string.IsNullOrWhiteSpace(safeName))
                safeName = Path.GetFileNameWithoutExtension(targetPath);

            var shortcutPath = Path.Combine(directory, $"{safeName}.lnk");
            var link = (IShellLinkW)new ShellLink();
            link.SetPath(targetPath);
            var workDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(workDir))
                link.SetWorkingDirectory(workDir);

            if (!string.IsNullOrWhiteSpace(description))
                link.SetDescription(description);

            var persist = (IPersistFile)link;
            persist.Save(shortcutPath, true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars).Trim();
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, int dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder ppszFileName);
    }
}
