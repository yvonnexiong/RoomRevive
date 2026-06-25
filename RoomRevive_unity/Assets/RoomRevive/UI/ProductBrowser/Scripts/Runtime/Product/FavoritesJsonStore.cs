using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RoomRevive.ProductBrowser
{
    /// <summary>
    /// Persists favorites to <c>HTML_Editor/admin/favorites.json</c> as POINTERS into
    /// <c>catalog.json</c> — never duplicating product data. Each entry stores the catalog item's
    /// stable <c>id</c> (resolved from the product's <see cref="ProductData.catalogKey"/> =
    /// catalog <c>modelKey</c>) plus the modelKey for readability.
    ///
    /// File shape (matches the manual test):
    /// <code>
    /// { "version": 1, "updatedAt": 1782217657000,
    ///   "favorites": [ { "id": "8e17e3187eb2c1e2", "modelKey": "G5611U_brilliantwhite_realsize" } ] }
    /// </code>
    ///
    /// Editor / prototype scope: writes the repo file directly via a path relative to the project
    /// (same resolution the catalog sync uses). In a built player that path won't exist, so writes
    /// are skipped with a warning rather than throwing.
    /// </summary>
    public static class FavoritesJsonStore
    {
        // .../RoomRevive_unity/Assets → up two → .../RoomRevive/HTML_Editor/admin
        static string AdminDir   => Path.GetFullPath(Path.Combine(Application.dataPath, "../../HTML_Editor/admin"));
        static string FavPath    => Path.Combine(AdminDir, "favorites.json");
        static string CatalogPath => Path.Combine(AdminDir, "catalog.json");

        // ── Public API ───────────────────────────────────────────────────────

        public static bool IsFavorited(ProductData product)
        {
            string key = product != null ? product.catalogKey : null;
            if (string.IsNullOrEmpty(key)) return false;
            FavFile file = Load();
            return file.favorites.Exists(e => e.modelKey == key);
        }

        /// <summary>Adds or removes the product's pointer in favorites.json. Returns true on a successful write.</summary>
        public static bool SetFavorited(ProductData product, bool favorited)
        {
            if (product == null) return false;

            string key = product.catalogKey;
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning($"[FavoritesJsonStore] '{Name(product)}' has no catalogKey — it isn't linked " +
                                 "to catalog.json, so it can't be saved as a pointer.");
                return false;
            }

            if (!Directory.Exists(AdminDir))
            {
                Debug.LogWarning($"[FavoritesJsonStore] Admin folder not found ({AdminDir}) — favorite not written " +
                                 "(expected in a built player; works in the editor).");
                return false;
            }

            FavFile file = Load();
            file.favorites.RemoveAll(e => e.modelKey == key); // de-dupe / handle un-favorite

            if (favorited)
                file.favorites.Add(new FavEntry { id = ResolveCatalogId(key), modelKey = key });

            file.version = 1;
            file.updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            try
            {
                File.WriteAllText(FavPath, JsonUtility.ToJson(file, true));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[FavoritesJsonStore] Failed to write favorites.json: {e.Message}");
                return false;
            }
        }

        // ── Internals ────────────────────────────────────────────────────────

        static FavFile Load()
        {
            try
            {
                if (File.Exists(FavPath))
                {
                    FavFile f = JsonUtility.FromJson<FavFile>(File.ReadAllText(FavPath));
                    if (f != null)
                    {
                        f.favorites ??= new List<FavEntry>();
                        return f;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FavoritesJsonStore] Could not read favorites.json, starting fresh: {e.Message}");
            }
            return new FavFile();
        }

        /// <summary>Resolves a catalog item's stable id from its product modelKey. Returns "" if not found.</summary>
        static string ResolveCatalogId(string modelKey)
        {
            try
            {
                if (!File.Exists(CatalogPath)) return "";
                CatRoot root = JsonUtility.FromJson<CatRoot>(File.ReadAllText(CatalogPath));
                if (root?.items == null) return "";
                foreach (CatItem it in root.items)
                    if (it?.product != null && it.product.modelKey == modelKey)
                        return it.id ?? "";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FavoritesJsonStore] Could not resolve catalog id for '{modelKey}': {e.Message}");
            }
            return "";
        }

        static string Name(ProductData p) =>
            !string.IsNullOrEmpty(p.productName) ? p.productName : p.name;

        // ── JSON DTOs ──────────────────────────────────────────────────────────
        [Serializable] class FavFile { public int version = 1; public long updatedAt; public List<FavEntry> favorites = new(); }
        [Serializable] class FavEntry { public string id; public string modelKey; }

        [Serializable] class CatRoot { public CatItem[] items; }
        [Serializable] class CatItem { public string id; public CatProduct product; }
        [Serializable] class CatProduct { public string modelKey; }
    }
}
