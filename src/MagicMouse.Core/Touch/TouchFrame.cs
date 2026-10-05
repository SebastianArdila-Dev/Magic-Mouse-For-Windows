namespace MagicMouse.Core.Touch;

public sealed record TouchContact(int Id, double X, double Y)
{
    public TouchContact Validate()
    {
        if (!double.IsFinite(X) || !double.IsFinite(Y) || X is < 0 or > 1 || Y is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(X), "Normalized contact coordinates must be in [0, 1].");
        return this;
    }
}

public sealed record TouchFrame(DateTimeOffset Timestamp, IReadOnlyList<TouchContact> Contacts)
{
    public TouchFrame Validate()
    {
        ArgumentNullException.ThrowIfNull(Contacts);
        if (Contacts.Select(contact => contact.Id).Distinct().Count() != Contacts.Count)
            throw new ArgumentException("Contact identifiers must be unique in a frame.", nameof(Contacts));
        foreach (var contact in Contacts) contact.Validate();
        return this;
    }
}
