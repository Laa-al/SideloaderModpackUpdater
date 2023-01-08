﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;

namespace SideloaderModpackUpdater.Data;

public class DownloadManager
{
    private static readonly List<DownloadManager> DownloadManagers = new()
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
            var task = _tasks.First?.Value;
            if (task is not null)
            {
                _unoccupied = false;
                try
                {
                    _tasks.RemoveFirst();

                    if (!Directory.Exists(task.Path))
                        Directory.CreateDirectory(task.Path);

                    var response = await _client.GetStreamAsync(task.Url);

                    await using var fs = File.Open(Path.Combine(task.Path, task.Name), FileMode.Create);

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