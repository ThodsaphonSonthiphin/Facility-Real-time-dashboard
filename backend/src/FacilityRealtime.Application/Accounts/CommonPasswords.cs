namespace FacilityRealtime.Application.Accounts;

/// <summary>
/// facility-0089: passwords too common to allow, compared ignoring letter case. common-passwords.txt is every entry of
/// 15–100 characters in SecLists' xato-net-10-million-passwords-1000000.txt (MIT License, commit c205c36; notice in
/// LICENSE-seclists), lowercased, sorted and without duplicates, one per line; shorter entries are already refused by
/// the length rule. Read once, on the first check. It stays on the server: the browser checks only the length
/// (facility-0062).
/// </summary>
internal static class CommonPasswords
{
    private const string ResourceName = "FacilityRealtime.Application.Accounts.common-passwords.txt";

    private static readonly Lazy<HashSet<string>> List = new(Read);

    public static int Count => List.Value.Count;

    public static bool Contains(string password) => List.Value.Contains(password.ToLowerInvariant());

    private static HashSet<string> Read()
    {
        using var stream = typeof(CommonPasswords).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"{ResourceName} is not embedded in the Application assembly");
        using var reader = new StreamReader(stream);
        var list = new HashSet<string>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0)
            {
                list.Add(line);
            }
        }

        return list;
    }
}
