using System;
using System.Collections.Generic;

namespace SideloaderModpackUpdater.Data
{  
    [Serializable]
    public class PathNode
    {
        public string Path;

        public string Name;

        public DateTime UpdateTime;

        public double Size;

        public bool IsFile;

        public bool ShouldUpdate;

        public bool RealUpdate;
        
        public bool Expended;
        
        public List<PathNode> Children;
    }
}
