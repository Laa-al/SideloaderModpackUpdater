using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json;
using SideloaderModpackUpdater.Data;

namespace SideloaderModpackUpdater.Pages;

public partial class Index:IDisposable
{
    private bool _downloadButtonDisabled;
    private NamePath _input = new("", "");

    private PathNode _rootNode = new();

    private bool _updateButtonDisabled;

    private bool DownloadButtonDisabled => !DownloadManager.Unoccupied || _downloadButtonDisabled;

    protected override async Task OnInitializedAsync()
    {
        if (File.Exists("config.json"))
        {
            await using var fs = File.Open("config.json", FileMode.Open);

            using var sr = new StreamReader(fs);

            _rootNode = JsonConvert.DeserializeObject<PathNode>(await sr.ReadToEndAsync());

            _input = new NamePath(_rootNode.Name, _rootNode.Path);
        }

        DownloadManager.AfterFinishDownload += Trigger;
    }

    private static void OperateNodeAndItChildren<TParameter>(
        PathNode node,
        TParameter parameter,
        Func<PathNode, TParameter, TParameter> operation)
    {
        var next = operation(node, parameter);

        if (next is null || node.Children is null)
            return;

        foreach (var child in node.Children)
            OperateNodeAndItChildren(child, next, operation);
    }

    private static void CheckUpdateForOneNode(PathNode node, string path)
    {
        if (File.Exists(path))
        {
            var fileInfo = new FileInfo(path);
            if (fileInfo.LastWriteTime >= node.UpdateTime &&
                node.Size * 0x100000 + 0x200000 > fileInfo.Length)
            {
                node.ShouldUpdate = false;
                return;
            }
        }

        node.ShouldUpdate = true;
        Console.WriteLine($"{node.Name} need update");
    }

    private void ApplyInput()
    {
        _rootNode.Name = _input.Name;
        _rootNode.Path = _input.Path;
    }

    private void ExpendAllNode()
    {
        OperateNodeAndItChildren(_rootNode, !_rootNode.Expended, ExpendAllNodeOperation);

        StateHasChanged();
    }

    private static bool ExpendAllNodeOperation(PathNode node, bool value)
    {
        node.Expended = value;

        return value;
    }

    private void SelectedWitchNeedUpdate()
    {
        OperateNodeAndItChildren(_rootNode, !_rootNode.ShouldUpdate, SelectedWitchNeedUpdateOperation);

        _rootNode.ShouldUpdate = !_rootNode.ShouldUpdate;

        StateHasChanged();
    }

    private static bool SelectedWitchNeedUpdateOperation(PathNode node, bool value)
    {
        node.RealUpdate = value && node.ShouldUpdate;

        return value;
    }

    private async Task SaveConfigAsync()
    {
        await using var fs = File.Open("config.json", FileMode.Create);

        await using var sw = new StreamWriter(fs);

        await sw.WriteAsync(JsonConvert.SerializeObject(_rootNode));
    }

    private void Select(PathNode node)
    {
        OperateNodeAndItChildren(node, !node.RealUpdate, SelectChildren);

        StateHasChanged();
    }

    private static bool SelectChildren(PathNode node, bool value)
    {
        node.RealUpdate = value;

        return value;
    }


    private void CheckUpdateByConfig()
    {
        ApplyInput();

        OperateNodeAndItChildren(_rootNode, "", CheckUpdateOperation);

        StateHasChanged();
    }

    private static string CheckUpdateOperation(PathNode node, string path)
    {
        path = Path.Combine(path, node.Name);

        if (!node.IsFile) return path;

        CheckUpdateForOneNode(node, path);

        return null;
    }


    private void Download()
    {
        if (DownloadButtonDisabled)
            return;

        _downloadButtonDisabled = true;

        OperateNodeAndItChildren(_rootNode, new NamePath("", ""), DownloadOperation);

        StateHasChanged();
    }

    private static NamePath DownloadOperation(PathNode node, NamePath namePath)
    {
        if (namePath.Name is null)
            return null;

        if (!node.IsFile)
            return new NamePath(
                Path.Combine(namePath.Name, node.Name),
                Path.Combine(namePath.Path, node.Path));

        if (node.RealUpdate)
            DownloadManager.AutoAddTask(
                new DownloadTask
                {
                    Name = node.Name,
                    Path = namePath.Name,
                    Url = Path.Combine(namePath.Path, node.Path)
                });

        return null;
    }

    private async Task UpdateSourceAsync()
    {
        _updateButtonDisabled = true;

        await SaveConfigAsync();

        Console.WriteLine("Mod below should be update!");

        _rootNode = new PathNode
        {
            Path = _input.Path,
            Name = _input.Name
        };

        OperateNodeAndItChildren(_rootNode, new NamePath("", ""), UpdateSourceOperation);

        await SaveConfigAsync();

        Console.WriteLine("Successfully check update!");

        _updateButtonDisabled = false;

        await InvokeAsync(StateHasChanged);
    }

    private static NamePath UpdateSourceOperation(PathNode node, NamePath namePath)
    {
        if (!node.IsFile)
        {
            var namePathCurrent = new NamePath(
                Path.Combine(namePath.Name, node.Name),
                Path.Combine(namePath.Path, node.Path));

            try
            {
                var client = new HttpClient();

                var response = client.GetStringAsync(namePathCurrent.Path).Result;

                node.Children = GetPathNodesFromHtml(response);

                return namePathCurrent;
            }
            catch
            {
                return null;
            }
        }

        CheckUpdateForOneNode(node, Path.Combine(namePath.Name, node.Name));

        return null;
    }

    private static List<PathNode> GetPathNodesFromHtml(string html)
    {
        var start = html.IndexOf("<table", StringComparison.Ordinal);
        var end = html.IndexOf("</table>", StringComparison.Ordinal) + 8;

        var xml = html[start..end].Replace('&', ' ');

        var document = XDocument.Parse(xml);
        if (document.Root is null) return null;
        var elements = document.Root.Elements();

        List<PathNode> pathNodes = new();

        foreach (var element in elements.Where(u => u.Name == "tr"))
        {
            var classAttr = element.Attribute("class");

            if (classAttr is null || classAttr.Value == "indexhead") continue;

            XElement
                indexcollastmod = null,
                indexcolsize = null,
                indexcolname = null;

            foreach (var attr in element.Elements().Where(u => u.Name == "td"))
            {
                var attribute = attr.Attribute("class");
                if (attribute is null) continue;

                switch (attribute.Value)
                {
                    case "indexcollastmod":
                        indexcollastmod = attr;
                        break;
                    case "indexcolsize":
                        indexcolsize = attr;
                        break;
                    case "indexcolname":
                        indexcolname = attr;
                        break;
                }
            }

            var a = indexcolname?.Element("a");

            if (a is null || a.Value == "Parent Directory") continue;

            var path = a.Attribute("href")?.Value.Trim();
            var name = a.Value.Trim();

            if (name.EndsWith('/'))
                name = name[..^1];

            if (path is null || name is null) continue;

            pathNodes.Add(new PathNode
            {
                Path = path,
                Name = name,
                IsFile = !path.EndsWith("/"),
                UpdateTime =
                    DateTime.TryParse(indexcollastmod?.Value, out var updateTime)
                        ? updateTime
                        : DateTime.MinValue,
                Size =
                    double.TryParse(indexcolsize?.Value[..^1], out var size) ? size : 0.0
            });
        }

        return pathNodes;
    }

    private async void Trigger(DownloadManager manager)
    {
        await InvokeAsync( StateHasChanged);
    }
    
    public void Dispose()
    {
        DownloadManager.AfterFinishDownload -= Trigger;
    }
}