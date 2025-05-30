using System.Runtime.InteropServices;
public class FrameIO
{
    private const string ImporterDllPath = @"E:\Quang\Project\dpc-test\Assets\lib\Importer.dll";


    [DllImport(ImporterDllPath, CallingConvention = CallingConvention.Cdecl)]
    private static extern int count_verts_ply(string filename);
    [DllImport(ImporterDllPath, CallingConvention = CallingConvention.Cdecl)]
    private static extern int count_faces_ply(string filename);
    [DllImport(ImporterDllPath, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool load_point_cloud_ply(string filename, float[] pos, byte[] color);
    [DllImport(ImporterDllPath, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool load_point_cloud_RBGA_ply(string filename, float[] pos, byte[] color);
    [DllImport(ImporterDllPath, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool load_trimesh_from_triangles_only_ply(string filename, float[] pos, float[] uv, int[] indices);

    public class PCreader
    {
        // Return Point Cloud number of vertices
        public static int CountVertices(string filename) { return count_verts_ply(filename); }

        /// <summary>
        /// Import point cloud .ply file knowing number of vertices.
        /// </summary>
        /// <param name="filename">The path to the .ply file to be imported.</param>
        /// <param name="pos">The float array to store position data (x, y, z).</param>
        /// <param name="color">The byte array to store color data (r, g, b, 255).</param>
        /// <remarks>
        /// Supports readable ASCII, binary little endian, binary big endian.
        /// Better performance with binary files.
        /// </remarks>
        /// <returns> True if load .ply file successfull, else false.</returns>
        public static bool LoadPlyFileData(string filename, float[] pos, byte[] color) { return load_point_cloud_ply(filename, pos, color); }
    }
}