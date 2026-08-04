using SekiroTool.Models;

namespace SekiroTool.Interfaces;

public interface ITravelService
{
    void Warp(Warp warp);
    void WarpWithCoords(float[] coords, float angle, int idolId);
    void WarpWithCoords(byte[] xyzBytes, float angle, int idolId);
    bool TryResolveIdol(int areaIndex, out int idolId);
}