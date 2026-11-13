using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2.Internal;

namespace STS2Mobile.Steam.Workshop;

internal sealed class SteamWorkshopDownloader : IDisposable
{
    private const long MaxWorkshopDownloadBytes = 2L * 1024L * 1024L * 1024L;
    private static readonly Regex StaleDownloadFileNamePattern = new(
        @"^\.\d+\.[0-9a-fA-F]{32}\.download$",
        RegexOptions.CultureInvariant
    );
    private static readonly Regex StaleDownloadDirectoryNamePattern = new(
        @"^(?<id>\d+)\.(?<kind>tmp|old)-[0-9a-fA-F]{32}$",
        RegexOptions.CultureInvariant
    );

    private readonly SteamConnection _connection;
    private readonly string _downloadsDirectory;
    private readonly HttpClient _http;
    private DepotDownloader _depotDownloader;

    internal SteamWorkshopDownloader(SteamConnection connection, string downloadsDirectory)
    {
        _connection = connection;
        _downloadsDirectory = Path.GetFullPath(downloadsDirectory);
        _http = AndroidJavaHttpMessageHandler.CreateCdnClient();
    }

    internal event Action<string> LogMessage;
    internal event Action<string> StatusChanged;

    internal Task<IReadOnlyList<SteamWorkshopItem>> DownloadAsync(
        IReadOnlyList<PublishedFileDetails> details,
        IReadOnlyDictionary<ulong, CPublishedFile_GetItemInfo_Response.WorkshopItemInfo> itemInfo,
        IReadOnlyCollection<ulong> subscribedItemIds,
        IReadOnlyDictionary<ulong, IReadOnlyCollection<ulong>> dependencyParents,
        string gameBranch,
        IReadOnlyDictionary<ulong, SteamWorkshopSyncManifestItem> previousItems = null,
        CancellationToken ct = default,
        SteamWorkshopBranchOrder gameBranchOrder = null)
    {
        var items = (details ?? Array.Empty<PublishedFileDetails>()).Select(detail =>
        {
            CPublishedFile_GetItemInfo_Response.WorkshopItemInfo info = null;
            SteamWorkshopSyncManifestItem previous = null;
            itemInfo?.TryGetValue(detail.publishedfileid, out info);
            previousItems?.TryGetValue(detail.publishedfileid, out previous);
            return SteamWorkshopUpdateItem.Prepare(detail, info, previous?.Installed, gameBranch, gameBranchOrder);
        }).ToArray();
        return DownloadAsync(items, subscribedItemIds, dependencyParents, ct);
    }

    internal async Task<IReadOnlyList<SteamWorkshopItem>> DownloadAsync(
        IReadOnlyList<SteamWorkshopUpdateItem> plan,
        IReadOnlyCollection<ulong> subscribedItemIds,
        IReadOnlyDictionary<ulong, IReadOnlyCollection<ulong>> dependencyParents,
        CancellationToken ct)
    {
        Directory.CreateDirectory(_downloadsDirectory);
        var downloadCount = plan.Count(item => item.NeedsDownload);
        if (downloadCount > 0)
            CleanupStaleDownloadArtifacts();

        var items = new List<SteamWorkshopItem>(plan.Count);
        var current = 0;
        foreach (var item in plan)
        {
            ct.ThrowIfCancellationRequested();
            var progress = "";
            if (item.NeedsDownload)
            {
                progress = $"Downloading {++current}/{downloadCount} · {item.Title}";
                StatusChanged?.Invoke(progress);
            }
            items.Add(await DownloadItemAsync(item, subscribedItemIds, dependencyParents, progress, ct).ConfigureAwait(false));
        }

        return items;
    }

    private async Task<SteamWorkshopItem> DownloadItemAsync(
        SteamWorkshopUpdateItem item,
        IReadOnlyCollection<ulong> subscribedItemIds,
        IReadOnlyDictionary<ulong, IReadOnlyCollection<ulong>> dependencyParents,
        string progress,
        CancellationToken ct)
    {
        var detail = item.Detail;
        var id = item.Id;
        var title = item.Title;
        var revision = item.Revision;
        var manifestId = revision?.ManifestId ?? 0;
        var itemDirectory = Path.Combine(_downloadsDirectory, id.ToString());
        var isDependency = subscribedItemIds == null || !subscribedItemIds.Contains(id);
        var requiredByPublishedFileIds = dependencyParents != null && dependencyParents.TryGetValue(id, out var parents)
            ? parents
            : Array.Empty<ulong>();

        var source = DownloadSource.Unknown;
        try
        {
            if (revision == null)
                throw new InvalidOperationException(item.Error);

            if (item.CanReuse)
            {
                source = revision.ManifestId != 0 || !string.IsNullOrWhiteSpace(detail.file_url)
                    ? await ResolveDownloadSourceAsync(detail, revision).ConfigureAwait(false)
                    : new DownloadSource("", "", DeclaredDownloadSize(detail.file_size), "ugc-hcontent", revision.HContentFile, null);
                return DownloadedItem(detail, title, itemDirectory, manifestId, source, revision,
                    isDependency, requiredByPublishedFileIds, reusedCachedDownload: true);
            }

            source = await ResolveDownloadSourceAsync(detail, revision).ConfigureAwait(false);
            if (source.UnsupportedReason != null)
                return Skipped(
                    detail,
                    manifestId,
                    source.UnsupportedReason,
                    isDependency,
                    requiredByPublishedFileIds,
                    source
                );

            if (string.Equals(source.Kind, "depot-manifest", StringComparison.OrdinalIgnoreCase))
            {
                await DownloadDepotManifestAsync(
                    detail,
                    title,
                    manifestId,
                    itemDirectory,
                    progress,
                    ct
                ).ConfigureAwait(false);
                return DownloadedItem(
                    detail,
                    title,
                    itemDirectory,
                    manifestId,
                    source,
                    revision,
                    isDependency,
                    requiredByPublishedFileIds,
                    reusedCachedDownload: false
                );
            }

            Log($"Downloading {title} ({id})");
            var tempFile = await DownloadToTempFileAsync(
                id,
                source.Url,
                source.ExpectedSize,
                progress,
                ct
            ).ConfigureAwait(false);

            StatusChanged?.Invoke($"Preparing {title}…");
            ReplaceDownloadedContent(tempFile, source.FileName, itemDirectory);
            Log($"Downloaded {title} ({id}) to {itemDirectory}");

            return DownloadedItem(
                detail,
                title,
                itemDirectory,
                manifestId,
                source,
                revision,
                isDependency,
                requiredByPublishedFileIds,
                reusedCachedDownload: false
            );
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log($"Failed to download {title} ({id}): {ex.Message}");
            return new SteamWorkshopItem(
                id,
                title,
                itemDirectory,
                revision?.TimeUpdated ?? detail.time_updated,
                manifestId,
                revision == null ? "preparation-failed" : "download-failed",
                ex.Message,
                "",
                source.Kind,
                source.ExpectedSize,
                source.HContentFile,
                isDependency,
                requiredByPublishedFileIds
            );
        }
    }

    private async Task<DownloadSource> ResolveDownloadSourceAsync(
        PublishedFileDetails detail,
        SteamWorkshopPreparedRevision revision
    )
    {
        if (revision.ManifestId != 0)
            return new DownloadSource("", "", 0, "depot-manifest", revision.HContentFile, null);

        if (!string.IsNullOrWhiteSpace(detail.file_url))
        {
            return new DownloadSource(
                detail.file_url,
                FileNameFromDetail(detail),
                DeclaredDownloadSize(detail.file_size),
                "direct-url",
                revision.HContentFile,
                null
            );
        }

        if (revision.HContentFile != 0)
        {
            try
            {
                var ugc = await _connection.GetWorkshopUgcDetailsAsync(revision.HContentFile)
                    .ConfigureAwait(false);
                return new DownloadSource(
                    ugc.URL,
                    FileNameFromUgcOrDetail(ugc.FileName, detail),
                    ugc.FileSize,
                    "ugc-hcontent",
                    revision.HContentFile,
                    null
                );
            }
            catch (Exception ex)
            {
                PatchHelper.Log(
                    $"[Workshop] UGC details unavailable for {detail.publishedfileid} and no depot manifest was exposed: {ex.Message}"
                );
                return new DownloadSource(
                    "",
                    "",
                    DeclaredDownloadSize(detail.file_size),
                    "no-download-source",
                    revision.HContentFile,
                    "Steam exposed a legacy Workshop UGC handle but RequestUGCDetails did not resolve a download URL"
                );
            }
        }

        return new DownloadSource(
            "",
            "",
            DeclaredDownloadSize(detail.file_size),
            "no-download-source",
            revision.HContentFile,
            "Steam did not expose a Workshop download URL or UGC handle"
        );
    }

    private async Task DownloadDepotManifestAsync(
        PublishedFileDetails detail,
        string title,
        ulong manifestId,
        string itemDirectory,
        string progress,
        CancellationToken ct
    )
    {
        Log($"Downloading depot-manifest Workshop content for {title} ({detail.publishedfileid}) manifest={manifestId}");
        if (_depotDownloader == null)
        {
            _depotDownloader = new DepotDownloader(_connection, _downloadsDirectory, SteamGameBranch.Public);
            _depotDownloader.LogMessage += Log;
        }

        void Report(DepotDownloader.DownloadProgress update)
            => update.ApplyTo((_, size) => StatusChanged?.Invoke($"{progress} · {size}"), _ => { });

        _depotDownloader.ProgressChanged += Report;
        try
        {
            await _depotDownloader.DownloadWorkshopDepotManifestAsync(manifestId, itemDirectory, title, ct)
                .ConfigureAwait(false);
        }
        finally
        {
            _depotDownloader.ProgressChanged -= Report;
        }
    }

    private static SteamWorkshopItem DownloadedItem(
        PublishedFileDetails detail,
        string title,
        string itemDirectory,
        ulong manifestId,
        DownloadSource source,
        SteamWorkshopPreparedRevision revision,
        bool isDependency,
        IReadOnlyCollection<ulong> requiredByPublishedFileIds,
        bool reusedCachedDownload
    )
        => new(
            detail.publishedfileid,
            title,
            itemDirectory,
            revision.TimeUpdated,
            manifestId,
            "downloaded",
            "",
            source.Url,
            source.Kind,
            source.ExpectedSize,
            source.HContentFile,
            isDependency,
            requiredByPublishedFileIds,
            reusedCachedDownload,
            revision.RequiredPublishedFileIds,
            revision.SelectedGameBranch,
            revision.Provenance,
            revision.BranchSupport
        );

    private void CleanupStaleDownloadArtifacts()
    {
        foreach (var file in Directory.EnumerateFiles(_downloadsDirectory).ToArray())
        {
            var name = Path.GetFileName(file);
            if (!StaleDownloadFileNamePattern.IsMatch(name))
                continue;

            DeleteDownloadFile(file);
        }

        foreach (var directory in Directory.EnumerateDirectories(_downloadsDirectory).ToArray())
        {
            var name = Path.GetFileName(directory);
            var match = StaleDownloadDirectoryNamePattern.Match(name);
            if (!match.Success)
                continue;

            if (string.Equals(match.Groups["kind"].Value, "old", StringComparison.OrdinalIgnoreCase))
                RestoreOrDeleteDownloadBackup(directory, match.Groups["id"].Value);
            else
                DeleteDownloadDirectory(directory);
        }
    }

    private void DeleteDownloadFile(string file)
    {
        var fullFile = Path.GetFullPath(file);
        if (!IsPathInsideDirectory(fullFile, _downloadsDirectory))
            throw new InvalidOperationException(
                $"Workshop download temp file escapes downloads root: {fullFile}"
            );

        DeleteFileQuietly(fullFile);
        Log($"Removed stale Workshop download temp file {Path.GetFileName(fullFile)}");
    }

    private void DeleteDownloadDirectory(string directory)
    {
        var fullDirectory = Path.GetFullPath(directory);
        if (!IsPathInsideDirectory(fullDirectory, _downloadsDirectory))
            throw new InvalidOperationException(
                $"Workshop download temp directory escapes downloads root: {fullDirectory}"
            );

        DeleteQuietly(fullDirectory);
        Log($"Removed stale Workshop download temp directory {Path.GetFileName(fullDirectory)}");
    }

    private void RestoreOrDeleteDownloadBackup(string directory, string publishedFileId)
    {
        var fullDirectory = Path.GetFullPath(directory);
        if (!IsPathInsideDirectory(fullDirectory, _downloadsDirectory))
            throw new InvalidOperationException(
                $"Workshop download temp directory escapes downloads root: {fullDirectory}"
            );

        var activeDirectory = Path.Combine(_downloadsDirectory, publishedFileId);
        if (TryMoveDirectory(fullDirectory, activeDirectory))
        {
            Log($"Restored stale Workshop download backup for {publishedFileId}");
            return;
        }

        DeleteDownloadDirectory(fullDirectory);
    }

    private static bool IsPathInsideDirectory(string path, string directory)
    {
        var fullPath = Path.GetFullPath(path);
        var fullDirectory = Path.GetFullPath(directory);
        if (!fullDirectory.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            fullDirectory += Path.DirectorySeparatorChar;

        return fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> DownloadToTempFileAsync(
        ulong publishedFileId,
        string url,
        long expectedSize,
        string progress,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Workshop download URL is missing");

        if (expectedSize > MaxWorkshopDownloadBytes)
            throw new InvalidOperationException(
                $"Workshop item is too large ({expectedSize} bytes)"
            );

        var tempFile = Path.Combine(
            _downloadsDirectory,
            $".{publishedFileId}.{Guid.NewGuid():N}.download"
        );
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await _http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                ct
            ).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength.HasValue && contentLength.Value > MaxWorkshopDownloadBytes)
                throw new InvalidOperationException(
                    $"Workshop item is too large ({contentLength.Value} bytes)"
                );

            await using var input = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var output = new FileStream(
                tempFile,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None
            );
            var totalBytes = contentLength.GetValueOrDefault(expectedSize);
            var lastReport = Environment.TickCount64;
            var downloadedBytes = await CopyWithSizeLimitAsync(input, output, bytes =>
            {
                var now = Environment.TickCount64;
                if (now - lastReport < 250)
                    return;
                lastReport = now;
                var size = $"{bytes / (1024.0 * 1024):F1} MB";
                if (totalBytes > 0)
                    size += $" / {totalBytes / (1024.0 * 1024):F1} MB ({100.0 * bytes / totalBytes:F0}%)";
                StatusChanged?.Invoke($"{progress} · {size}");
            }, ct)
                .ConfigureAwait(false);
            if (expectedSize > 0 && downloadedBytes != expectedSize)
                throw new InvalidOperationException(
                    $"Workshop item downloaded size mismatch: expected {expectedSize} bytes but received {downloadedBytes} bytes"
                );

            return tempFile;
        }
        catch
        {
            DeleteFileQuietly(tempFile);
            throw;
        }
    }

    private static async Task<long> CopyWithSizeLimitAsync(
        Stream input,
        Stream output,
        Action<long> reportProgress,
        CancellationToken ct
    )
    {
        var buffer = new byte[128 * 1024];
        long total = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, ct).ConfigureAwait(false);
            if (read == 0)
                return total;

            total += read;
            if (total > MaxWorkshopDownloadBytes)
                throw new InvalidOperationException(
                    $"Workshop item is too large (over {MaxWorkshopDownloadBytes} bytes)"
                );

            await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            reportProgress(total);
        }
    }

    private static void ReplaceDownloadedContent(
        string tempFile,
        string fileName,
        string itemDirectory
    )
    {
        var tempDirectory = itemDirectory + ".tmp-" + Guid.NewGuid().ToString("N");
        var backupDirectory = itemDirectory + ".old-" + Guid.NewGuid().ToString("N");

        Directory.CreateDirectory(tempDirectory);
        try
        {
            if (IsZipFile(fileName, tempFile))
                ExtractZipSafely(tempFile, tempDirectory);
            else
                File.Move(tempFile, Path.Combine(tempDirectory, SafeFileName(fileName)), overwrite: true);

            var backupCreated = TryMoveDirectory(itemDirectory, backupDirectory);

            Directory.Move(tempDirectory, itemDirectory);
            if (backupCreated)
                DeleteQuietly(backupDirectory);
        }
        catch
        {
            DeleteQuietly(tempDirectory);
            TryMoveDirectory(backupDirectory, itemDirectory);
            throw;
        }
        finally
        {
            DeleteFileQuietly(tempFile);
        }
    }

    private static bool IsZipFile(string fileName, string path)
    {
        if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return true;

        Span<byte> header = stackalloc byte[4];
        using var file = File.OpenRead(path);
        return file.Read(header) == 4
            && header[0] == 0x50
            && header[1] == 0x4B
            && header[2] == 0x03
            && header[3] == 0x04;
    }

    private static void ExtractZipSafely(string zipPath, string targetDirectory)
    {
        var root = Path.GetFullPath(targetDirectory);
        if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            root += Path.DirectorySeparatorChar;

        long extractedBytes = 0;
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.FullName))
                continue;

            var destination = Path.GetFullPath(Path.Combine(root, entry.FullName));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Unsafe Workshop zip entry: {entry.FullName}");

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            extractedBytes += entry.Length;
            if (extractedBytes > MaxWorkshopDownloadBytes)
                throw new InvalidOperationException(
                    $"Workshop zip expands over {MaxWorkshopDownloadBytes} bytes"
                );

            var parent = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(parent))
                Directory.CreateDirectory(parent);

            entry.ExtractToFile(destination, overwrite: true);
        }
    }

    private SteamWorkshopItem Skipped(
        PublishedFileDetails detail,
        ulong manifestId,
        string reason,
        bool isDependency,
        IReadOnlyCollection<ulong> requiredBy,
        DownloadSource source
    )
    {
        var title = string.IsNullOrWhiteSpace(detail.title) ? detail.publishedfileid.ToString() : detail.title.Trim();
        Log($"Skipping {title} ({detail.publishedfileid}): {reason}");
        return new SteamWorkshopItem(
            detail.publishedfileid,
            title,
            Path.Combine(_downloadsDirectory, detail.publishedfileid.ToString()),
            detail.time_updated,
            manifestId,
            "unsupported",
            reason,
            "",
            source.Kind,
            source.ExpectedSize,
            source.HContentFile,
            isDependency,
            requiredBy
        );
    }

    private static string FileNameFromUgcOrDetail(string fileName, PublishedFileDetails detail)
    {
        if (!string.IsNullOrWhiteSpace(fileName))
            return SafeFileName(fileName);

        return FileNameFromDetail(detail);
    }

    private static string FileNameFromDetail(PublishedFileDetails detail)
    {
        if (!string.IsNullOrWhiteSpace(detail.filename))
            return SafeFileName(detail.filename);

        if (Uri.TryCreate(detail.file_url, UriKind.Absolute, out var uri))
        {
            var name = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrWhiteSpace(name))
                return SafeFileName(name);
        }

        return $"{detail.publishedfileid}.bin";
    }

    private static long DeclaredDownloadSize(ulong size)
        => size > MaxWorkshopDownloadBytes
            ? MaxWorkshopDownloadBytes + 1
            : (long)size;

    private static string SafeFileName(string fileName)
    {
        var safe = Path.GetFileName(fileName);
        return string.IsNullOrWhiteSpace(safe) ? "content.bin" : safe;
    }

    private static void DeleteQuietly(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return;

        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException ex) when (IsMissingPathFailure(ex))
        {
        }
        catch
        {
        }
    }

    private static void DeleteFileQuietly(string file)
    {
        if (string.IsNullOrWhiteSpace(file))
            return;

        try
        {
            File.Delete(file);
        }
        catch (FileNotFoundException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch
        {
        }
    }

    private static bool TryMoveDirectory(string sourceDirectory, string targetDirectory)
    {
        try
        {
            Directory.Move(sourceDirectory, targetDirectory);
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (IOException ex) when (IsMissingPathFailure(ex))
        {
            return false;
        }
    }

    private static bool IsMissingPathFailure(IOException ex)
        => ex.Message.IndexOf("not find", StringComparison.OrdinalIgnoreCase) >= 0
            || ex.Message.IndexOf("does not exist", StringComparison.OrdinalIgnoreCase) >= 0
            || ex.Message.IndexOf("no such file", StringComparison.OrdinalIgnoreCase) >= 0;

    private void Log(string message)
    {
        PatchHelper.Log($"[Workshop] {message}");
        try
        {
            LogMessage?.Invoke(message);
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Workshop] Log callback failed: {ex.Message}");
        }
    }

    void IDisposable.Dispose()
        => Dispose();

    internal void Dispose()
    {
        _depotDownloader?.Dispose();
        _http.Dispose();
    }

    private readonly struct DownloadSource
    {
        internal static DownloadSource Unknown { get; } = new(
            "",
            "",
            0,
            "unknown",
            0,
            null
        );

        internal DownloadSource(
            string url,
            string fileName,
            long expectedSize,
            string kind,
            ulong hContentFile,
            string unsupportedReason
        )
        {
            Url = url ?? "";
            FileName = fileName ?? "";
            ExpectedSize = expectedSize;
            Kind = kind ?? "";
            HContentFile = hContentFile;
            UnsupportedReason = unsupportedReason;
        }

        internal string Url { get; }
        internal string FileName { get; }
        internal long ExpectedSize { get; }
        internal string Kind { get; }
        internal ulong HContentFile { get; }
        internal string UnsupportedReason { get; }
    }
}
