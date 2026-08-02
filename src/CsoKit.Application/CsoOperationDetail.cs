namespace CsoKit.Application;

public enum CsoOperationDetailKind
{
    Blank,
    Section,
    Field,
    Bullet,
    Text,
}

public sealed record CsoOperationDetail(
    CsoOperationDetailKind Kind,
    string? Label = null,
    string? Value = null)
{
    public static CsoOperationDetail Blank() => new(CsoOperationDetailKind.Blank);

    public static CsoOperationDetail Section(string label) =>
        new(CsoOperationDetailKind.Section, label);

    public static CsoOperationDetail Field(string label, string value) =>
        new(CsoOperationDetailKind.Field, label, value);

    public static CsoOperationDetail Bullet(string value) =>
        new(CsoOperationDetailKind.Bullet, Value: value);

    public static CsoOperationDetail Text(string value) =>
        new(CsoOperationDetailKind.Text, Value: value);
}
