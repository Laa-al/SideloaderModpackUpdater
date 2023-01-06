using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SideloaderModpackUpdater.Data;

public class DownloadManager
{
    private static readonly List<DownloadManager> DownloadManagers = new()
    {
        new(),
        new(),
        new(),
        new(),
        new(),
        new(),
        new(),
        new(),
    };

    public static bool Unoccupied => DownloadManagers.All(u => u._unoccupied);
    
    public static void AutoAddTask(DownloadTask task)
    {
        int minPos = 0;
        int minValue = int.MaxValue;

        for (int i = 0; i < DownloadManagers.Count; i++)
        {
            if (minValue > DownloadManagers[i]._tasks.Count)
            {
                minValue = DownloadManagers[i]._tasks.Count;
                minPos = i;
            }
        }
        
        DownloadManagers[minPos].AddTask(task);
    }

    public static void ClearAllThread()
    {
        foreach (var downloadManager in DownloadManagers)
        {
            downloadManager._threadIsRun = false;
        }
        DownloadManagers.Clear();
    }
    
    private DownloadManager()
    {
        Thread downloadThread = new(RunAsync);
        _threadIsRun = true;
        downloadThread.Start();
    }

    private bool _unoccupied = true;
    
    private readonly LinkedList<DownloadTask> _tasks = new();

    private bool _threadIsRun;
    
    private void AddTask(DownloadTask task)
    {
        _tasks.AddLast(task);
    }
    
    private async void RunAsync()
    {
        while (_threadIsRun)
        {
            var task = _tasks.First?.Value;
            if (task is not null)
            {
                _unoccupied = false;
                try
                {
                    _tasks.RemoveFirst();

                    if (!Directory.Exists(task.Path))
                        Directory.CreateDirectory(task.Path);
                    
                    var client = new HttpClient();

                    var response = await client.GetStreamAsync(task.Url);

                    await using var fs = File.Open(Path.Combine(task.Path,task.Name) , FileMode.Create);
                    
                    await response.CopyToAsync(fs);

                    Console.WriteLine($"\r\n Finish download {task.Name}");
                }
                catch
                {
                    // ignored
                }
            }
            else
            {
                _unoccupied = true;
                Thread.Sleep(1000);
            }
        }
    }
}