namespace NCalc.Handlers;

public class UpdateParameterArgs : EventArgs
{
    private readonly object? value;

    public UpdateParameterArgs(string name, Guid id, object? value) : this(name, id, null, value)
    {
    }

    public UpdateParameterArgs(string name, Guid id, int? index, object? value)
    {
        this.value = value;
        Id = id;
        Name = name;
        Index = index;
    }

    public Guid Id { get; }

    public string Name { get; }

    public int? Index { get; }

    public object? Value => value;

    public bool UpdateParameterLists { get; set; } = true;
}