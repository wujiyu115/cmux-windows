using System.Windows;
using System.Windows.Controls;
using Cmux.Core.Models;
using Cmux.ViewModels;

namespace Cmux.Controls;

public class SidebarTemplateSelector : DataTemplateSelector
{
    public DataTemplate? WorkspaceTemplate { get; set; }
    public DataTemplate? GroupTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        => item switch
        {
            WorkspaceViewModel => WorkspaceTemplate,
            WorkspaceGroup => GroupTemplate,
            _ => base.SelectTemplate(item, container),
        };
}
