using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json;
using SideloaderModpackUpdater.Data;

namespace SideloaderModpackUpdater.Pages
{
    public partial class Index
    {
        private string _originUrl;

        private string _localPath;

        private PathNode _rootNode = new()
        {
            Children = new()
        };

        protected override async Task OnInitializedAsync()
        {
            if (File.Exists("config.json"))
            {
                await using var fs = File.Open("config.json", FileMode.Open);

                using var sr = new StreamReader(fs);

                _rootNode = JsonConvert.DeserializeObject<PathNode>(await sr.ReadToEndAsync());

                _originUrl = _rootNode.Path;
                _localPath = _rootNode.Name;
            }
        }

        private bool _updateButtonDisabled;

        private bool _downloadButtonDisabled;
        
        private bool DownloadButtonDisabled => !DownloadManager.Unoccupied || _downloadButtonDisabled;

        private void UpdatePathOnly()
        {
            _rootNode.Name = _localPath;
        }

        private void CheckUpdateOnly()
        {
            UpdatePathOnly();
            CheckUpdateOnly(_rootNode, "");
        }

        private void CheckUpdateOnly(PathNode node, string parentPath)
        {
            string newPath = Path.Combine(parentPath, node.Name);

            if (node.IsFile)
            {
                CheckNodeUpdate(node, newPath);
            }
            else if (node.Children is not null)
                foreach (var chi in node.Children)
                    CheckUpdateOnly(chi, newPath);
        }
        
        private void CheckNodeUpdate(PathNode node, string path)
        {
            if (File.Exists(path))
            {
                var fileInfo = new FileInfo(path);
                if (fileInfo.LastWriteTime >= node.UpdateTime &&
                    node.Size * 0x100000 + 0x200000  > fileInfo.Length)
                {
                    node.ShouldUpdate = false;
                    return;
                }
            }
            node.ShouldUpdate = true;
            Console.WriteLine($"{node.Name} need update");
        }


        private void ExpendAll()
        {
            ExpendAll(_rootNode, !_rootNode.Expended);
        }

        private void ExpendAll(PathNode node, bool value)
        {
            node.Expended = value;

            if (node.Children is not null)
                foreach (var chi in node.Children)
                    ExpendAll(chi, value);
        }

        private void SelectedNeedUpdate()
        {
            SelectedNeedUpdate(_rootNode, !_rootNode.ShouldUpdate);
            _rootNode.ShouldUpdate = !_rootNode.ShouldUpdate;
        }

        private void SelectedNeedUpdate(PathNode node, bool value)
        {
            node.RealUpdate = value && node.ShouldUpdate;

            if (node.Children is not null)
                foreach (var chi in node.Children)
                    SelectedNeedUpdate(chi, value);
        }

        private async Task SaveConfigAsync()
        {
            await using var fs = File.Open("config.json", FileMode.Create);

            await using var sw = new StreamWriter(fs);

            await sw.WriteAsync(JsonConvert.SerializeObject(_rootNode));
        }

        private void SelectAction(PathNode node)
        {
            SelectChildren(node, !node.RealUpdate);
        }

        static void SelectChildren(PathNode node, bool value)
        {
            node.RealUpdate = value;
            if (node.Children is null)
                return;
            foreach (var child in node.Children)
                SelectChildren(child, value);
        }


        private async Task StartDownloadAsync()
        {
            if (DownloadButtonDisabled)
                return;
            _downloadButtonDisabled = true;
            await DownloadAsync(_rootNode.Children, _rootNode.Path, _rootNode.Name);
            _downloadButtonDisabled = false;
        }


        private async Task DownloadAsync(List<PathNode> nodes, string url, string localPath)
        {
            if (localPath is null)
                return;
            // if (!Directory.Exists(localPath))
            //     Directory.CreateDirectory(localPath);

            foreach (var node in nodes)
            {
                var newUrl = Path.Combine(url, node.Path);

                if (node.IsFile && node.RealUpdate)
                {
                    DownloadManager.AutoAddTask(new()
                    {
                        Name = node.Name,
                        Path = localPath,
                        Url = newUrl
                    });
                }
                else if (!node.IsFile)
                {
                    await DownloadAsync(node.Children, newUrl, Path.Combine(localPath, node.Name));
                }
            }
        }

        private async Task StartUpdateSourceAsync()
        {
            _updateButtonDisabled = true;
            
            _rootNode.Path = _originUrl;
            _rootNode.Name = _localPath;

            Console.WriteLine("Mod below should be update!");

            _rootNode.Children = await UpdateSourceAsync(_originUrl, _localPath);

            await SaveConfigAsync();

            
            _updateButtonDisabled = false;
        }

        private async Task<List<PathNode>> UpdateSourceAsync(string url, string localPath)
        {
            var nodes = await AnalyzeUrlAsync(url);

            if (nodes == null) return new();

            foreach (var node in nodes)
            {
                string newPath = Path.Combine(localPath, node.Name);
                string newUrl = Path.Combine(url, node.Path);

                if (node.IsFile)
                {
                    
                    CheckNodeUpdate(node, newPath);

                    node.Children = new();
                }
                else
                {
                    node.Children = await UpdateSourceAsync(newUrl, newPath);
                }
            }

            return nodes;
        }

        private async Task<List<PathNode>> AnalyzeUrlAsync(string url)
        {
            try
            {
                var client = new HttpClient();

                var response = await client.GetStringAsync(url);

                var start = response.IndexOf("<table", StringComparison.Ordinal);
                var end = response.IndexOf("</table>", StringComparison.Ordinal) + 8;

                var origin = response[start..end].Replace('&', ' ');

                return AnalyzePath(origin);
            }
            catch
            {
                return null;
            }
        }

        List<PathNode> AnalyzePath(string xml)
        {
            XDocument document = XDocument.Parse(xml);
            if (document.Root is null) return null;
            var elements = document.Root.Elements();

            List<PathNode> pathNodes = new();
            foreach (var element in elements.Where(u => u.Name == "tr"))
            {
                var classAttr = element.Attribute("class");

                if (classAttr is null || classAttr.Value == "indexhead") continue;

                var attrs = new Dictionary<string, XElement>();

                foreach (var attr in element.Elements().Where(u => u.Name == "td"))
                {
                    var attribute = attr.Attribute("class");
                    if (attribute is null) continue;

                    attrs[attribute.Value] = attr;
                }

                if (!attrs.ContainsKey("indexcolname")) continue;

                var a = attrs["indexcolname"].Element("a");

                if (a is null || a.Value == "Parent Directory") continue;

                string name = a.Value.Trim();
                if (name.EndsWith('/'))
                    name = name[..^1];
                
                var pathNode = new PathNode
                {
                    Path = a.Attribute("href")?.Value.Trim(),
                    Name = name
                };

                if (pathNode.Path != null)
                    pathNode.IsFile = !pathNode.Path.EndsWith("/");

                if (attrs.TryGetValue("indexcollastmod", out XElement indexcollastmod))
                    _ = DateTime.TryParse(indexcollastmod.Value, out pathNode.UpdateTime);

                if (attrs.TryGetValue("indexcolsize", out XElement indexcolsize))
                    _ = double.TryParse(indexcolsize.Value[..^1], out pathNode.Size);
                pathNodes.Add(pathNode);
            }

            return pathNodes;
        }
    }
}