using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;

namespace SideloaderModpackUpdater.Data;

public class DownloadManager
{
    public static event Action<DownloadManager> AfterFinishDownload;
    
    public static readonly List<DownloadManager> DownloadManagers = new()
    {
        new DownloadManager(),
        new DownloadManager(),
        new DownloadManager(),
        new DownloadManager(),
        new DownloadManager(),
        new DownloadManager(),
        new DownloadManager(),
        new DownloadManager()
    };

    public static bool Unoccupied => DownloadManagers.All(u => u._unoccupied);

    public static void AutoAddTask(DownloadTask task)
    {
        var minPos = 0;
        var minValue = int.MaxValue;

        for (var i = 0; i < DownloadManagers.Count; i++)
            if (minValue > DownloadManagers[i]._tasks.Count)
            {
                minValue = DownloadManagers[i]._tasks.Count;
                minPos = i;
            }

        DownloadManagers[minPos].AddTask(task);
    }

    public static void ClearAllThread()
    {
        foreach (var downloadManager in DownloadManagers) downloadManager._threadIsRun = false;
        DownloadManagers.Clear();
    }
   

    private readonly LinkedList<DownloadTask> _tasks = new();

    private bool _threadIsRun;

    private bool _unoccupied = true;

    private readonly HttpClient _client = new();

    public DownloadTask CurrentTask { get; private set; }
    
    private DownloadManager()
    {
        Thread downloadThread = new(RunAsync);
        _threadIsRun = true;
        downloadThread.Start();
    }
    
    
    private void AddTask(DownloadTask task)
    {
        _tasks.AddLast(task);
    }

    private async void RunAsync()
    {
        while (_threadIsRun)
        {
            CurrentTask = _tasks.First?.Value;
            if (CurrentTask is not null)
            {
                try
                {
                    _unoccupied = false;
                    
                    _tasks.RemoveFirst();

                    if (!Directory.Exists(CurrentTask.Path))
                        Directory.CreateDirectory(CurrentTask.Path);

                    SimpleLogger.LogInfo($"Start download {CurrentTask.Name}");
                    AfterFinishDownload?.Invoke(this);

                    var response = await _client.GetStreamAsync(CurrentTask.Url);

                    await using var fs = File.Open(Path.Combine(CurrentTask.Path, CurrentTask.Name), FileMode.Create);

                    await response.CopyToAsync(fs);

                    SimpleLogger.LogInfo($"Finish download {CurrentTask.Name}");
                }
                catch
                {
                    var path = Path.Combine(CurrentTask.Path, CurrentTask.Name);
                    if (File.Exists(path))
                        File.Delete(path);
                    
                    SimpleLogger.LogError($"\r\n {CurrentTask.Name} download failed!");
                }
            }
            else
            {
                if (!_unoccupied)
                {
                    AfterFinishDownload?.Invoke(this);
                    _unoccupied = true;
                }
                Thread.Sleep(1000);
            }
        }
    }
}