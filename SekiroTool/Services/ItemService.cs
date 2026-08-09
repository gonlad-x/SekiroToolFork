using System.Diagnostics;
using SekiroTool.Enums;
using SekiroTool.Interfaces;
using SekiroTool.Memory;
using SekiroTool.Models;
using SekiroTool.Utilities;
using static SekiroTool.Memory.Offsets;

namespace SekiroTool.Services;

public class ItemService(IMemoryService memoryService) : IItemService
{
    public void SpawnItem(Item item, int quantity)
    {
        var structPtr = CodeCaveOffsets.Base + CodeCaveOffsets.ItemStruct;
        var code = CodeCaveOffsets.Base + CodeCaveOffsets.ItemGiveCode;


        var bytes = AsmLoader.GetAsmBytes(AsmScript.GiveItem);

        AsmHelper.WriteRelativeOffsets(bytes, [
            (code + 0x4, MapItemMan.Base, 7, 0x4 + 3),
            (code + 0xB, structPtr, 7, 0xB + 3),
            (code + 0x22, Functions.ItemSpawn, 5, 0x22 + 1)
        ]);
        
        memoryService.Write(structPtr, 1);
        memoryService.Write(structPtr + 0x4, (short) item.ItemId);
        memoryService.Write(structPtr + 0x6, item.ItemType);
        memoryService.Write(structPtr + 0x8, quantity);
        memoryService.WriteBytes(code, bytes);

        memoryService.RunThread(code);
    }

    public void GiveSkillOrPros(int id)
    {
        var bytes = AsmLoader.GetAsmBytes(AsmScript.GiveSkillsAndPros);
        AsmHelper.WriteAbsoluteAddresses(bytes, [
            ( id, 0x4 + 2),
            (Functions.GiveSkillAndPros, 0x1A + 2)
        ]);
        
        memoryService.AllocateAndExecute(bytes);
    }

    public void RemoveItem(int id)
    {

        var bytes = AsmLoader.GetAsmBytes(AsmScript.RemoveItem);

        AsmHelper.WriteImmediateDword(bytes, ChrIns.PlayerGameDataOffsets.EquipInventoryData,0xE + 3 );

        AsmHelper.WriteAbsoluteAddresses(bytes, [
            (GameDataMan.Base, 0x0 + 2),
            (id, 0x18 + 2),
            (Functions.GetItemSlot, 0x2F + 2),
            (Functions.RemoveItem, 0x50 + 2)
        ]);
        memoryService.AllocateAndExecute(bytes);
    }

    // Shares its prologue with RemoveItem (same GetItemSlot call), but writes the raw slot index to a
    // scratch offset instead of proceeding to remove it. Slot >= 0 is treated as "found" -- this matches
    // the conventional GetItemSlot-style not-found sentinel used elsewhere in this codebase family, but
    // has not been independently confirmed for Sekiro's GetItemSlot specifically. Verify in-game before
    // relying on this for anything beyond a manual check.
    //
    // GetItemSlot's "item id" argument is a combined 32-bit value (category/type in the high 16 bits,
    // numeric id in the low 16 bits) -- NOT the bare numeric id. RemoveItem (the only other caller of
    // GetItemSlot in this codebase) only ever passes skill/prosthetic ids, where type is implicitly 0,
    // so it never needed to combine anything; that masked this requirement until HasItem was tested
    // against a Goods item (type 0x4000). Unverified beyond one live test -- flag if it's still wrong.
    public bool HasItem(int id, short itemType)
    {
        var bytes = AsmLoader.GetAsmBytes(AsmScript.HasItem);

        AsmHelper.WriteImmediateDword(bytes, ChrIns.PlayerGameDataOffsets.EquipInventoryData, 0xE + 3);

        int combinedId = (itemType << 16) | (id & 0xFFFF);

        AsmHelper.WriteAbsoluteAddresses(bytes, [
            (GameDataMan.Base, 0x0 + 2),
            (combinedId, 0x18 + 2),
            (Functions.GetItemSlot, 0x2F + 2),
            (CodeCaveOffsets.Base + CodeCaveOffsets.HasItemResult, 0x3F + 2)
        ]);
        memoryService.AllocateAndExecute(bytes);

        int slot = memoryService.Read<int>(CodeCaveOffsets.Base + CodeCaveOffsets.HasItemResult);
        return slot >= 0;
    }
}