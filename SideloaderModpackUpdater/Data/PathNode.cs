using System;
using System.Collections.Generic;

namespace SideloaderModpackUpdater.Data;

public class PathNode
{
    public List<PathNode> Children;

    public bool Expended;

    public bool IsFile;

    public string Name;
    public string Path;

    public bool RealUpdate;

    public bool ShouldUpdate;

    public double Size;

    public DateTime UpdateTime;
}