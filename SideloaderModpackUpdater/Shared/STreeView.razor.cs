using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;

namespace SideloaderModpackUpdater.Shared
{
    public partial class STreeView<TItem> :ComponentBase
    {
        [Parameter] public List<TItem> Items { get; set; }

        [Parameter] public RenderFragment<TItem> Content { get; set; }

        [Parameter] public Func<TItem, List<TItem>> Children { get; set; }

        [Parameter] public Func<TItem,bool> Selected { get; set; }

        [Parameter] public Func<TItem, bool> Expended { get; set; }

        [Parameter] public Action<TItem> SelectAction { get; set; }

        [Parameter] public Action<TItem> ExpendAction { get; set; }

    }
}
