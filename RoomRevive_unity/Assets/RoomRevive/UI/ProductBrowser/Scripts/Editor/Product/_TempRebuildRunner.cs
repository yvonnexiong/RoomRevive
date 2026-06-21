#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace RoomRevive.ProductBrowser.EditorTools
{
    // TEMPORARY one-shot bootstrap: runs an editor action once per session, synchronously in the
    // reload callback (the MCP menu/run-command paths aren't usable in this session). Deleted when done.
    internal static class _TempRebuildRunner
    {
        [DidReloadScripts]
        static void OnReload()
        {
            if (SessionState.GetBool("RR_SwapRebuild_NoImg5Dots", false)) return;
            SessionState.SetBool("RR_SwapRebuild_NoImg5Dots", true);

            Debug.Log("[_TempRebuildRunner] Running RebuildSwapPanelOnly() (no image, 5 dots)...");
            try
            {
                ProductBrowserPrefabCreator.RebuildSwapPanelOnly();
                Debug.Log("[_TempRebuildRunner] RebuildSwapPanelOnly() completed.");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[_TempRebuildRunner] RebuildSwapPanelOnly() threw: " + e);
            }
        }
    }
}
#endif
