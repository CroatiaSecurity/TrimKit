using System.IO;
using System.Xml.Linq;
using TrimKit.Models;

namespace TrimKit.Services;

public class UnattendService : IUnattendService
{
    private readonly ILogService _logService;
    private static readonly XNamespace Ns = "urn:schemas-microsoft-com:unattend";
    private static readonly XNamespace Wcm = "http://schemas.microsoft.com/WMIConfig/2002/State";

    public UnattendService(ILogService logService)
    {
        _logService = logService;
    }

    public string GenerateUnattendXml(UnattendConfig config)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            CreateUnattendElement(config)
        );

        using var sw = new StringWriter();
        doc.Save(sw);
        return sw.ToString();
    }

    public async Task SaveUnattendAsync(UnattendConfig config, string outputPath)
    {
        var xml = GenerateUnattendXml(config);
        await File.WriteAllTextAsync(outputPath, xml);
        _logService.Log(LogLevel.Success, $"Saved autounattend.xml: {outputPath}");
    }

    public async Task InjectIntoImageAsync(string mountPath, UnattendConfig config)
    {
        var pantherDir = Path.Combine(mountPath, @"Windows\Panther");
        Directory.CreateDirectory(pantherDir);

        var outputPath = Path.Combine(pantherDir, "unattend.xml");
        await SaveUnattendAsync(config, outputPath);
        _logService.Log(LogLevel.Success, "Injected unattend.xml into image (Windows\\Panther)");
    }

    private XElement CreateUnattendElement(UnattendConfig config)
    {
        var unattend = new XElement(Ns + "unattend",
            new XAttribute(XNamespace.Xmlns + "wcm", Wcm));

        // windowsPE pass
        unattend.Add(CreateWindowsPEPass(config));

        // specialize pass
        unattend.Add(CreateSpecializePass(config));

        // oobeSystem pass
        unattend.Add(CreateOobePass(config));

        return unattend;
    }

    private XElement CreateWindowsPEPass(UnattendConfig config)
    {
        var settings = new XElement(Ns + "settings",
            new XAttribute("pass", "windowsPE"));

        // International-Core-WinPE
        var intlComp = CreateComponent("Microsoft-Windows-International-Core-WinPE");
        intlComp.Add(
            new XElement(Ns + "InputLocale", config.InputLocale),
            new XElement(Ns + "SystemLocale", config.Language),
            new XElement(Ns + "UILanguage", config.Language),
            new XElement(Ns + "UILanguageFallback", "en-US"),
            new XElement(Ns + "UserLocale", config.Language)
        );
        settings.Add(intlComp);

        // Setup component
        var setupComp = CreateComponent("Microsoft-Windows-Setup");

        // Diagnostics
        setupComp.Add(new XElement(Ns + "Diagnostics",
            new XElement(Ns + "OptIn", "false")));

        // DynamicUpdate
        setupComp.Add(new XElement(Ns + "DynamicUpdate",
            new XElement(Ns + "Enable", "false"),
            new XElement(Ns + "WillShowUI", "Never")));

        // ImageInstall
        var imageInstall = new XElement(Ns + "ImageInstall",
            new XElement(Ns + "OSImage",
                new XElement(Ns + "WillShowUI", "OnError"),
                new XElement(Ns + "InstallFrom",
                    new XElement(Ns + "MetaData",
                        new XAttribute(Wcm + "action", "add"),
                        new XElement(Ns + "Key", "/IMAGE/INDEX"),
                        new XElement(Ns + "Value", config.ImageIndex.ToString())
                    )
                )
            ));
        setupComp.Add(imageInstall);

        // UserData
        var userData = new XElement(Ns + "UserData",
            new XElement(Ns + "AcceptEula", "true"));
        if (!string.IsNullOrEmpty(config.ProductKey))
        {
            userData.Add(new XElement(Ns + "ProductKey",
                new XElement(Ns + "Key", config.ProductKey)));
        }
        setupComp.Add(userData);

        // TPM/SecureBoot/RAM bypass (Win11)
        if (config.BypassTpmCheck || config.BypassSecureBootCheck || config.BypassRamCheck)
        {
            var runSync = new XElement(Ns + "RunSynchronous");
            int order = 1;

            if (config.BypassTpmCheck)
            {
                runSync.Add(CreateRunSyncCommand(order++,
                    "reg add HKLM\\SYSTEM\\Setup\\LabConfig /v BypassTPMCheck /t REG_DWORD /d 1 /f"));
            }
            if (config.BypassSecureBootCheck)
            {
                runSync.Add(CreateRunSyncCommand(order++,
                    "reg add HKLM\\SYSTEM\\Setup\\LabConfig /v BypassSecureBootCheck /t REG_DWORD /d 1 /f"));
            }
            if (config.BypassRamCheck)
            {
                runSync.Add(CreateRunSyncCommand(order++,
                    "reg add HKLM\\SYSTEM\\Setup\\LabConfig /v BypassRAMCheck /t REG_DWORD /d 1 /f"));
            }

            setupComp.Add(runSync);
        }

        settings.Add(setupComp);
        return settings;
    }

    private XElement CreateSpecializePass(UnattendConfig config)
    {
        var settings = new XElement(Ns + "settings",
            new XAttribute("pass", "specialize"));

        // Shell-Setup (ComputerName)
        var shellComp = CreateComponent("Microsoft-Windows-Shell-Setup");
        shellComp.Add(new XElement(Ns + "ComputerName", config.ComputerName));

        if (config.DisableDefender)
        {
            // Run command to disable Defender in specialize
            var deployComp = CreateComponent("Microsoft-Windows-Deployment");
            var runSync = new XElement(Ns + "RunSynchronous");
            runSync.Add(CreateRunSyncCommand(1,
                "reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows Defender\" /v DisableAntiSpyware /t REG_DWORD /d 1 /f"));
            deployComp.Add(runSync);
            settings.Add(deployComp);
        }

        if (config.EnableAdminAccount)
        {
            var deployComp = settings.Element(Ns + "component") != null
                ? settings.Elements(Ns + "component").FirstOrDefault(c => c.Attribute("name")?.Value == "Microsoft-Windows-Deployment")
                : null;

            if (deployComp == null)
            {
                deployComp = CreateComponent("Microsoft-Windows-Deployment");
                settings.Add(deployComp);
            }

            var runSync = deployComp.Element(Ns + "RunSynchronous") ?? new XElement(Ns + "RunSynchronous");
            if (deployComp.Element(Ns + "RunSynchronous") == null)
                deployComp.Add(runSync);

            var existingCount = runSync.Elements().Count();
            runSync.Add(CreateRunSyncCommand((int)existingCount + 1, "net user Administrator /active:Yes"));
        }

        settings.Add(shellComp);
        return settings;
    }

    private XElement CreateOobePass(UnattendConfig config)
    {
        var settings = new XElement(Ns + "settings",
            new XAttribute("pass", "oobeSystem"));

        // International-Core
        var intlComp = CreateComponent("Microsoft-Windows-International-Core");
        intlComp.Add(
            new XElement(Ns + "InputLocale", config.InputLocale),
            new XElement(Ns + "SystemLocale", config.Language),
            new XElement(Ns + "UILanguage", config.Language),
            new XElement(Ns + "UserLocale", config.Language)
        );
        settings.Add(intlComp);

        // Shell-Setup
        var shellComp = CreateComponent("Microsoft-Windows-Shell-Setup");
        shellComp.Add(new XElement(Ns + "TimeZone", config.TimeZone));

        // OOBE
        var oobe = new XElement(Ns + "OOBE");
        if (config.HideEula)
            oobe.Add(new XElement(Ns + "HideEULAPage", "true"));
        if (config.SkipOobe)
        {
            oobe.Add(new XElement(Ns + "HideLocalAccountScreen", "true"));
            oobe.Add(new XElement(Ns + "HideOnlineAccountScreens", "true"));
            oobe.Add(new XElement(Ns + "SkipMachineOOBE", "true"));
            oobe.Add(new XElement(Ns + "SkipUserOOBE", "true"));
        }
        if (config.HideWirelessSetup)
            oobe.Add(new XElement(Ns + "HideWirelessSetupInOOBE", "true"));
        if (config.DisableTelemetry)
            oobe.Add(new XElement(Ns + "ProtectYourPC", "3"));

        shellComp.Add(oobe);

        // User accounts
        var accounts = new XElement(Ns + "UserAccounts");
        var localAccounts = new XElement(Ns + "LocalAccounts");
        var localAccount = new XElement(Ns + "LocalAccount",
            new XAttribute(Wcm + "action", "add"),
            new XElement(Ns + "DisplayName", config.UserName),
            new XElement(Ns + "Group", "Administrators"),
            new XElement(Ns + "Name", config.UserName));

        if (!string.IsNullOrEmpty(config.Password))
        {
            localAccount.Add(new XElement(Ns + "Password",
                new XElement(Ns + "PlainText", "true"),
                new XElement(Ns + "Value", config.Password)));
        }
        else
        {
            localAccount.Add(new XElement(Ns + "Password",
                new XElement(Ns + "PlainText", "true"),
                new XElement(Ns + "Value", "")));
        }

        localAccounts.Add(localAccount);
        accounts.Add(localAccounts);
        shellComp.Add(accounts);

        // AutoLogon
        if (config.AutoLogon)
        {
            shellComp.Add(new XElement(Ns + "AutoLogon",
                new XElement(Ns + "Enabled", "true"),
                new XElement(Ns + "LogonCount", "1"),
                new XElement(Ns + "Username", config.UserName),
                new XElement(Ns + "Password",
                    new XElement(Ns + "PlainText", "true"),
                    new XElement(Ns + "Value", config.Password ?? ""))));
        }

        // FirstLogonCommands
        if (config.FirstLogonCommands.Count > 0 || config.DisableHibernation || config.DisableUac)
        {
            var firstLogon = new XElement(Ns + "FirstLogonCommands");
            int order = 1;

            if (config.DisableHibernation)
            {
                firstLogon.Add(CreateSyncCommand(order++, "powercfg /h off", "Disable hibernation"));
            }
            if (config.DisableUac)
            {
                firstLogon.Add(CreateSyncCommand(order++,
                    "reg add HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System /v EnableLUA /t REG_DWORD /d 0 /f",
                    "Disable UAC"));
            }

            foreach (var cmd in config.FirstLogonCommands)
            {
                firstLogon.Add(CreateSyncCommand(order++, cmd, $"Custom command {order}"));
            }

            shellComp.Add(firstLogon);
        }

        settings.Add(shellComp);
        return settings;
    }

    private XElement CreateComponent(string name)
    {
        return new XElement(Ns + "component",
            new XAttribute("name", name),
            new XAttribute("processorArchitecture", "amd64"),
            new XAttribute("publicKeyToken", "31bf3856ad364e35"),
            new XAttribute("language", "neutral"),
            new XAttribute("versionScope", "nonSxS"),
            new XAttribute(XNamespace.Xmlns + "wcm", Wcm));
    }

    private XElement CreateRunSyncCommand(int order, string path)
    {
        return new XElement(Ns + "RunSynchronousCommand",
            new XAttribute(Wcm + "action", "add"),
            new XElement(Ns + "Order", order.ToString()),
            new XElement(Ns + "Path", path),
            new XElement(Ns + "WillReboot", "Never"));
    }

    private XElement CreateSyncCommand(int order, string commandLine, string description)
    {
        return new XElement(Ns + "SynchronousCommand",
            new XAttribute(Wcm + "action", "add"),
            new XElement(Ns + "Order", order.ToString()),
            new XElement(Ns + "CommandLine", commandLine),
            new XElement(Ns + "Description", description),
            new XElement(Ns + "RequiresUserInput", "false"));
    }
}
