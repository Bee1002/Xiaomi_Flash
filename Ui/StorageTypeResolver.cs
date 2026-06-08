using System;
using System.Collections.Generic;

namespace Xiaomi_Flash.Ui
{
    internal static class StorageTypeResolver
    {
        public static string? Resolve(Dictionary<string, string> vars)
        {
            string? explicitType = FastbootVarHelper.FirstVar(vars, "storage-type", "storage_type", "mmc_type");
            if (!string.IsNullOrWhiteSpace(explicitType))
            {
                string? normalized = NormalizeToken(explicitType);
                if (normalized != null)
                    return normalized;
            }

            if (vars.TryGetValue("variant", out string? variant))
            {
                string? fromVariant = FromVariant(variant);
                if (fromVariant != null)
                    return fromVariant;
            }

            if (vars.TryGetValue("ufs", out string? ufsFlag)
                && ufsFlag.Equals("yes", StringComparison.OrdinalIgnoreCase))
                return "UFS";

            if (FastbootVarHelper.FirstVar(vars, "ufs_version", "ufs-version", "storage-vendor", "storage-model") != null)
                return "UFS";

            bool hasMmc = false;
            bool hasUfsPartition = false;

            foreach (string key in vars.Keys)
            {
                string lower = key.ToLowerInvariant();
                if (lower.Contains("mmcblk0"))
                    hasMmc = true;
                if (lower.Contains("ufs") && !lower.Equals("ufs"))
                    hasUfsPartition = true;
            }

            if (hasMmc && !hasUfsPartition)
                return "eMMC";

            string? storageValue = FastbootVarHelper.FirstVar(vars, "storage");
            if (!string.IsNullOrWhiteSpace(storageValue))
            {
                string? normalized = NormalizeToken(storageValue);
                if (normalized != null)
                    return normalized;
            }

            return null;
        }

        public static string? FromVariant(string? variant)
        {
            if (string.IsNullOrWhiteSpace(variant))
                return null;

            if (variant.Equals("NA", StringComparison.OrdinalIgnoreCase))
                return null;

            return NormalizeToken(variant);
        }

        public static string? NormalizeToken(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            string compact = raw.Replace(" ", "").Replace("_", "").Replace("-", "");
            if (compact.IndexOf("UFS", StringComparison.OrdinalIgnoreCase) >= 0)
                return "UFS";

            if (compact.IndexOf("EMMC", StringComparison.OrdinalIgnoreCase) >= 0)
                return "eMMC";

            if (compact.Equals("MMC", StringComparison.OrdinalIgnoreCase)
                || compact.StartsWith("MMC", StringComparison.OrdinalIgnoreCase))
                return "eMMC";

            return null;
        }

    }
}
