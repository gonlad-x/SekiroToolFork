using SekiroTool.Enums;
using SekiroTool.Interfaces;

namespace SekiroTool.Memory
{
    public class HookManager
    {
        private readonly Dictionary<nint, HookData> _hookRegistry = new();
        private readonly IMemoryService _memoryService;

        public HookManager(IMemoryService memoryService, IStateService stateService)
        {
            _memoryService = memoryService;
            stateService.Subscribe(State.Detached, OnGameDetached);
        }

        private class HookData
        {
            public nint OriginAddr { get; set; }
            public nint CaveAddr { get; set; }
            public byte[] OriginalBytes { get; set; }
        }

        public void InstallHook(nint codeLoc, nint origin, byte[] originalBytes)
        {
            byte[] hookBytes = GetHookBytes(originalBytes.Length, codeLoc, origin);
            _memoryService.WriteBytes(origin, hookBytes);
            _hookRegistry[codeLoc] = new HookData
            {
                CaveAddr = codeLoc,
                OriginAddr = origin,
                OriginalBytes = originalBytes
            };
        }

        private byte[] GetHookBytes(int originalBytesLength, nint target, nint origin)
        {
            byte[] hookBytes = new byte[originalBytesLength];
            hookBytes[0] = 0xE9;

            int jumpOffset = (int)(target - (origin + 5));
            byte[] offsetBytes = BitConverter.GetBytes(jumpOffset);
            Array.Copy(offsetBytes, 0, hookBytes, 1, 4);

            for (int i = 5; i < hookBytes.Length; i++)
            {
                hookBytes[i] = 0x90;
            }

            return hookBytes;
        }

        public void UninstallHook(nint key)
        {
            if (!_hookRegistry.TryGetValue(key, out HookData hookToUninstall)) return;

            _memoryService.WriteBytes(hookToUninstall.OriginAddr, hookToUninstall.OriginalBytes);
            _hookRegistry.Remove(key);
        }

        /// <summary>
        /// Cave addresses of every currently installed hook. Lets a caller uninstall a subset
        /// (see RunModeService, which keeps the hooks that are legal during a run).
        /// </summary>
        public IReadOnlyList<nint> InstalledHookKeys => _hookRegistry.Keys.ToList();

        /// <summary>
        /// Origin address and original bytes for a currently installed hook, if any. Read-only -
        /// lets a caller (RunModeService) snapshot enough to reinstall the hook later via
        /// InstallHook without needing to know that option's specific parameters again.
        /// </summary>
        public bool TryGetHookInstallData(nint key, out nint origin, out byte[] originalBytes)
        {
            if (_hookRegistry.TryGetValue(key, out var data))
            {
                origin = data.OriginAddr;
                originalBytes = data.OriginalBytes;
                return true;
            }

            origin = default;
            originalBytes = [];
            return false;
        }

        public void ClearHooks()
        {
            _hookRegistry.Clear();
        }

        public void UninstallAllHooks()
        {
            foreach (var key in _hookRegistry.Keys.ToList())
            {
                UninstallHook(key);
            }
        }
        
        private void OnGameDetached()
        {
            _hookRegistry.Clear();
        }
    }
}