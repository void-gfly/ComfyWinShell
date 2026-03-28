using System.ComponentModel;
using WpfDesktop.Models;

namespace WpfDesktop.ViewModels;

public static class WorkflowSortingHelper
{
    public const string DefaultProperty = nameof(WorkflowInfo.Name);
    public const ListSortDirection DefaultDirection = ListSortDirection.Ascending;

    public static IReadOnlyList<WorkflowInfo> Sort(
        IEnumerable<WorkflowInfo> workflows,
        string propertyName,
        ListSortDirection direction)
    {
        ArgumentNullException.ThrowIfNull(workflows);

        return propertyName switch
        {
            nameof(WorkflowInfo.Name) => direction == ListSortDirection.Ascending
                ? workflows.OrderBy(workflow => workflow.Name, StringComparer.OrdinalIgnoreCase).ToList()
                : workflows.OrderByDescending(workflow => workflow.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            nameof(WorkflowInfo.SizeBytes) => direction == ListSortDirection.Ascending
                ? workflows.OrderBy(workflow => workflow.SizeBytes).ToList()
                : workflows.OrderByDescending(workflow => workflow.SizeBytes).ToList(),
            nameof(WorkflowInfo.LastModified) => direction == ListSortDirection.Ascending
                ? workflows.OrderBy(workflow => workflow.LastModified).ToList()
                : workflows.OrderByDescending(workflow => workflow.LastModified).ToList(),
            _ => throw new ArgumentOutOfRangeException(nameof(propertyName), propertyName, "Unsupported workflow sort property.")
        };
    }

    public static ListSortDirection GetNextDirection(
        string nextProperty,
        string? currentProperty,
        ListSortDirection currentDirection)
    {
        return string.Equals(nextProperty, currentProperty, StringComparison.Ordinal)
            ? currentDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending
            : ListSortDirection.Ascending;
    }
}
