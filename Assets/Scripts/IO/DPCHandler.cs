using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class VVHandler
{

    protected string contentName = "longdress";
    protected int contentRate = 5;
    protected int startFrame = 0;
    protected int lastFrame = 299;
    protected int frameRate = 30;

    protected float duration = 0;

    protected string contentFolderPath;

    public VVHandler(string contentName, string contentFolderPath, int contentRate = 5, int startFrame = 0, int lastFrame = 299, int frameRate = 30, float duration = 0)
    {
        this.contentFolderPath = contentFolderPath;
        this.contentName = contentName;
        this.contentRate = contentRate;
        this.startFrame = startFrame;
        this.lastFrame = lastFrame;
        this.frameRate = frameRate;
        this.duration = duration
            ;
        if (!System.IO.Directory.Exists(GetFullPath()))
        {
            UnityEngine.Debug.LogError($"Cannot find content. Have you place it in {contentFolderPath} ?");
        }
    }

    public VVHandler(VVHandler other)
    {
        this.contentName = other.contentName;
        this.contentRate = other.contentRate;
        this.startFrame = other.startFrame;
        this.lastFrame = other.lastFrame;
        this.frameRate = other.frameRate;
    }

    public string GetContentName() { return this.contentName; }
    public int GetContentRate() { return this.contentRate; }
    public int GetStartFrame() { return this.startFrame; }
    public int GetLastFrame() { return this.lastFrame; }
    public int GetFrameRate() { return this.frameRate; }

    public float GetDuration() { return this.duration; }
    public virtual string GetFullPath() { return $"{contentFolderPath}\\{contentName}\\representation{contentRate}"; }
}

public class DPCHandler : VVHandler
{
    public DPCHandler(string contentName, int contentRate = 5, int startFrame = 0, int lastFrame = 299, int frameRate = 30, float duration = 0) 
        : base(contentName, "Assets\\PointCloud", contentRate, startFrame, lastFrame, frameRate, duration) { }
}
