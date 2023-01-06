using System.Collections.Generic;
using Microsoft.AspNetCore.Components;

namespace SideloaderModpackUpdater.Shared
{
    public partial class STreeViewNode<TItem>
    {
        [Parameter] public List<TItem> Items { get; set; }

        [Parameter] public STreeView<TItem> STreeView { get; set; }


        void Expended(TItem item)
        {
            STreeView.ExpendAction(item);
            StateHasChanged();
        }
    }
}
