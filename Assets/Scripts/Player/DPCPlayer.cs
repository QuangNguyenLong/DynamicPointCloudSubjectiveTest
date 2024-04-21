using UnityEngine;
using UnityEngine.Experimental.AI;

public class DPCPlayer : BasePlayer
{
    [SerializeField] private string _ContentName = "longdress";
    [SerializeField] private int _ContentRate = 5;
    [SerializeField] private int _StartFrame = 0;
    [SerializeField] private int _LastFrame = 299;
    [SerializeField] private int _FrameRate = 30;
    public Vector3 OffSet = new (0f, 0f, 0f);
    public Vector3 Rotation = new(0f, 0f, 0f);

    protected ComputeBuffer _posBuffer;
    protected ComputeBuffer _colorBuffer;

    private DPCHandler _content;
    protected override void Initialize()
    {
        _offset = OffSet;
        _scale = new(0.002f, 0.002f, 0.002f);
        _rotation = Rotation;
        _tint = new(0.6f, 0.6f, 0.6f);
        _content = new DPCHandler(_ContentName, _ContentRate, _StartFrame, _LastFrame, _FrameRate);

        if (_shader == null)
            _shader = Shader.Find("Point_BothEyes");
        _material = new Material(_shader);

        _material.EnableKeyword("_COMPUTE_BUFFER");
        _material.SetColor("_Tint", _tint);
        _material.SetMatrix("_Transform", Matrix4x4.TRS(_offset, Quaternion.Euler(_rotation), _scale));
        Pause();
    }

    protected override int GetStartFrame() => _StartFrame;
    protected override VVHandler GetCurrentContent() => _content;
    protected override void UpdatePosition() { }
    private RenderParams _rp;
    protected override void RenderCurrentFrame()
    {
        _material.SetBuffer("_Positions", _posBuffer);
        _material.SetBuffer("_Colors", _colorBuffer);
        _material.SetMatrix("_Transform", Matrix4x4.TRS(_offset, Quaternion.Euler(_rotation), _scale));
        _material.SetPass(0);

        _rp = new RenderParams(_material);
        _rp.worldBounds = new Bounds(Vector3.zero, 10000 * Vector3.one);
        Graphics.RenderPrimitives(_rp, MeshTopology.Points, _buffer.Peek().NumVerts);
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
        _posBuffer = new ComputeBuffer(_buffer.Peek().NumVerts, 12);
        _colorBuffer = new ComputeBuffer(_buffer.Peek().NumVerts, 4);

        _posBuffer.SetData(_buffer.Peek().vertex);
        _colorBuffer.SetData(_buffer.Peek().color);
    }

    protected override void ImporterNextFrame()
    {
        var inPlayContent = GetCurrentContent();
        string filename = inPlayContent.GetFullPath() + "\\" + inPlayContent.GetContentName() + (_currentImportFrame % (inPlayContent.GetLastFrame() + 1 - inPlayContent.GetStartFrame())).ToString("D4") + ".ply\0";
        int count = FrameIO.PCreader.CountVertices(filename);

        if (count <= 0)
            UnityEngine.Debug.LogError("Fail to import frame. Filename: " + filename);

        DPCFrameBuffer temp = new(count);
        FrameIO.PCreader.LoadPlyFileData(filename, temp.vertex, temp.color);
        _buffer.Enqueue(temp);
    }

    public int FramesLeft { get { return _LastFrame - _currentRenderFrame;  } }

}
