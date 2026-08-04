// 

using SekiroTool.Enums;
using SekiroTool.Interfaces;
using SekiroTool.Memory;
using SekiroTool.Utilities;
using static SekiroTool.Memory.Offsets;

namespace SekiroTool.Services;

public class ReminderService(IMemoryService memoryService) : IReminderService
{
    /// <summary>
    /// Icon the reminder swaps in; matches the immediate baked into the ChangeIdolIcon script.
    /// </summary>
    private const ushort ReminderIconId = 0x233;

    public void ChangeIdolIcon() => SetIdolIcon(ReminderIconId);

    /// <summary>
    /// Puts the original icon back, using the value the script saved into the code cave before its
    /// first overwrite. No-ops when the icon was never swapped this session, or when the backup only
    /// holds the reminder icon itself - that means a previous tool run swapped it and this one never
    /// saw the original, so only a game restart can recover it.
    /// </summary>
    public void RestoreIdolIcon()
    {
        if (CodeCaveOffsets.Base == IntPtr.Zero) return;

        var original = memoryService.Read<ushort>(CodeCaveOffsets.Base + CodeCaveOffsets.IdolIconBackup);
        if (original == 0 || original == ReminderIconId) return;

        SetIdolIcon(original);
    }

    private void SetIdolIcon(ushort iconId)
    {
        if (CodeCaveOffsets.Base == IntPtr.Zero) return;

        var backupSlot = CodeCaveOffsets.Base + CodeCaveOffsets.IdolIconBackup;

        // The script always writes whichever icon it replaced into the backup slot, so keep the
        // first value ever captured - that is the only one that is the game's own.
        var alreadyCaptured = memoryService.Read<ushort>(backupSlot);

        var bytes = AsmLoader.GetAsmBytes(AsmScript.ChangeIdolIcon);
        AsmHelper.WriteAbsoluteAddress(bytes, Functions.GetGoodsParam, 0xE + 2);
        AsmHelper.WriteAbsoluteAddress(bytes, backupSlot, 0x27 + 2);
        BitConverter.GetBytes(iconId).CopyTo(bytes, 0x34 + 4);

        memoryService.AllocateAndExecute(bytes);

        if (alreadyCaptured != 0) memoryService.Write(backupSlot, alreadyCaptured);
    }
}