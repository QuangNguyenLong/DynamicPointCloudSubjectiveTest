using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Plays one of 42 deterministic frame-rate-variation stimuli over a 30 fps source.
/// Each stimulus is defined by a StrideForFrame(int f) function that tells us
/// how many source frames we should *advance* after we render frame f.
/// A stride of 1 → show every frame; 2 → drop every second source frame, etc.
/// </summary>
public class FrameRateVariation : BasePlayer
{
    // ───────────────────── inspector ─────────────────────
    [Header("Experiment setup")]
    [SerializeField, Range(0, 41)]           private int  _variantIndex = 0;
    [SerializeField]                         private string _nextScene  = "";

    [Header("Content")]
    [SerializeField] private string  _contentName = "longdress";
    [SerializeField] private int     _contentRate = 5;      // #points / frame in M
    [SerializeField] private int     _startFrame  = 0;
    [SerializeField] private int     _lastFrame   = 299;
    [SerializeField] private int     _frameRate   = 30;

    [Header("UI")]
    [SerializeField] private GameObject UI;

    // ───────────────────── internals ─────────────────────
    private readonly List<FRVVariant>   _variants = new();
    private             int[]           _importFrames;
    private             int             _importCursor;
    private             int             _testNo;

    private DPCHandler _content;
    private string     _mosPath;

    private ComputeBuffer _posBuffer, _colorBuffer;
    private RenderParams  _rp;

    #region Unity lifecycle ------------------------------------------------------

    protected override void Initialize()
    {
        /* one-time parameter taken from your Stall.Init() ---------------------- */
        _offset   = Vector3.zero;
        _rotation = Vector3.zero;
        _scale    = Vector3.one * 0.002f;
        _tint     = new Color(0.6f, 0.6f, 0.6f, 1.0f);
        if (_shader == null)
            _shader = Shader.Find("Point_BothEyes");
        _material = new Material(_shader);
        _material.EnableKeyword("_COMPUTE_BUFFER");
        _material.SetColor("_Tint",        _tint);
        _material.SetMatrix("_Transform",  Matrix4x4.TRS(_offset, Quaternion.Euler(_rotation), _scale));

        /* build ALL patterns once --------------------------------------------- */
        BuildVariantTable();

        if (_variantIndex < 0 || _variantIndex >= _variants.Count)
            throw new ArgumentOutOfRangeException(nameof(_variantIndex),
                "Illegal variant index (0-41 expected)");

        _importFrames   = BuildPlayedSequence(_variants[_variantIndex]);
        _content        = new DPCHandler(_contentName, _contentRate, _startFrame, _lastFrame,
                                         _frameRate, ((float)_importFrames.Length+1)/_frameRate);

        /* create result file --------------------------------------------------- */
        var dir = "MOS_FRV";
        System.IO.Directory.CreateDirectory(dir);
        var id  = 1;
        while (System.IO.File.Exists($"{dir}/MOS.user{id}.txt")) id++;
        _mosPath = $"{dir}/MOS.user{id}.txt";
        System.IO.File.AppendAllText(_mosPath, $"ContentName={_contentName}\nVariant,MOS\n");
    }

    protected override int       GetStartFrame()                 => _startFrame;
    protected override VVHandler GetCurrentContent()             => _content;
    protected override void      UpdatePosition()                { /* not needed */ }

    protected override void EndOfContent()
    {
        Pause();
        UI.SetActive(true);
    }

    protected override void RenderCurrentFrame()
    {
        _material.SetBuffer("_Positions",  _posBuffer);
        _material.SetBuffer("_Colors",     _colorBuffer);
        _material.SetMatrix("_Transform",  Matrix4x4.TRS(_offset, Quaternion.Euler(_rotation), _scale));
        _material.SetPass(0);

        _rp = new RenderParams(_material) { worldBounds = new Bounds(Vector3.zero, 10000*Vector3.one) };
        Graphics.RenderPrimitives(_rp, MeshTopology.Points, _buffer.Peek().NumVerts);
    }

    protected override void DeleteBuffers()
    {
        _posBuffer?.Release();  _posBuffer = null;
        _colorBuffer?.Release(); _colorBuffer = null;
    }

    protected override void SetCurrentFrameBuffer()
    {
        _posBuffer   = new ComputeBuffer(_buffer.Peek().NumVerts, 12);
        _colorBuffer = new ComputeBuffer(_buffer.Peek().NumVerts,  4);
        _posBuffer.SetData(_buffer.Peek().vertex);
        _colorBuffer.SetData(_buffer.Peek().color);
    }

    protected override void ImporterNextFrame()
    {
        var path = $"{_content.GetFullPath()}\\{_content.GetContentName()}{_importFrames[_importCursor]:D4}.ply\0";
        var cnt  = FrameIO.PCreader.CountVertices(path);
        if (cnt <= 0) Debug.LogError($"Cannot import {path}");

        var fbuf = new DPCFrameBuffer(cnt);
        FrameIO.PCreader.LoadPlyFileData(path, fbuf.vertex, fbuf.color);
        _buffer.Enqueue(fbuf);

        if (_importCursor < _importFrames.Length - 1) _importCursor++;
    }

    #endregion ------------------------------------------------------------------

    #region MOS buttons ----------------------------------------------------------
    public void SaveMOS1() { Save(1); }
    public void SaveMOS2() { Save(2); }
    public void SaveMOS3() { Save(3); }
    public void SaveMOS4() { Save(4); }
    public void SaveMOS5() { Save(5); }

    private void Save(int score)
    {
        System.IO.File.AppendAllText(_mosPath, $"{_variantIndex},{score}\n");
        UI.SetActive(false);
        GoToNextVariant();
    }
    #endregion ------------------------------------------------------------------

    #region Variant bookkeeping --------------------------------------------------
    private void GoToNextVariant()
    {
        if (++_variantIndex >= _variants.Count)
        {
            if (!string.IsNullOrEmpty(_nextScene))
                SceneManager.LoadScene($"{_nextScene}.unity");
            return;
        }

        _importFrames   = BuildPlayedSequence(_variants[_variantIndex]);
        _importCursor   = 0;
        _currentImportFrame = _currentRenderFrame = _importFrames[0];

        _content = new DPCHandler(_contentName, _contentRate, _startFrame,
                                  _lastFrame, _frameRate,
                                  ((float)_importFrames.Length+1)/_frameRate);

        while (_buffer.Count > 0) _buffer.Dequeue();
        DeleteBuffers();
        Buffering();
        SetCurrentFrameBuffer();
        Play();
    }
    #endregion ------------------------------------------------------------------

    // ────────────────────────────────────────────────────────────────────────────
    // ░░  VARIANT  DEFINITION  SECTION
    // ────────────────────────────────────────────────────────────────────────────
    #region Hand-rolled deterministic patterns

    private const int TOTAL_FRAMES = 300;
    private const int SOURCE_FPS   = 30;

    private sealed class FRVVariant
    {
        public string Name;
        public Func<int, int> StrideForFrame;  // f -> stride (≥1)
    }

    private void BuildVariantTable()
    {
        // ─── 0-3  Baseline (constant) ──────────────────────────────────────────
        _variants.AddRange(new[]{
            Baseline(30),  // 0
            Baseline(25),  // 1
            Baseline(20),  // 2
            Baseline(15)   // 3
        });

        // ─── 4-9  Single macro-drop (centre 135-164) ───────────────────────────
        _variants.AddRange(new[]{
            SingleDip(30,15),  // 4
            SingleDip(30,20),  // 5
            SingleDip(30,25),  // 6
            SingleDip(25,15),  // 7
            SingleDip(25,20),  // 8
            SingleDip(20,15)   // 9
        });

        // ─── 10-21  Micro-stutter: fixed width=3f (100 ms) ─────────────────────
        _variants.AddRange(MicroGrid(fixedWidth:true));

        // ─── 22-33  Micro-stutter: variable width 1/3/8f ───────────────────────
        _variants.AddRange(MicroGrid(fixedWidth:false));

        // ─── 34-39  Jitter (bounded random walk) ───────────────────────────────
        _variants.AddRange(new[]{
            Jitter(28,2,3,  seed:101),   // 34
            Jitter(28,2,15, seed:102),   // 35
            Jitter(25,5,3,  seed:103),   // 36
            Jitter(25,5,15, seed:104),   // 37
            Jitter(20,10,3, seed:105),   // 38
            Jitter(20,10,15,seed:106)    // 39
        });

        // ─── 40-41  Hard jitter controls (drop 10 % / 20 %) ────────────────────
        _variants.Add(HardJitter(0.10f, seed:201));  // 40
        _variants.Add(HardJitter(0.20f, seed:202));  // 41
    }

    // ---------------- helper builders -----------------------------------------

    private FRVVariant Baseline(int fps)
    {
        var stride = Mathf.RoundToInt((float)SOURCE_FPS / fps);
        return new FRVVariant
        {
            Name = $"Baseline_{fps}fps",
            StrideForFrame = _ => stride
        };
    }

    private FRVVariant SingleDip(int baseFps, int dipFps)
    {
        // centre window 135-164 inclusive = 30 frames
        var baseStride = Mathf.RoundToInt((float)SOURCE_FPS / baseFps);
        var dipStride  = Mathf.RoundToInt((float)SOURCE_FPS / dipFps);

        return new FRVVariant
        {
            Name = $"SingleDip_{baseFps}to{dipFps}_30f",
            StrideForFrame = f => (f >= 135 && f <= 164) ? dipStride : baseStride
        };
    }

    private IEnumerable<FRVVariant> MicroGrid(bool fixedWidth)
    {
        int[] bases = {30,30,30,30,25,25,25,25,20,20,20,20};
        int[] dips  = {25,20,25,20,20,15,20,15,15,15,15,15};
        int[] ints  = {60,60,120,120,60,60,120,120,60,60,120,120};

        for (int i = 0; i < bases.Length; i++)
        {
            int baseFps = bases[i], dipFps = dips[i], interval = ints[i];
            yield return MicroStutter(baseFps, dipFps, interval, fixedWidth);
        }
    }

    private FRVVariant MicroStutter(int baseFps, int dipFps, int intervalFrames,
                                    bool fixedWidth)
    {
        var baseStride = Mathf.RoundToInt((float)SOURCE_FPS / baseFps);
        var dipStride  = Mathf.RoundToInt((float)SOURCE_FPS / dipFps);
        int[] widths   = fixedWidth ? new[]{3} : new[]{1,3,8};
        string name    = fixedWidth
            ? $"Micro_{baseFps}to{dipFps}_Every{intervalFrames}f_W3f"
            : $"MicroVar_{baseFps}to{dipFps}_Every{intervalFrames}f_W1-3-8f";

        return new FRVVariant
        {
            Name = name,
            StrideForFrame = f =>
            {
                int cycle = f / intervalFrames;
                int off   = f % intervalFrames;
                int w     = widths[cycle % widths.Length];
                return off < w ? dipStride : baseStride;
            }
        };
    }

    private FRVVariant Jitter(int targetFps, int range, int stepPeriod, int seed)
    {
        var r = new System.Random(seed);
        // pre-compute stride table (one entry per step)
        int steps = Mathf.CeilToInt((float)TOTAL_FRAMES / stepPeriod) + 1;
        int[] table = new int[steps];
        int curFps  = targetFps;

        for (int i = 0; i < steps; i++)
        {
            table[i] = Mathf.RoundToInt((float)SOURCE_FPS / curFps);

            int delta = r.Next(3) - 1; // {-1,0,+1}
            curFps = Mathf.Clamp(curFps + delta, targetFps - range, targetFps + range);
        }

        return new FRVVariant
        {
            Name = $"Jitter_{targetFps}_±{range}_Every{stepPeriod}f",
            StrideForFrame = f => table[f / stepPeriod]
        };
    }

    private FRVVariant HardJitter(float dropRatio, int seed)
    {
        var rnd   = new System.Random(seed);
        var drops = new HashSet<int>();
        int totalDrops = Mathf.RoundToInt(dropRatio * TOTAL_FRAMES);
        while (drops.Count < totalDrops)
            drops.Add(rnd.Next(TOTAL_FRAMES));

        return new FRVVariant
        {
            Name = $"HardJitter_Drop{dropRatio:P0}",
            StrideForFrame = f => drops.Contains(f) ? 2 : 1  // skip this frame? stride=2
        };
    }

    #endregion ------------------------------------------------------------------

    // build full import list (length ≤ 300) ------------------------------------
// Put inside FRVPlayer
private int[] BuildPlayedSequence(FRVVariant v)
{
    var played = new List<int>(TOTAL_FRAMES * 4);   // rough upper bound

    for (int src = _startFrame; src <= _lastFrame;  src++)
    {
        int stride = Mathf.Max(1, v.StrideForFrame(src));   // stride depends on *source* index
        for (int h = 0; h < stride; h++)
            played.Add(src);                                // duplicate
    }

    played.Add(played[^1]);   // sentinel for BasePlayer
    return played.ToArray();
}
}
