using UnityEngine;
using System.Collections.Generic;
using System.IO;
using UnityEngine.SceneManagement;

public class FrameRateVariation : BasePlayer
{
    [SerializeField] private string _ContentName = "longdress";
    [SerializeField] private string _NextScene = "Assets/Scenes/FrameRateVariation/soldierFrameRateVariation";
    [SerializeField] private int _FrameRate = 30; // starting frame rate
    [SerializeField] private int _ContentRate = 5; // quality of point cloud
    [SerializeField] private int _StartFrame = 0;
    [SerializeField] private int _LastFrame = 299;
    [SerializeField] private float _Duration = 0f; // optional duration override
    [SerializeField] private GameObject UI;

    // Two example arrays defining micro-stutter or jitter behavior. You can expand these or load from external configs.
    private float[] _dipEverySeconds = {2f, 4f}; // frequent/infrequent dips
    private int[] _dipTargets = {25, 20, 15};    // example dip targets
    private float[] _dipDurations = {0.05f, 0.1f, 0.25f}; // variable durations
    private bool _inDip = false;
    private float _dipTimer = 0f;
    private float _dipDuration = 0.1f;
    private int _dipTarget = 25;
    private float _timeSinceLastDip = 0f;

    // Jitter demo fields
    private bool _useJitter = false;
    private float _jitterTimer = 0f;
    private float _jitterInterval = 0.1f;
    private float targetFrameTime = 1f / 30f; // Default to 30 FPS
    private int _targetFrameRate = 28;
    private int _rangeFrameRate = 2; // ±2 fps

    private DPCHandler _content;
    private string _MOSDataPath = "";
    private readonly string _MOSFolder = "MOS_FRAMERATEVARIATION";

    private RenderParams _rp;

    private int _TestNo = 0;

    protected override void Initialize()
    {
        // Similar to Stall/VersionSwitch setup
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

        // Create MOS file
        int count = 1;
        Directory.CreateDirectory($"{_MOSFolder}");
        while (File.Exists($"{_MOSFolder}/MOS.user{count}.txt"))
            count++;
        _MOSDataPath = $"{_MOSFolder}/MOS.user{count}.txt";
        File.AppendAllText(_MOSDataPath, $"ContentName={_ContentName}\nId,MOS\n");

        // Optionally set a custom duration if you want extra control
        float realDuration = _Duration <= 0f ? ((float)(_LastFrame - _StartFrame + 1) / _FrameRate) : _Duration;

        _content = new DPCHandler(_ContentName, _ContentRate, _StartFrame, _LastFrame, _FrameRate, realDuration);
        _buffer = new MyMath.Queue<DPCFrameBuffer>(_bufferSize);
        Buffering();
        SetCurrentFrameBuffer();
    }

    protected override int GetStartFrame() => _StartFrame;
    protected override VVHandler GetCurrentContent() => _content;
    protected override void UpdatePosition() { }

    protected override void ImporterNextFrame()
    {
        var inPlayContent = GetCurrentContent();
        string filename = inPlayContent.GetFullPath() + "\\" + inPlayContent.GetContentName() 
                          + (_currentImportFrame % (_LastFrame + 1 - _StartFrame)).ToString("D4") + ".ply\0";
        int count = FrameIO.PCreader.CountVertices(filename);
        if (count <= 0) 
            UnityEngine.Debug.LogError("Fail to import frame. Filename: " + filename);

        DPCFrameBuffer temp = new(count);
        FrameIO.PCreader.LoadPlyFileData(filename, temp.vertex, temp.color);
        _buffer.Enqueue(temp);
    }

    protected override void Update()
    {
        // Frame rate variation logic
        // 1) Micro-stutter example (when _useJitter == false)
        if (!_useJitter)
        {
            _timeSinceLastDip += Time.deltaTime;
            if (!_inDip && _timeSinceLastDip >= _dipEverySeconds[0]) // pick frequent vs infrequent or check conditions
            {
                _timeSinceLastDip = 0f;
                _inDip = true;
                _dipTimer = 0f;
                _dipDuration = _dipDurations[Random.Range(0, _dipDurations.Length)];
                _dipTarget = _dipTargets[Random.Range(0, _dipTargets.Length)];
            }
            if (_inDip)
            {
                _dipTimer += Time.deltaTime;
                // artificially slow down or "simulate" the dip
                // for demonstration, just override targetFrameTime
                targetFrameTime = 1f / _dipTarget;
                if (_dipTimer >= _dipDuration)
                {
                    _inDip = false;
                    // revert to normal frame rate
                    targetFrameTime = 1f / _FrameRate;
                }
            }
        }
        // 2) Jitter example (when _useJitter == true)
        else
        {
            _jitterTimer += Time.deltaTime;
            if (_jitterTimer >= _jitterInterval)
            {
                _jitterTimer = 0f;
                int minFps = _targetFrameRate - _rangeFrameRate;
                int maxFps = _targetFrameRate + _rangeFrameRate;
                int newFps = Random.Range(minFps, maxFps + 1);
                targetFrameTime = 1f / newFps;
            }
        }

        base.Update(); // calls RenderCurrentFrame, timing, etc.
    }

    protected override void RenderCurrentFrame()
    {
        _material.SetBuffer("_Positions", _posBuffer);
        _material.SetBuffer("_Colors", _colorBuffer);
        _material.SetMatrix("_Transform", Matrix4x4.TRS(_offset, Quaternion.Euler(_rotation), _scale));
        _material.SetPass(0);
        _rp = new RenderParams(_material);
        _rp.worldBounds = new Bounds(Vector3.zero, 10000f * Vector3.one);
        Graphics.RenderPrimitives(_rp, MeshTopology.Points, _buffer.Peek().NumVerts);
    }

    protected override void DeleteBuffers()
    {
        if (_posBuffer != null) _posBuffer.Release();
        if (_colorBuffer != null) _colorBuffer.Release();
    }

    protected override void SetCurrentFrameBuffer()
    {
        _posBuffer = new ComputeBuffer(_buffer.Peek().NumVerts, 12);
        _colorBuffer = new ComputeBuffer(_buffer.Peek().NumVerts, 4);

        _posBuffer.SetData(_buffer.Peek().vertex);
        _colorBuffer.SetData(_buffer.Peek().color);
    }

    public void SaveMOS5() { File.AppendAllText(_MOSDataPath, $"{_TestNo},5\n"); }
    public void SaveMOS4() { File.AppendAllText(_MOSDataPath, $"{_TestNo},4\n"); }
    public void SaveMOS3() { File.AppendAllText(_MOSDataPath, $"{_TestNo},3\n"); }
    public void SaveMOS2() { File.AppendAllText(_MOSDataPath, $"{_TestNo},2\n"); }
    public void SaveMOS1() { File.AppendAllText(_MOSDataPath, $"{_TestNo},1\n"); }

    public void GoToNextTestSample()
    {
        // Example logic that increments test or loads next scene
        Pause();
        DeleteBuffers();
        SceneManager.LoadScene($"{_NextScene}.unity");
        // or re-initialize, etc.
    }
}