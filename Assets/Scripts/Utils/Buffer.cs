using System.Drawing;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UIElements;

public class BaseFrameBuffer
{
    protected int _numVerts;
    public int NumVerts => _numVerts;
    protected virtual bool Allocation(int n) { return false; }

    // Default constructor
    public BaseFrameBuffer()
    {
        _numVerts = 0;
    }
    public BaseFrameBuffer(int num)
    {
        _numVerts = num;
        if (!Allocation(num))
            Debug.Log($"Failed to allocate memory for {this.GetType().Name}. Array size must be positive.");
    }
}
public class DPCFrameBuffer : BaseFrameBuffer
{
    public float[] vertex;
    public byte[] color;
    protected override bool Allocation(int n)
    {
        if (n <= 0)
            return false;
        vertex = new float[n * 3];
        color = new byte[n * 4];
        return true;
    }
    private bool Allocation(float[] pos, byte[] col)
    {
        if (pos.Length <= 0 || col.Length <= 0 || pos.Length * 4 != col.Length * 3)
            return false;
        _numVerts = col.Length / 3;
        vertex = pos;
        color = col;
        return true;
    }

    // Default constructor
    public DPCFrameBuffer() : base()
    {
        vertex = null;
        color = null;
    }
    public DPCFrameBuffer(int num) : base(num) {}
    public DPCFrameBuffer(float[] pos, byte[] col)
    {
        if (!Allocation(pos, col))
            Debug.Log($"Failed to allocate memory for {this.GetType().Name}. Invalid input array.");
    }
}