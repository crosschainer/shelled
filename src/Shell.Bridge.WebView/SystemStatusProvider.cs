using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using Shell.Core;

namespace Shell.Bridge.WebView;

/// <summary>
/// Provides a lightweight snapshot of host system status (time, network, volume)
/// for consumption by the Web UI via the bridge.
/// </summary>
internal static class SystemStatusProvider
{
    private readonly record struct NetworkStatusPayload(string Kind, bool IsConnected, bool HasWifiAdapter, bool HasEthernetAdapter);
    private readonly record struct VolumeStatusPayload(int LevelPercent, bool IsMuted);

    public static string GetSystemStatusJson()
    {
        var now = DateTime.Now;
        var network = TryGetNetworkStatus();
        var volume = TryGetVolumeStatus();

        var payload = new
        {
            localTime = now.ToString("HH:mm"),
            localTimeIso = now.ToString("O"),
            network = new
            {
                kind = network.Kind,
                isConnected = network.IsConnected,
                hasWifiAdapter = network.HasWifiAdapter,
                hasEthernetAdapter = network.HasEthernetAdapter
            },
            volume = new
            {
                levelPercent = volume.LevelPercent,
                isMuted = volume.IsMuted
            }
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static NetworkStatusPayload TryGetNetworkStatus()
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new NetworkStatusPayload("unknown", false, false, false);
            }

            var allInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(networkInterface =>
                    networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    networkInterface.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .ToArray();

            var hasWifiAdapter = allInterfaces.Any(networkInterface =>
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);

            var hasEthernetAdapter = allInterfaces.Any(IsEthernetInterfaceType);

            var activeInterfaces = allInterfaces
                .Where(networkInterface => networkInterface.OperationalStatus == OperationalStatus.Up)
                .ToArray();

            if (activeInterfaces.Length == 0)
            {
                return new NetworkStatusPayload("offline", false, hasWifiAdapter, hasEthernetAdapter);
            }

            if (activeInterfaces.Any(networkInterface =>
                    networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
            {
                return new NetworkStatusPayload("wifi", true, hasWifiAdapter, hasEthernetAdapter);
            }

            if (activeInterfaces.Any(IsEthernetInterfaceType))
            {
                return new NetworkStatusPayload("ethernet", true, hasWifiAdapter, hasEthernetAdapter);
            }

            return new NetworkStatusPayload("other", true, hasWifiAdapter, hasEthernetAdapter);
        }
        catch
        {
            return new NetworkStatusPayload("unknown", false, false, false);
        }
    }

    private static VolumeStatusPayload TryGetVolumeStatus()
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new VolumeStatusPayload(0, false);
            }

            var levelPercent = 0;
            var isMuted = false;

            var success = TryWithEndpointVolume(endpointVolume =>
            {
                endpointVolume.GetMasterVolumeLevelScalar(out var levelScalar);
                endpointVolume.GetMute(out var volumeIsMuted);

                levelPercent = (int)Math.Round(levelScalar * 100);
                levelPercent = Math.Clamp(levelPercent, 0, 100);
                isMuted = volumeIsMuted;

                return true;
            });

            if (!success)
            {
                return new VolumeStatusPayload(0, false);
            }

            return new VolumeStatusPayload(levelPercent, isMuted);
        }
        catch
        {
            return new VolumeStatusPayload(0, false);
        }
    }

    public static bool SetSystemVolumePercent(int levelPercent)
    {
        var clamped = Math.Clamp(levelPercent, 0, 100);
        var scalar = clamped / 100f;

        return TryWithEndpointVolume(endpointVolume =>
        {
            var contextGuid = Guid.Empty;
            endpointVolume.SetMasterVolumeLevelScalar(scalar, ref contextGuid);
            return true;
        });
    }

    public static bool ToggleSystemMute()
    {
        var result = false;

        var success = TryWithEndpointVolume(endpointVolume =>
        {
            endpointVolume.GetMute(out var isMuted);
            var contextGuid = Guid.Empty;
            endpointVolume.SetMute(!isMuted, ref contextGuid);
            result = true;
            return true;
        });

        return success && result;
    }

    public static bool OpenNetworkSettings()
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }

            if (ShellConfiguration.DisableDangerousOperations)
            {
                return false;
            }

            if (TryStartProcess(new ProcessStartInfo("ms-settings:network-status")
            {
                UseShellExecute = true
            }))
            {
                return true;
            }

            if (TryStartProcess(new ProcessStartInfo("control.exe", "/name Microsoft.NetworkAndSharingCenter")
            {
                UseShellExecute = true
            }))
            {
                return true;
            }

            if (TryStartProcess(new ProcessStartInfo("ncpa.cpl")
            {
                UseShellExecute = true
            }))
            {
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public static bool PreferNetworkKind(string preferredKind)
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }

            if (ShellConfiguration.DisableDangerousOperations)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(preferredKind))
            {
                return false;
            }

            var normalizedKind = preferredKind.Trim().ToLowerInvariant();

            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(networkInterface =>
                    networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    networkInterface.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .ToArray();

            if (networkInterfaces.Length == 0)
            {
                return false;
            }

            var wifiInterfaces = networkInterfaces
                .Where(networkInterface => networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                .ToArray();

            var ethernetInterfaces = networkInterfaces
                .Where(IsEthernetInterfaceType)
                .ToArray();

            if (normalizedKind == "wifi")
            {
                if (wifiInterfaces.Length == 0)
                {
                    return false;
                }

                var success = true;

                foreach (var ethernetInterface in ethernetInterfaces)
                {
                    if (!TrySetInterfaceAdminState(ethernetInterface.Name, false))
                    {
                        success = false;
                    }
                }

                foreach (var wifiInterface in wifiInterfaces)
                {
                    if (!TrySetInterfaceAdminState(wifiInterface.Name, true))
                    {
                        success = false;
                    }
                }

                return success;
            }

            if (normalizedKind == "ethernet")
            {
                if (ethernetInterfaces.Length == 0)
                {
                    return false;
                }

                var success = true;

                foreach (var wifiInterface in wifiInterfaces)
                {
                    if (!TrySetInterfaceAdminState(wifiInterface.Name, false))
                    {
                        success = false;
                    }
                }

                foreach (var ethernetInterface in ethernetInterfaces)
                {
                    if (!TrySetInterfaceAdminState(ethernetInterface.Name, true))
                    {
                        success = false;
                    }
                }

                return success;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryWithEndpointVolume(Func<IAudioEndpointVolume, bool> operation)
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }

            var enumeratorClsid = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
            var enumeratorType = Type.GetTypeFromCLSID(enumeratorClsid, throwOnError: false);
            if (enumeratorType is null)
            {
                return false;
            }

            var enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(enumeratorType)!;
            enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var device);

            var endpointVolumeGuid = typeof(IAudioEndpointVolume).GUID;
            device.Activate(ref endpointVolumeGuid, 23, IntPtr.Zero, out var endpointVolume);

            var result = operation(endpointVolume);

            Marshal.ReleaseComObject(endpointVolume);
            Marshal.ReleaseComObject(device);
            Marshal.ReleaseComObject(enumerator);

            return result;
        }
        catch
        {
            return false;
        }
    }

    private static bool TrySetInterfaceAdminState(string interfaceName, bool enable)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(interfaceName))
            {
                return false;
            }

            var state = enable ? "enabled" : "disabled";

            var startInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"interface set interface name=\"{interfaceName}\" admin={state}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            if (!process.WaitForExit(5000))
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                }

                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryStartProcess(ProcessStartInfo startInfo)
    {
        try
        {
            using var process = Process.Start(startInfo);
            return process != null;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsEthernetInterfaceType(NetworkInterface networkInterface)
    {
        return networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
               networkInterface.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet ||
               networkInterface.NetworkInterfaceType == NetworkInterfaceType.FastEthernetFx ||
               networkInterface.NetworkInterfaceType == NetworkInterfaceType.FastEthernetT ||
               networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet3Megabit;
    }

    private enum EDataFlow
    {
        eRender,
        eCapture,
        eAll
    }

    private enum ERole
    {
        eConsole,
        eMultimedia,
        eCommunications
    }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int NotImpl1();

        [PreserveSig]
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppDevice);

        int NotImpl2();
        int NotImpl3();
        int NotImpl4();
        int NotImpl5();
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, out IAudioEndpointVolume ppInterface);

        int NotImpl1();
        int NotImpl2();
    }

    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr pNotify);
        int UnregisterControlChangeNotify(IntPtr pNotify);
        int GetChannelCount(out uint pnChannelCount);
        int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        int GetMasterVolumeLevel(out float pfLevelDB);

        [PreserveSig]
        int GetMasterVolumeLevelScalar(out float pfLevel);

        int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
        int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
        int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);

        [PreserveSig]
        int GetMute(out bool pbMute);

        int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
        int VolumeStepUp(ref Guid pguidEventContext);
        int VolumeStepDown(ref Guid pguidEventContext);
        int QueryHardwareSupport(out uint pdwHardwareSupportMask);
        int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
    }
}
