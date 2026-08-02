using System.Collections.ObjectModel;
using System.Text;

namespace CsoKit.Application;

internal sealed class CsoOperationDetailsBuilder
{
    private readonly List<CsoOperationDetail> details = [];

    public void Blank() => details.Add(CsoOperationDetail.Blank());

    public void Section(string label) => details.Add(CsoOperationDetail.Section(label));

    public void Field(string label, string value) => details.Add(CsoOperationDetail.Field(label, value));

    public void Bullet(string value) => details.Add(CsoOperationDetail.Bullet(value));

    public void Text(string value) => details.Add(CsoOperationDetail.Text(value));

    public IReadOnlyList<CsoOperationDetail> Build()
    {
        return new ReadOnlyCollection<CsoOperationDetail>(details.ToArray());
    }
}

internal static class CsoOperationDetailFormatter
{
    public static string Format(IReadOnlyList<CsoOperationDetail> details)
    {
        if (details.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new();

        foreach (CsoOperationDetail detail in details)
        {
            switch (detail.Kind)
            {
                case CsoOperationDetailKind.Blank:
                    builder.AppendLine();
                    break;
                case CsoOperationDetailKind.Section:
                    builder.AppendLine($"{detail.Label}:");
                    break;
                case CsoOperationDetailKind.Field:
                    builder.AppendLine($"{detail.Label}: {detail.Value}");
                    break;
                case CsoOperationDetailKind.Bullet:
                    builder.AppendLine($"- {detail.Value}");
                    break;
                case CsoOperationDetailKind.Text:
                    builder.AppendLine(detail.Value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(detail.Kind), detail.Kind, "Unsupported operation detail kind.");
            }
        }

        return builder.ToString();
    }
}
