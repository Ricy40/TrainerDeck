using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Win32;
using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Core.Widget;

namespace TrainerDeck.Install;

// Installs the Xbox Game Bar widget package that build.ps1 embeds in the setup exe. The package is signed with a
// self-signed certificate, so that certificate is added to the PC's Trusted People store first (which trusts only
// packages signed with it, not anything it might issue).
public static class WidgetInstaller
{
    private const string ResourcePrefix = "Widget/";
    private const string ProtocolKey = @"Software\Classes\trainerdeck";

    public static bool Available => Resources().Any(n => n.EndsWith(".msix", StringComparison.OrdinalIgnoreCase));

    public static void Install(string userSid)
    {
        string dir = Path.Combine(Path.GetTempPath(), "TrainerDeck-widget-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var files = Resources().ToDictionary(n => n, n => Extract(n, dir));
            string package = files.First(f => f.Key.EndsWith(".msix", StringComparison.OrdinalIgnoreCase) && !f.Key.Contains("Dependency.")).Value;
            string certificate = files.First(f => f.Key.EndsWith(".cer", StringComparison.OrdinalIgnoreCase)).Value;
            string[] dependencies = files.Where(f => f.Key.Contains("Dependency.")).Select(f => f.Value).ToArray();

            TrustCertificate(certificate);

            bool sameUser = string.Equals(userSid, Installer.CurrentUserSid(), StringComparison.OrdinalIgnoreCase);
            string deps = dependencies.Length == 0 ? "" : " -DependencyPath " + string.Join(",", dependencies.Select(Quote));
            string script = sameUser
                ? $"$p = {Quote(package)}; " +
                  $"try {{ Add-AppxPackage -Path $p{deps} -ForceApplicationShutdown -ForceUpdateFromAnyVersion -ErrorAction Stop }} " +
                  $"catch {{ Get-AppxPackage -Name '{PackageIdentity.WidgetName}' | Remove-AppxPackage; Add-AppxPackage -Path $p{deps} -ErrorAction Stop }}"
                // Setup was elevated with another account: provision it so it installs for every user, including the right one.
                : $"Add-AppxProvisionedPackage -Online -PackagePath {Quote(package)}{(dependencies.Length == 0 ? "" : " -DependencyPackagePath " + string.Join(",", dependencies.Select(Quote)))} -SkipLicense -ErrorAction Stop | Out-Null";
            RunPowerShell(script, "install the Game Bar widget");
            Log.Info($"Game Bar widget installed ({(sameUser ? "current user" : "provisioned for all users")}).");
        }
        finally
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch (IOException)
            {
            }
        }
    }

    public static void Uninstall()
    {
        try
        {
            RunPowerShell(
                $"Get-AppxPackage -AllUsers -Name '{PackageIdentity.WidgetName}' | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue; " +
                $"Get-AppxProvisionedPackage -Online | Where-Object DisplayName -eq '{PackageIdentity.WidgetName}' | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Out-Null",
                "remove the Game Bar widget");
        }
        catch (InvalidOperationException ex)
        {
            Log.Warn(ex.Message);
        }

        using var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadWrite);
        foreach (X509Certificate2 cert in store.Certificates.Find(X509FindType.FindBySubjectDistinguishedName, PackageIdentity.Publisher, false))
        {
            store.Remove(cert);
        }
    }

    // trainerdeck: links let the widget open Trainer Deck when it isn't running.
    public static void RegisterProtocol(string exe)
    {
        using RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).CreateSubKey(ProtocolKey);
        key.SetValue("", "URL:Trainer Deck");
        key.SetValue("URL Protocol", "");
        using RegistryKey icon = key.CreateSubKey("DefaultIcon");
        icon.SetValue("", $"\"{exe}\",0");
        using RegistryKey command = key.CreateSubKey(@"shell\open\command");
        command.SetValue("", $"\"{exe}\" \"%1\"");
    }

    public static void UnregisterProtocol() =>
        RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).DeleteSubKeyTree(ProtocolKey, throwOnMissingSubKey: false);

    private static void TrustCertificate(string path)
    {
        using var cert = X509CertificateLoader.LoadCertificateFromFile(path);
        using var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadWrite);
        foreach (X509Certificate2 old in store.Certificates.Find(X509FindType.FindBySubjectDistinguishedName, PackageIdentity.Publisher, false))
        {
            if (old.Thumbprint != cert.Thumbprint)
            {
                store.Remove(old);
            }
        }

        store.Add(cert);
    }

    private static IEnumerable<string> Resources() =>
        Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal));

    private static string Extract(string resource, string dir)
    {
        string path = Path.Combine(dir, resource[ResourcePrefix.Length..]);
        using Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)!;
        using FileStream target = File.Create(path);
        source.CopyTo(target);
        return path;
    }

    private static string Quote(string path) => "'" + path.Replace("'", "''") + "'";

    private static void RunPowerShell(string script, string what)
    {
        var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        using Process p = Process.Start(psi) ?? throw new InvalidOperationException($"Couldn't start PowerShell to {what}.");
        string error = p.StandardError.ReadToEnd();
        p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0)
        {
            throw new InvalidOperationException($"Couldn't {what}: {error.Trim()}");
        }
    }
}
