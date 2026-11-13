using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SteamKit2.Internal;

namespace STS2Mobile.Steam.Workshop;

internal sealed record SteamWorkshopUpdateItem(
    PublishedFileDetails Detail,
    SteamWorkshopPreparedRevision Revision,
    SteamWorkshopInstallation Installed,
    bool CanReuse,
    string Error
)
{
    internal ulong Id => Detail.publishedfileid;
    internal string Title => string.IsNullOrWhiteSpace(Detail.title) ? Id.ToString() : Detail.title.Trim();
    internal bool NeedsDownload => Revision != null && !CanReuse;

    internal static SteamWorkshopUpdateItem Prepare(
        PublishedFileDetails detail,
        CPublishedFile_GetItemInfo_Response.WorkshopItemInfo info,
        SteamWorkshopInstallation installed,
        string gameBranch,
        SteamWorkshopBranchOrder branchOrder = null)
    {
        try
        {
            var revision = SteamWorkshopRevisionResolver.Resolve(detail, info, gameBranch, branchOrder);
            var canReuse = SteamWorkshopRevisionResolver.MatchesInstallation(installed, revision)
                && !string.IsNullOrWhiteSpace(installed.Generation)
                && Directory.Exists(installed.Directory);
            return new(detail, revision, installed, canReuse, "");
        }
        catch (InvalidOperationException ex)
        {
            return new(detail, null, installed, false, ex.Message);
        }
    }
}

internal sealed record SteamWorkshopUpdatePlan(
    IReadOnlyList<SteamWorkshopUpdateItem> Items,
    IReadOnlyList<SteamWorkshopSyncManifestItem> PotentialRemovals,
    SteamWorkshopSyncMetadata Metadata,
    IReadOnlyCollection<ulong> SubscribedItemIds,
    IReadOnlyDictionary<ulong, IReadOnlyCollection<ulong>> DependencyParents)
{
    internal bool NeedsConfirmation => Items.Any(item => item.NeedsDownload) || PotentialRemovals.Count > 0;
}
