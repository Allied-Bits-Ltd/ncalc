namespace NCalc.Handlers;

public class MatchStringArgs(string value, string pattern, bool caseInsensitive) : EventArgs
{
    public string Value { get; } = value;

    public string Pattern { get; } = pattern;

    public bool CaseInsensitive { get; } = caseInsensitive;

    public bool? Matches { get; set; }
}