using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct DebugData
{
    public int raysShotCount;
    public int rayTriangleTestCount;
    public int rayTriangleIntersectionCount;
    public int rayVolumeTestCount;
}