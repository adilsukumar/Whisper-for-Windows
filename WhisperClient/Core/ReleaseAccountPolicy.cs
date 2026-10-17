using System.Reflection;
namespace WhisperClient.Core;
public static class ReleaseAccountPolicy
{
    public static Uri? Origin
    {
        get
        {
#if WHISPER_PAID_RELEASE
            var value = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
                .SingleOrDefault(item => item.Key == "WhisperAccountsOrigin")?.Value;
            if (!Uri.TryCreate(value, UriKind.Absolute, out var origin) || origin.Scheme != "https"
                || origin.AbsolutePath != "/" || origin.UserInfo != "" || origin.Query != "" || origin.Fragment != "" || origin.IsLoopback)
                throw new InvalidOperationException("Paid release account service is not configured.");
            return origin;
#else
            return null; // Explicit developer build, never a paid release.
#endif
        }
    }
}
