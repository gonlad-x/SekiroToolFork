using System.Runtime.InteropServices;
using SekiroTool.Enums;
using SekiroTool.Interfaces;
using SekiroTool.Memory;
using SekiroTool.Models;
using SekiroTool.Utilities;
using static SekiroTool.Memory.Offsets;

namespace SekiroTool.Services;

public class TravelService(IMemoryService memoryService, HookManager hookManager) : ITravelService
{
    private readonly Dictionary<int, int> _idolsByAreaIndex = DataLoader.GetIdolIdsByAreaIndexDictionary();

    public bool TryResolveIdol(int areaIndex, out int idolId) =>
        _idolsByAreaIndex.TryGetValue(areaIndex, out idolId);

    public void Warp(Warp warp)
    {
        if (warp.HasCoordinates)
        {
            WarpWithCoords(warp.Coords, warp.Angle, warp.IdolId);
            return;
        }

        WarpToIdol(warp.IdolId);
    }

    public void WarpWithCoords(float[] coords, float angle, int idolId) =>
        WarpWithCoords(MemoryMarshal.AsBytes(coords.AsSpan()).ToArray(), angle, idolId);

    public void WarpWithCoords(byte[] xyzBytes, float angle, int idolId)
    {
        WarpToIdol(idolId);

        var coordWriteHook = Hooks.SetWarpCoordinates;
        var angleWriteHook = Hooks.SetWarpAngle;

        var coordLoc = CodeCaveOffsets.Base + CodeCaveOffsets.WarpCoords;
        var coordWriteCode = CodeCaveOffsets.Base + CodeCaveOffsets.WarpCoordsCode;
        
        memoryService.WriteBytes(coordLoc, xyzBytes);
        
        var codeBytes = AsmLoader.GetAsmBytes(AsmScript.WarpCoordWrite);
        
        var bytes = AsmHelper.GetRelOffsetBytes(coordWriteCode, coordLoc, 7);
        Array.Copy(bytes, 0, codeBytes, 0x0 + 3, bytes.Length);
        memoryService.WriteBytes(coordWriteCode, codeBytes);

        var angleLoc = CodeCaveOffsets.Base + CodeCaveOffsets.WarpAngle;
        
        var angleWriteCode = CodeCaveOffsets.Base + CodeCaveOffsets.WarpAngleCode;
        
        var angleToWrite = new float[] { 0f, angle, 0f, 0f };
        memoryService.WriteBytes(angleLoc, MemoryMarshal.AsBytes(angleToWrite.AsSpan()).ToArray());
        
        codeBytes = AsmLoader.GetAsmBytes(AsmScript.WarpAngleWrite);
        bytes = AsmHelper.GetRelOffsetBytes(angleWriteCode, angleLoc, 7);
        Array.Copy(bytes, 0, codeBytes, 0x0 + 3, bytes.Length);
        memoryService.WriteBytes(angleWriteCode, codeBytes);

        hookManager.InstallHook(coordWriteCode, coordWriteHook,
            [0x66, 0x0F, 0x7F, 0x80, 0xC0, 0x0A, 0x00, 0x00]);
        hookManager.InstallHook(angleWriteCode, angleWriteHook,
            [0x66, 0x0F, 0x7F, 0x80, 0xD0, 0x0A, 0x00, 0x00]);

        var isGameLoadedPtr = memoryService.Read<nint>(MenuMan.Base) + MenuMan.IsLoaded;
        {
            int start = Environment.TickCount;
            while (memoryService.Read<byte>(isGameLoadedPtr) != 0 &&
                   Environment.TickCount < start + 10000) 
                Thread.Sleep(50);
        }
        
        {
            int start = Environment.TickCount;
          
            while (memoryService.Read<byte>(isGameLoadedPtr) != 1 &&
                   Environment.TickCount < start + 10000) 
                Thread.Sleep(50);
        }
        
        hookManager.UninstallHook(coordWriteCode);
        hookManager.UninstallHook(angleWriteCode);
    }
        
    private void WarpToIdol(int idolId)
    {
        var bytes = AsmLoader.GetAsmBytes(AsmScript.Warp);
        AsmHelper.WriteAbsoluteAddresses(bytes, [
            (idolId, 0x0 + 2),
            (Functions.Warp, 0x10 + 2)
        ]);
        memoryService.AllocateAndExecute(bytes);
    }
}