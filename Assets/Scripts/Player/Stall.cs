using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using UnityEngine.SceneManagement;

public class Stall : BasePlayer
{
    [SerializeField] private string _ContentName = "longdress";
    [SerializeField] private string _NextScene = "Assets/Scenes/Stall/soldierStall";

    [SerializeField] private int _ContentRate = 5;
    [SerializeField] private int _StartFrame = 0;
    [SerializeField] private int _LastFrame = 299;
    [SerializeField] private int _FrameRate = 30;

    [SerializeField] private GameObject UI;

    public Vector3 OffSet = new(0f, 0f, 0f);
    public Vector3 Rotation = new(0f, 0f, 0f);

    protected ComputeBuffer _posBuffer;
    protected ComputeBuffer _colorBuffer;

    [SerializeField] private int[] StallAt = { 60, 120, 180, 240 };
    private int[][] StallCount = { new int[]{0, 7, 0, 0},
                                    new int[]{0, 15, 0, 0},
                                    new int[]{0, 22, 0, 0},
                                    new int[]{0, 30, 0, 0},
                                    new int[]{0, 45, 0, 0},
                                    new int[]{0, 60, 0, 0},
                                    new int[]{0, 90, 0, 0},
                                    new int[]{0, 120, 0, 0},
                                    new int[]{7, 0, 7, 0},
                                    new int[]{15, 0, 15, 0},
                                    new int[]{30, 0, 30, 0},
                                    new int[]{60, 0, 60, 0},
                                    new int[]{7, 0, 7, 7},
                                    new int[]{15, 0, 15, 15},
                                    new int[]{30, 0, 30, 30},
                                    new int[]{7, 7, 7, 7},
                                    new int[]{15, 15, 15, 15},
                                    new int[]{30, 30, 30, 30},
                                    new int[]{0, 0, 0, 7},
                                    new int[]{0, 0, 0, 15},
                                    new int[]{0, 0, 0, 22},
                                    new int[]{0, 0, 0, 30},
                                    new int[]{0, 0, 0, 45},
                                    new int[]{0, 0, 0, 60},
                                    new int[]{0, 0, 0, 90},
                                    new int[]{0, 0, 0, 120},
                                    new int[]{0, 7, 7, 0},
                                    new int[]{0, 15, 15, 0},
                                    new int[]{0, 30, 30, 0},
                                    new int[]{0, 60, 60, 0},
                                    new int[]{7, 7, 7, 0},
                                    new int[]{15, 15, 15, 0},
                                    new int[]{30, 30, 30, 0}};

    private int[][] ImportFrame;
    private int ImportIndex = 0;
    private int _TestNo = 0;

    private string _MOSDataPath;
    private string _MOSFolder = "MOS_STALL";

    private DPCHandler _content;

    private static int[] GenerateStallArray(int[] stallAt, int[] stallCount, int lastFrame)
    {
        List<int> result = new List<int>();

        int index = 0;
        int tmp = -1;
        while (tmp < lastFrame)
        {
            if (tmp == stallAt[index] && stallCount[index] > 0) { stallCount[index]--; }
            else tmp++;
            result.Add(tmp);
            if (stallCount[index] == 0) index += index == stallAt.Length - 1 ? 0 : 1;
        }
        result.Add(0);
        return result.ToArray();
    }
    private static int[][] GenerateStallArrays(int[] stallAt, int[][] stallCount, int lastFrame)
    {
        List<int[]> results = new List<int[]>();
        foreach (var c in stallCount)
            results.Add(GenerateStallArray(stallAt, c, lastFrame));
        return results.ToArray();
    }
    protected override void Initialize()
    {
        _offset = new(0f, 0f, 0f);
        _scale = new(0.002f, 0.002f, 0.002f);
        _rotation = new(0f, 0f, 0f);
        _tint = new(0.6f, 0.6f, 0.6f);

        if (_shader == null)
            _shader = Shader.Find("Point_BothEyes");
        _material = new Material(_shader);

        _material.EnableKeyword("_COMPUTE_BUFFER");
        _material.SetColor("_Tint", _tint);
        _material.SetMatrix("_Transform", Matrix4x4.TRS(_offset, Quaternion.Euler(_rotation), _scale));


        // Create opinion score file 
        int count = 1;
        Directory.CreateDirectory($"{_MOSFolder}");
        while (File.Exists($"{_MOSFolder}/MOS.user{count}.txt"))
        {
            count++;
        }
        _MOSDataPath = $"{_MOSFolder}/MOS.user{count}.txt";
        File.AppendAllText(_MOSDataPath, $"ContentName={_ContentName}\nId,MOS\n");
        //
        ImportFrame = GenerateStallArrays(StallAt, StallCount, _LastFrame);
        _content = new DPCHandler(_ContentName, _ContentRate, _StartFrame, _LastFrame, _FrameRate, ((float)ImportFrame[_TestNo].Length + 1) / _FrameRate);

    }

    protected override int GetStartFrame() => _StartFrame;
    protected override VVHandler GetCurrentContent() => _content;
    protected override void UpdatePosition() { }
    protected override void EndOfContent()
    {
        Pause();
        UI.SetActive(true);
    }
    private RenderParams _rp;
    protected override void RenderCurrentFrame()
    {
        _material.SetBuffer("_Positions", _posBuffer);
        _material.SetBuffer("_Colors", _colorBuffer);
        _material.SetMatrix("_Transform", Matrix4x4.TRS(_offset, Quaternion.Euler(_rotation), _scale));
        _material.SetPass(0);
        _rp = new RenderParams(_material);
        _rp.worldBounds = new Bounds(Vector3.zero, 10000 * Vector3.one);

        DPCFrameBuffer frontBuffer;
        if (_buffer.TryPeek(out frontBuffer))
        {
            Graphics.RenderPrimitives(_rp, MeshTopology.Points, frontBuffer.NumVerts);
        }
    }
    protected override void DeleteBuffers()
    {
        if (_posBuffer != null)
        {
            _posBuffer.Release();
            _posBuffer = null;
        }
        if (_colorBuffer != null)
        {
            _colorBuffer.Release();
            _colorBuffer = null;
        }
    }
    protected override void SetCurrentFrameBuffer()
    {
        if (_buffer.TryPeek(out var frontBuffer))
        {
            _posBuffer = new ComputeBuffer(frontBuffer.NumVerts, 12);
            _colorBuffer = new ComputeBuffer(frontBuffer.NumVerts, 4);

            _posBuffer.SetData(frontBuffer.vertex);
            _colorBuffer.SetData(frontBuffer.color);
        }
        else
        {
            Debug.LogError("SetCurrentFrameBuffer: Buffer is empty.");
        }
    }

    protected override void ImporterNextFrame()
    {
        var inPlayContent = GetCurrentContent();
        string filename = inPlayContent.GetFullPath() + "\\" + inPlayContent.GetContentName() + (ImportFrame[_TestNo][ImportIndex]).ToString("D4") + ".ply\0";
        int count = FrameIO.PCreader.CountVertices(filename);

        if (count <= 0)
            UnityEngine.Debug.LogError("Fail to import frame. Filename: " + filename);

        DPCFrameBuffer temp = new(count);
        FrameIO.PCreader.LoadPlyFileData(filename, temp.vertex, temp.color);
        _buffer.Enqueue(temp);

        ImportIndex += ImportIndex == ImportFrame[_TestNo].Length - 1 ? 0 : 1;
    }

    public void GoToNextTestSample()
    {
        _timer = 0.0f;
        stopwatch = new();

        if (_TestNo == StallCount.Length - 1)
        {
            if (_NextScene != null)
                SceneManager.LoadScene($"{_NextScene}.unity");
            return;
        }
        _TestNo++;
        ImportIndex = 0;
        _currentImportFrame = _currentRenderFrame = ImportFrame[_TestNo][0];

        _content = new DPCHandler(_ContentName, _ContentRate, _StartFrame, _LastFrame, _FrameRate, ((float)ImportFrame[_TestNo].Length + 1) / _FrameRate);

        _buffer = new System.Collections.Concurrent.ConcurrentQueue<DPCFrameBuffer>();
        DeleteBuffers();
        Buffering();
        SetCurrentFrameBuffer();
        Play();
    }

    public void SaveMOS5() { File.AppendAllText(_MOSDataPath, $"{_TestNo},5\n"); }
    public void SaveMOS4() { File.AppendAllText(_MOSDataPath, $"{_TestNo},4\n"); }
    public void SaveMOS3() { File.AppendAllText(_MOSDataPath, $"{_TestNo},3\n"); }
    public void SaveMOS2() { File.AppendAllText(_MOSDataPath, $"{_TestNo},2\n"); }
    public void SaveMOS1() { File.AppendAllText(_MOSDataPath, $"{_TestNo},1\n"); }

}
