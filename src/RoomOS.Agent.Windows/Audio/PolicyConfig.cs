using System.Runtime.InteropServices;

namespace RoomOS.Agent.Windows.Audio;

/// <summary>
/// Change le périphérique de sortie par défaut de Windows.
/// </summary>
/// <remarks>
/// <para>
/// Windows n'expose aucune API publique pour cela. La seule voie est l'interface COM
/// <c>IPolicyConfig</c>, non documentée par Microsoft mais stable depuis Windows 7 et
/// utilisée par tous les utilitaires de bascule audio. C'est le point du projet le
/// plus exposé à une rupture Windows (docs/06-agent-windows.md).
/// </para>
/// <para>
/// Deux variantes coexistent selon les versions. On tente la plus récente, puis celle
/// dite « Vista ». Seule <c>SetDefaultEndpoint</c> nous intéresse : les méthodes qui
/// la précèdent ne sont déclarées que pour occuper leur position dans la table
/// virtuelle, et ne doivent jamais être appelées.
/// </para>
/// </remarks>
public static class PolicyConfig
{
    public static bool TrySetDefaultOutput(string windowsDeviceId, out string? error)
    {
        foreach (var attempt in new Func<string, string?>[] { ViaModern, ViaVista })
        {
            error = attempt(windowsDeviceId);

            if (error is null)
            {
                return true;
            }
        }

        error = $"Aucune variante d'IPolicyConfig n'a accepté « {windowsDeviceId} ».";
        return false;
    }

    private static string? ViaModern(string deviceId) =>
        Invoke<CPolicyConfigClient, IPolicyConfig>(
            deviceId, (config, id, role) => config.SetDefaultEndpoint(id, role));

    private static string? ViaVista(string deviceId) =>
        Invoke<CPolicyConfigVistaClient, IPolicyConfigVista>(
            deviceId, (config, id, role) => config.SetDefaultEndpoint(id, role));

    /// <summary>Renvoie <c>null</c> en cas de succès, le message d'erreur sinon.</summary>
    private static string? Invoke<TClass, TInterface>(
        string deviceId, Func<TInterface, string, ERole, int> call)
        where TClass : new()
        where TInterface : class
    {
        object? instance = null;

        try
        {
            instance = new TClass();

            if (instance is not TInterface config)
            {
                return $"{typeof(TClass).Name} n'expose pas {typeof(TInterface).Name}.";
            }

            // Les trois rôles : sans quoi la bascule ne vaut que pour une partie des
            // applications, et Windows continue d'en router certaines vers l'ancienne
            // sortie.
            foreach (var role in new[] { ERole.Console, ERole.Multimedia, ERole.Communications })
            {
                var hr = call(config, deviceId, role);

                if (hr != 0)
                {
                    return $"{typeof(TInterface).Name}.SetDefaultEndpoint({role}) a renvoyé 0x{hr:X8}.";
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            return $"{typeof(TInterface).Name} : {ex.GetType().Name} — {ex.Message}";
        }
        finally
        {
            if (instance is not null && Marshal.IsComObject(instance))
            {
                Marshal.ReleaseComObject(instance);
            }
        }
    }

    private enum ERole
    {
        Console = 0,
        Multimedia = 1,
        Communications = 2,
    }

    [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    private class CPolicyConfigClient;

    [ComImport, Guid("294935CE-F637-4E7C-A41B-AB255460B862")]
    private class CPolicyConfigVistaClient;

    [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        // Emplacements de la table virtuelle. Ne pas appeler, ne pas réordonner.
        int GetMixFormat(string deviceName, out nint format);
        int GetDeviceFormat(string deviceName, bool isDefault, out nint format);
        int ResetDeviceFormat(string deviceName);
        int SetDeviceFormat(string deviceName, nint endpointFormat, nint mixFormat);
        int GetProcessingPeriod(string deviceName, bool isDefault, out long defaultPeriod, out long minimumPeriod);
        int SetProcessingPeriod(string deviceName, ref long period);
        int GetShareMode(string deviceName, out nint shareMode);
        int SetShareMode(string deviceName, nint shareMode);
        int GetPropertyValue(string deviceName, ref nint key, out nint value);
        int SetPropertyValue(string deviceName, ref nint key, ref nint value);

        int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);

        int SetEndpointVisibility(string deviceName, bool visible);
    }

    [ComImport, Guid("568b9108-44bf-40b4-9006-86afe5b5a620")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfigVista
    {
        // La variante Vista n'a pas ResetDeviceFormat : une position de moins.
        int GetMixFormat(string deviceName, out nint format);
        int GetDeviceFormat(string deviceName, bool isDefault, out nint format);
        int SetDeviceFormat(string deviceName, nint endpointFormat, nint mixFormat);
        int GetProcessingPeriod(string deviceName, bool isDefault, out long defaultPeriod, out long minimumPeriod);
        int SetProcessingPeriod(string deviceName, ref long period);
        int GetShareMode(string deviceName, out nint shareMode);
        int SetShareMode(string deviceName, nint shareMode);
        int GetPropertyValue(string deviceName, ref nint key, out nint value);
        int SetPropertyValue(string deviceName, ref nint key, ref nint value);

        int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);

        int SetEndpointVisibility(string deviceName, bool visible);
    }
}
