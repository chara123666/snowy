namespace Snowy.Core;

public enum Strength { VeryWeak, Weak, Fair, Strong }

public readonly record struct StrengthReport(double Bits, Strength Level);

public static class Entropy
{
    private static readonly string[] CommonWords =
    {
        "password", "passwort", "qwerty", "123456", "letmein", "welcome", "admin", "iloveyou",
        "monkey", "dragon", "snowy", "snow", "secret", "пароль", "йцукен"
    };

    public static double PatternBits(IReadOnlyList<int> nodes)
    {
        int n = nodes.Count;
        if (n < 1 || n > 9) return 0;
        double ordered = 1;
        for (int i = 0; i < n; i++) ordered *= 9 - i;
        return Math.Log2(ordered);
    }

    public static double PasswordBits(string password)
    {
        if (string.IsNullOrEmpty(password)) return 0;

        bool lower = false, upper = false, digit = false, symbol = false, other = false;
        foreach (char c in password)
        {
            if (c is >= 'a' and <= 'z') lower = true;
            else if (c is >= 'A' and <= 'Z') upper = true;
            else if (c is >= '0' and <= '9') digit = true;
            else if (c < 128) symbol = true;
            else other = true;
        }
        int pool = (lower ? 26 : 0) + (upper ? 26 : 0) + (digit ? 10 : 0) + (symbol ? 33 : 0) + (other ? 100 : 0);
        double perChar = Math.Log2(Math.Max(pool, 2));

        double bits = 0;
        var seen = new HashSet<char>();
        for (int i = 0; i < password.Length; i++)
        {
            bool repeat = !seen.Add(password[i]);
            bool run = i >= 2
                && Math.Abs(password[i] - password[i - 1]) == 1
                && password[i] - password[i - 1] == password[i - 1] - password[i - 2];
            bits += repeat || run ? 1.0 : perChar;
        }

        string lowered = password.ToLowerInvariant();
        foreach (var word in CommonWords)
            if (lowered.Contains(word))
                bits = Math.Max(0, bits - word.Length * perChar) + 10;

        return bits;
    }

    public static Strength LevelFor(double bits)
        => bits < 28 ? Strength.VeryWeak : bits < 36 ? Strength.Weak : bits < 60 ? Strength.Fair : Strength.Strong;

    public static StrengthReport AssessPassword(string password)
    {
        double bits = PasswordBits(password);
        return new StrengthReport(bits, LevelFor(bits));
    }

    public static StrengthReport AssessPattern(IReadOnlyList<int> nodes)
    {
        double bits = PatternBits(nodes);
        return new StrengthReport(bits, LevelFor(bits));
    }
}

public enum PolicyIssue
{
    WrongFactorSet,
    PatternInvalid,
    PatternTooShort,
    PatternTrivial,
    PasswordTooShort,
    AnswerMissing
}

public sealed class PolicyViolationException : ArgumentException
{
    public IReadOnlyList<PolicyIssue> Issues { get; }

    public PolicyViolationException(IReadOnlyList<PolicyIssue> issues)
        : base("secrets don't meet this protection level: " + string.Join(", ", issues))
    {
        Issues = issues;
    }
}

public static class ProtectedPolicy
{
    public const int MinPatternNodes = 8;

    private static readonly HashSet<string> TrivialPatterns = BuildTrivial();

    public static Factors MaskOf(ProtectedSecrets s)
    {
        var mask = Factors.None;
        if (!string.IsNullOrEmpty(s.Pattern)) mask |= Factors.Pattern;
        if (!string.IsNullOrEmpty(s.Password)) mask |= Factors.Password;
        if (!string.IsNullOrEmpty(s.KeyfilePath)) mask |= Factors.Keyfile;
        if (!string.IsNullOrWhiteSpace(s.Answer)) mask |= Factors.Question;
        return mask;
    }

    public static int MinPasswordLength(ProtectedTier tier) => tier == ProtectedTier.A ? 12 : 16;

    public static List<PolicyIssue> Check(ProtectedRequest r)
    {
        var issues = new List<PolicyIssue>();
        var mask = MaskOf(r);

        bool factorsOk = r.Tier switch
        {
            ProtectedTier.Puzzle => mask is Factors.Pattern or Factors.Question,
            ProtectedTier.A => mask == (Factors.Pattern | Factors.Password),
            ProtectedTier.B => mask == (Factors.Password | Factors.Keyfile),
            ProtectedTier.C => mask == (Factors.Pattern | Factors.Password | Factors.Keyfile),
            _ => false
        };
        if (!factorsOk) { issues.Add(PolicyIssue.WrongFactorSet); return issues; }

        if (mask.HasFlag(Factors.Pattern))
        {
            if (!TryParsePattern(r.Pattern!, out var nodes)) issues.Add(PolicyIssue.PatternInvalid);
            else if (r.Tier is ProtectedTier.A or ProtectedTier.C)
            {
                if (nodes.Count < MinPatternNodes) issues.Add(PolicyIssue.PatternTooShort);
                else if (IsTrivialPattern(nodes)) issues.Add(PolicyIssue.PatternTrivial);
            }
        }

        if (mask.HasFlag(Factors.Password) && r.Password!.Length < MinPasswordLength(r.Tier))
            issues.Add(PolicyIssue.PasswordTooShort);

        return issues;
    }

    public static bool IsTrivialPattern(IReadOnlyList<int> nodes)
        => nodes.Count >= MinPatternNodes && TrivialPatterns.Contains(PatternCodec.Encode(nodes));

    private static bool TryParsePattern(string pattern, out List<int> nodes)
    {
        nodes = new List<int>();
        foreach (var part in pattern.Split('-', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(part, out int n) || n < 0 || n > 8 || nodes.Contains(n)) return false;
            nodes.Add(n);
        }
        return nodes.Count > 0;
    }

    private static HashSet<string> BuildTrivial()
    {
        int[][] bases =
        {
            new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 },
            new[] { 0, 1, 2, 5, 4, 3, 6, 7, 8 },
            new[] { 0, 1, 2, 5, 8, 7, 6, 3, 4 }
        };

        static int Rotate(int n) => (n % 3) * 3 + (2 - n / 3);
        static int Reflect(int n) => (n / 3) * 3 + (2 - n % 3);

        var set = new HashSet<string>();
        void Add(int[] path)
        {
            set.Add(PatternCodec.Encode(path));
            set.Add(PatternCodec.Encode(path.Take(MinPatternNodes)));
        }

        foreach (var b in bases)
        {
            var cur = b;
            for (int mirror = 0; mirror < 2; mirror++)
            {
                for (int rot = 0; rot < 4; rot++)
                {
                    Add(cur);
                    Add(cur.Reverse().ToArray());
                    cur = cur.Select(Rotate).ToArray();
                }
                cur = cur.Select(Reflect).ToArray();
            }
        }
        return set;
    }
}
