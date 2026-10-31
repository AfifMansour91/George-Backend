using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using George.Common;
using George.Data;
using George.DB;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace George.Services.AiStudio;

/// <summary>
/// Runs one claimed job end to end (spec §4): load the photo → Claude identifies the product and what is incidental →
/// fast preview → Claude judges (one free regeneration if the product drifted) → full-size final. Each stage writes the row and
/// pushes <c>AiJobUpdated</c>, so the phone shows the preview while the final is still rendering.
/// </summary>
public class AiStudioJobRunner
{
    public const string HttpClientName = "AiStudio.Download";
    private const string ErrAmbiguous = "מהשם לבדו לא ברור מה צריך להופיע בתמונה. פתחו שוב \"צור תמונה\" וכתבו בתיאור מה רואים (למשל: מארז של 3 סטייקי אנטריקוט ו-2 המבורגרים על מגש).";
    private const string ErrAmbiguousHint = "(פתחו שוב \"צור תמונה\" וכתבו זאת בשדה התיאור.)";

    private readonly AiStudioStorage _storage;
    private readonly AiCreditStorage _credits;
    private readonly MediaStorage _mediaStorage;
    private readonly IFileStorage _fileStorage;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEnumerable<IImageModelProvider> _providers;
    private readonly ProductVisionService _vision;
    private readonly IAiStudioRealtimeNotifier _notifier;
    private readonly AiStudioOptions _options;
    private readonly ILogger<AiStudioJobRunner> _logger;
    private readonly ProductStorage _productStorage;
    private readonly ProductService _productService;

    /// <summary>Style reference images by URL, downloaded once per process.</summary>
    private static readonly ConcurrentDictionary<string, ImageInput> ReferenceCache = new(StringComparer.OrdinalIgnoreCase);

    public AiStudioJobRunner(
        AiStudioStorage storage,
        AiCreditStorage credits,
        MediaStorage mediaStorage,
        IFileStorage fileStorage,
        IHttpClientFactory httpClientFactory,
        IEnumerable<IImageModelProvider> providers,
        ProductVisionService vision,
        IAiStudioRealtimeNotifier notifier,
        IOptions<AiStudioOptions> options,
        ILogger<AiStudioJobRunner> logger,
        ProductStorage productStorage,
        ProductService productService)
    {
        _productStorage = productStorage;
        _productService = productService;
        _storage = storage;
        _credits = credits;
        _mediaStorage = mediaStorage;
        _fileStorage = fileStorage;
        _httpClientFactory = httpClientFactory;
        _providers = providers;
        _vision = vision;
        _notifier = notifier;
        _options = options.Value;
        _logger = logger;
    }

    public async Task RunAsync(long jobId, CancellationToken cancelToken)
    {
        var job = await _storage.GetJobForUpdateAsync(jobId, cancelToken).ConfigureAwait(false);
        if (job == null || job.Status != AiImageJob.StatusRunning)
            return;

        var look = AiStudioLooks.Find(job.Look);
        if (look == null)
        {
            await FailAsync(job, "מראה לא מוכר", cancelToken).ConfigureAwait(false);
            return;
        }
        // Same resolution as at create time: the account's own preset row (prompt / provider) over the global one.
        var accountPreset = await _storage.GetAccountPresetAsync(job.AccountId, look.Key, cancelToken).ConfigureAwait(false);
        if (accountPreset != null)
            look = AiStudioLooks.Merge(look, accountPreset);

        var options = AiStudioService.ParseOptions(job.OptionsJson);
        var provider = PickProvider(options.Provider ?? look.Provider);
        var finalPx = options.Hd ? Math.Max(_options.FinalPx, _options.HdPx) : _options.FinalPx;
        if (provider == null)
        {
            await FailAsync(job, "אף מנוע תמונה לא מוגדר בשרת", cancelToken).ConfigureAwait(false);
            return;
        }
        job.Provider = provider.Key;

        try
        {
            // 1. Source photo.
            ImageInput? source = null;
            if (look.NeedsSource)
            {
                var raw = await DownloadAsync(job.SourceUrl!, cancelToken).ConfigureAwait(false);
                source = ImagePostProcess.PrepareSource(raw, _options.SourceMaxPx);
            }

            // 2. Identify (skipped on retries - the incidental text is copied from the first job).
            var productName = options.ProductName;
            string? brief = null;
            if (source != null && job.IncidentalText == null && job.RetryOfJobId == null)
            {
                await StageAsync(job, AiImageJob.StageIdentify, cancelToken).ConfigureAwait(false);
                var id = await _vision.IdentifyAsync(source, options.ProductName, options.CategoryName, cancelToken).ConfigureAwait(false);
                if (id != null)
                {
                    await RecordAsync(job, AiProviderSpend.ProviderAnthropic, _vision.ModelName, AiProviderSpend.KindIdentify, id.InputTokens, id.OutputTokens, _vision.CostUsd(id.InputTokens, id.OutputTokens), cancelToken).ConfigureAwait(false);
                    if (!id.Usable)
                    {
                        await FailAsync(job, string.IsNullOrWhiteSpace(id.UnusableReason) ? "לא זוהה מוצר שלם בתמונה. צלמו את המוצר כולו ממרחק של 30–50 ס\"מ." : id.UnusableReason, cancelToken).ConfigureAwait(false);
                        return;
                    }
                    job.IncidentalText = string.IsNullOrWhiteSpace(id.Incidental) ? string.Empty : id.Incidental.Trim();
                    options.IdentifiedName = id.ProductName;
                    options.CategoryEn = id.CategoryEn;
                    options.IsPackaged = id.IsPackaged;
                    job.OptionsJson = JsonSerializer.Serialize(options);
                    productName ??= id.ProductName;
                }
                else
                {
                    job.IncidentalText = string.Empty;
                }
            }
            else if (source == null)
            {
                // generate: expand name + category + the shop's description into a visual brief (reused on retries).
                brief = options.Brief;
                if (string.IsNullOrWhiteSpace(brief))
                {
                    await StageAsync(job, AiImageJob.StageIdentify, cancelToken).ConfigureAwait(false);
                    var b = await _vision.BriefAsync(options.ProductName ?? "product", options.CategoryName, options.Description, cancelToken).ConfigureAwait(false);
                    if (b != null)
                    {
                        await RecordAsync(job, AiProviderSpend.ProviderAnthropic, _vision.ModelName, AiProviderSpend.KindBrief, b.InputTokens, b.OutputTokens, _vision.CostUsd(b.InputTokens, b.OutputTokens), cancelToken).ConfigureAwait(false);
                        if (b.Ambiguous || string.IsNullOrWhiteSpace(b.Brief))
                        {
                            // Better to ask than to draw a random box (QA 29.10.2026: "מארז" became a pack of toilet paper).
                            await FailAsync(job, string.IsNullOrWhiteSpace(b.Question) ? ErrAmbiguous : b.Question + " " + ErrAmbiguousHint, cancelToken).ConfigureAwait(false);
                            return;
                        }
                        brief = b.Brief;
                        options.Brief = b.Brief;
                        options.CategoryEn ??= b.CategoryEn;
                        options.IsPackaged = b.IsBrandedPackaged;
                        job.OptionsJson = JsonSerializer.Serialize(options);
                    }
                }
                brief ??= string.Join(", ", new[] { options.ProductName, options.Description }.Where(s => !string.IsNullOrWhiteSpace(s)));
            }
            productName ??= options.CategoryName ?? "product";

            // 3. Prompt + references (a generated image borrows the style references of its chosen background).
            var categoryEn = options.CategoryEn ?? options.CategoryName;
            job.Prompt = PromptBuilder.Build(look, categoryEn, options.Ratio, job.IncidentalText, options.Scene, brief, options.Background, look.StyleNotes);
            var references = await LoadReferencesAsync(source == null ? (options.Background ?? look.Key) : look.Key, job.AccountId, cancelToken).ConfigureAwait(false);
            await _storage.SaveAsync(cancelToken).ConfigureAwait(false);

            // 4. Preview.
            if (await IsCanceledAsync(job, cancelToken).ConfigureAwait(false)) return;
            await StageAsync(job, AiImageJob.StagePreview, cancelToken).ConfigureAwait(false);
            var sw = Stopwatch.StartNew();
            var preview = await CallAsync(provider, source, references, job.Prompt, options.Ratio, _options.PreviewQuality, _options.PreviewPx, cancelToken).ConfigureAwait(false);
            job.PreviewMs = (int)sw.ElapsedMilliseconds;
            job.Model = preview.Model;
            await RecordAsync(job, provider.Key, preview.Model, AiProviderSpend.KindPreview, preview.InputTokens, preview.OutputTokens, preview.CostUsd, cancelToken).ConfigureAwait(false);

            var previewMedia = await SaveResultAsync(job, preview.Bytes, productName, look, "preview", _options.PreviewPx, cancelToken).ConfigureAwait(false);
            job.PreviewUrl = previewMedia.Url;
            job.PreviewMediaId = previewMedia.Id;
            job.Status = AiImageJob.StatusPreviewReady;
            await SaveAndNotifyAsync(job, cancelToken).ConfigureAwait(false);
            _logger.LogInformation("AiStudio job {JobId}: preview in {Ms} ms ({Provider}/{Model})", job.GuidId, job.PreviewMs, provider.Key, preview.Model);

            // 5. Judge, with one free regeneration when the product drifted.
            await StageAsync(job, AiImageJob.StageJudge, cancelToken).ConfigureAwait(false);
            // Without a source the judge can only check the picture against the words, so give it the brief too.
            var judgeSubject = source == null && !string.IsNullOrWhiteSpace(brief) ? $"{productName} ({brief})" : productName;
            var judgeLook = source == null && options.Background != null ? (AiStudioLooks.Find(options.Background)?.Label ?? look.Label) : look.Label;
            var judgement = await _vision.JudgeAsync(source, new ImageInput(preview.Bytes, "image/jpeg", "preview.jpg"), judgeSubject, judgeLook, cancelToken).ConfigureAwait(false);
            if (judgement != null)
            {
                await RecordAsync(job, AiProviderSpend.ProviderAnthropic, _vision.ModelName, AiProviderSpend.KindJudge, judgement.InputTokens, judgement.OutputTokens, _vision.CostUsd(judgement.InputTokens, judgement.OutputTokens), cancelToken).ConfigureAwait(false);
                ApplyJudgement(job, judgement);
                if (source != null && judgement.Fidelity < _options.RetryBelowFidelity && job.RetryOfJobId == null)
                {
                    if (await IsCanceledAsync(job, cancelToken).ConfigureAwait(false)) return;
                    _logger.LogInformation("AiStudio job {JobId}: fidelity {Fidelity} below {Threshold}, regenerating preview", job.GuidId, judgement.Fidelity, _options.RetryBelowFidelity);
                    sw.Restart();
                    var again = await CallAsync(provider, source, references, job.Prompt, options.Ratio, _options.PreviewQuality, _options.PreviewPx, cancelToken).ConfigureAwait(false);
                    await RecordAsync(job, provider.Key, again.Model, AiProviderSpend.KindPreview, again.InputTokens, again.OutputTokens, again.CostUsd, cancelToken).ConfigureAwait(false);
                    var secondJudgement = await _vision.JudgeAsync(source, new ImageInput(again.Bytes, "image/jpeg", "preview.jpg"), productName, look.Label, cancelToken).ConfigureAwait(false);
                    if (secondJudgement != null)
                        await RecordAsync(job, AiProviderSpend.ProviderAnthropic, _vision.ModelName, AiProviderSpend.KindJudge, secondJudgement.InputTokens, secondJudgement.OutputTokens, _vision.CostUsd(secondJudgement.InputTokens, secondJudgement.OutputTokens), cancelToken).ConfigureAwait(false);
                    if (secondJudgement == null || secondJudgement.Fidelity >= judgement.Fidelity)
                    {
                        preview = again;
                        job.PreviewMs = (int)sw.ElapsedMilliseconds;
                        previewMedia = await SaveResultAsync(job, preview.Bytes, productName, look, "preview", _options.PreviewPx, cancelToken).ConfigureAwait(false);
                        job.PreviewUrl = previewMedia.Url;
                        job.PreviewMediaId = previewMedia.Id;
                        if (secondJudgement != null)
                            ApplyJudgement(job, secondJudgement);
                        await SaveAndNotifyAsync(job, cancelToken).ConfigureAwait(false);
                    }
                }
            }

            // 6. Final.
            if (await IsCanceledAsync(job, cancelToken).ConfigureAwait(false)) return;
            await StageAsync(job, AiImageJob.StageFinal, cancelToken).ConfigureAwait(false);
            sw.Restart();
            var final = await CallAsync(provider, source, references, job.Prompt, options.Ratio, _options.FinalQuality, finalPx, cancelToken, options.Hd).ConfigureAwait(false);
            job.FinalMs = (int)sw.ElapsedMilliseconds;
            await RecordAsync(job, provider.Key, final.Model, AiProviderSpend.KindFinal, final.InputTokens, final.OutputTokens, final.CostUsd, cancelToken).ConfigureAwait(false);
            var finalMedia = await SaveResultAsync(job, final.Bytes, productName, look, "final", finalPx, cancelToken).ConfigureAwait(false);
            job.ResultUrl = finalMedia.Url;
            job.ResultMediaId = finalMedia.Id;
            job.Status = AiImageJob.StatusDone;
            job.FinishedAt = DateTime.UtcNow;
            await SaveAndNotifyAsync(job, cancelToken).ConfigureAwait(false);
            _logger.LogInformation("AiStudio job {JobId}: done, final in {Ms} ms, est. ${Cost}", job.GuidId, job.FinalMs, job.ProviderCostUsd);

            // "צור תמונות למוצרים ללא תמונה": the final becomes the product's image (only if it still has none) and the product is
            // pushed to its Woo stores like any product edit. Failing here must not fail the job - the image exists in the library.
            if (options.AttachToProduct && job.ProductId is > 0)
                await AttachToProductAsync(job, finalMedia, cancelToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancelToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ImageModelException ex)
        {
            await FailAsync(job, ex.Message, cancelToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AiStudio job {JobId} crashed", job.GuidId);
            await FailAsync(job, "שגיאה בעיבוד התמונה. נסו שוב.", cancelToken).ConfigureAwait(false);
        }
    }

    //*************************    Steps    *************************//

    private IImageModelProvider? PickProvider(string? overrideKey)
    {
        var key = string.IsNullOrWhiteSpace(overrideKey) ? _options.DefaultProvider : overrideKey;
        var p = _providers.FirstOrDefault(x => x.IsConfigured && string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
        return p ?? _providers.FirstOrDefault(x => x.IsConfigured);
    }

    private static Task<ImageModelResult> CallAsync(IImageModelProvider provider, ImageInput? source, List<ImageInput> references, string prompt, string ratio, string quality, int px, CancellationToken cancelToken, bool hd = false)
    {
        var req = new ImageModelRequest { Source = source, References = references, Prompt = prompt, Ratio = ratio, Quality = quality, LongestPx = px, Hd = hd };
        return source == null ? provider.GenerateAsync(req, cancelToken) : provider.EditAsync(req, cancelToken);
    }

    private async Task AttachToProductAsync(AiImageJob job, Media finalMedia, CancellationToken cancelToken)
    {
        try
        {
            var siteIds = await _productStorage.AddProductImageIfEmptyAsync(job.ProductId!.Value, finalMedia.Url, finalMedia.Id, cancelToken).ConfigureAwait(false);
            if (siteIds == null)
            {
                _logger.LogInformation("AiStudio job {JobId}: product {ProductId} already has an image, result left in the library", job.GuidId, job.ProductId);
                return;
            }
            _logger.LogInformation("AiStudio job {JobId}: attached as the image of product {ProductId}", job.GuidId, job.ProductId);
            if (siteIds.Count > 0)
                await _productService.SyncProductToWooCommerceForAssignedSitesAsync(job.ProductId.Value, siteIds, cancelToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AiStudio job {JobId}: attaching the result to product {ProductId} failed", job.GuidId, job.ProductId);
        }
    }

    /// <summary>Style references for a look: the account's preset row, else the global row, else AiStudio:ReferenceUrls.</summary>
    private async Task<List<ImageInput>> LoadReferencesAsync(string look, int accountId, CancellationToken cancelToken)
    {
        var list = new List<ImageInput>();
        IReadOnlyList<string>? urls = null;
        var def = AiStudioLooks.Find(look);
        if (def != null)
        {
            var row = await _storage.GetAccountPresetAsync(accountId, def.Key, cancelToken).ConfigureAwait(false);
            urls = (row == null ? def : AiStudioLooks.Merge(def, row)).References;
        }
        if (urls == null && _options.ReferenceUrls.TryGetValue(look, out var cfg))
            urls = cfg;
        if (urls == null)
            return list;
        foreach (var url in urls.Where(u => !string.IsNullOrWhiteSpace(u)).Take(AiStudioLooks.MaxReferences))
        {
            if (ReferenceCache.TryGetValue(url, out var cached))
            {
                list.Add(cached);
                continue;
            }
            try
            {
                var bytes = await DownloadAsync(url, cancelToken).ConfigureAwait(false);
                var prepared = ImagePostProcess.PrepareReference(bytes, $"ref-{list.Count + 1}.jpg");
                ReferenceCache[url] = prepared;
                list.Add(prepared);
            }
            catch (Exception ex)
            {
                // A missing reference degrades the look, it does not fail the job.
                _logger.LogWarning(ex, "AiStudio: reference image {Url} could not be loaded", url);
            }
        }
        return list;
    }

    private async Task<byte[]> DownloadAsync(string url, CancellationToken cancelToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var res = await client.GetAsync(url, cancelToken).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
            throw new ImageModelException($"לא ניתן לקרוא את תמונת המקור (HTTP {(int)res.StatusCode})");
        var bytes = await res.Content.ReadAsByteArrayAsync(cancelToken).ConfigureAwait(false);
        if (bytes.Length == 0)
            throw new ImageModelException("תמונת המקור ריקה");
        return bytes;
    }

    private async Task<Media> SaveResultAsync(AiImageJob job, byte[] bytes, string productName, AiStudioLooks.LookDef look, string stage, int maxPx, CancellationToken cancelToken)
    {
        // A generated image is named after the background it was drawn on, not after the "generate" verb.
        var options = AiStudioService.ParseOptions(job.OptionsJson);
        var label = look.Key == AiStudioLooks.Generate && options.Background != null ? $"{AiStudioLooks.Find(options.Background)?.Label ?? look.Label} (AI)" : look.Label;
        var (encoded, _, _) = ImagePostProcess.EncodeResult(bytes, maxPx, _options.OutputJpegQuality);
        // ASCII + the job id: the storage keeps the name as given, so a second run of the same product/look must not overwrite the first.
        var fileName = $"ai-{job.GuidId:N}"[..11] + $"-{look.Key}-{stage}.jpg";
        var formFile = ToFormFile(encoded, fileName, "image/jpeg");
        var upload = await _fileStorage.UploadFileAsync(formFile, FileHelper.GetTempFolderPath(), cancelToken).ConfigureAwait(false);
        if (!upload.IsSuccessful || string.IsNullOrEmpty(upload.FilePath))
            throw new ImageModelException("שמירת התמונה נכשלה: " + (upload.Exception?.Message ?? "upload failed"));

        var media = new Media
        {
            Url = FileHelper.GetFileExternalPath(upload.FilePath),
            Name = $"{productName} - {label}{(stage == "preview" ? " (preview)" : string.Empty)}",
            CreationUserId = job.UserId,
            CreationTime = DateTime.UtcNow,
            IsDeleted = false,
            FileSize = encoded.LongLength,
            UsageCount = 0,
            AccountId = job.AccountId,
            Origin = look.NeedsSource ? Media.OriginAi : Media.OriginAiGenerated,
            AiJobId = job.Id,
            // Only the final of a community job goes to the pool; previews stay private clutter-free, and a branded / packaged
            // product (Claude's call) never enters it - the pool is for generic food shots any shop may reuse (spec §7).
            LicensePool = job.IsShared && stage == "final" && options.IsPackaged != true,
            TypeId = await _mediaStorage.GetOrCreateMediaTypeAsync("image", cancelToken).ConfigureAwait(false),
        };
        media = await _mediaStorage.CreateMediaAsync(media, null, null, job.AccountId, cancelToken, isGlobal: media.LicensePool).ConfigureAwait(false);
        if (job.SiteId is > 0)
            await _mediaStorage.AddAccountMediaUsageAsync(job.AccountId, job.SiteId.Value, media.Id, cancelToken).ConfigureAwait(false);
        return media;
    }

    private static IFormFile ToFormFile(byte[] bytes, string fileName, string contentType)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName) { Headers = new HeaderDictionary(), ContentType = contentType };
    }

    private static string Slug(string s)
    {
        var chars = s.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug = new string(chars).Trim('-');
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return string.IsNullOrEmpty(slug) ? "product" : slug.Length > 40 ? slug[..40] : slug;
    }

    private static void ApplyJudgement(AiImageJob job, ImageJudgement j)
    {
        job.FidelityScore = j.Fidelity;
        job.AestheticScore = j.Aesthetic;
        job.JudgeVerdict = j.Verdict;
        job.JudgeIssues = j.Issues.Count == 0 ? null : JsonSerializer.Serialize(j.Issues);
    }

    /// <summary>Adds the call's tokens and cost to the job AND writes it to the per-provider spend journal (the wallet panel
    /// on /aistudioadmin). Vision calls count too, so ProviderCostUsd is the whole bill of the job.</summary>
    private async Task RecordAsync(AiImageJob job, string provider, string? model, string kind, int? input, int? output, decimal? costUsd, CancellationToken cancelToken)
    {
        AddTokens(job, input, output, costUsd);
        if (costUsd is > 0m)
        {
            await _storage.AddSpendAsync(new AiProviderSpend
            {
                Provider = provider, Model = model, Kind = kind, AccountId = job.AccountId, RefJobId = job.Id,
                InputTokens = input, OutputTokens = output, AmountUsd = costUsd.Value,
            }, cancelToken).ConfigureAwait(false);
        }
    }

    private static void AddTokens(AiImageJob job, int? input, int? output, decimal? costUsd = null)
    {
        if (input.HasValue) job.InputTokens = (job.InputTokens ?? 0) + input.Value;
        if (output.HasValue) job.OutputTokens = (job.OutputTokens ?? 0) + output.Value;
        if (costUsd.HasValue) job.ProviderCostUsd = (job.ProviderCostUsd ?? 0) + costUsd.Value;
    }

    private async Task StageAsync(AiImageJob job, string stage, CancellationToken cancelToken)
    {
        job.Stage = stage;
        await SaveAndNotifyAsync(job, cancelToken).ConfigureAwait(false);
    }

    private async Task<bool> IsCanceledAsync(AiImageJob job, CancellationToken cancelToken)
    {
        var status = await _storage.GetStatusAsync(job.Id, cancelToken).ConfigureAwait(false);
        if (status != AiImageJob.StatusCanceled)
            return false;
        // The row was flipped by the API while we were busy; keep the tracked entity in step and stop.
        job.Status = AiImageJob.StatusCanceled;
        job.FinishedAt ??= DateTime.UtcNow;
        await _storage.SaveAsync(cancelToken).ConfigureAwait(false);
        return true;
    }

    private async Task FailAsync(AiImageJob job, string error, CancellationToken cancelToken)
    {
        job.Status = AiImageJob.StatusFailed;
        job.Error = error.Length > 1000 ? error[..1000] : error;
        job.FinishedAt = DateTime.UtcNow;
        if (job.Credits > 0 && job.LedgerEntryId != null)
        {
            try
            {
                await _credits.RefundJobAsync(job.Id, "זיכוי: " + (error.Length > 200 ? error[..200] : error), cancelToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AiStudio job {JobId}: refund failed", job.GuidId);
            }
        }
        job.Refunded = job.Credits > 0;
        await SaveAndNotifyAsync(job, cancelToken).ConfigureAwait(false);
        _logger.LogWarning("AiStudio job {JobId} failed at {Stage}: {Error}", job.GuidId, job.Stage, error);
    }

    private async Task SaveAndNotifyAsync(AiImageJob job, CancellationToken cancelToken)
    {
        await _storage.SaveAsync(cancelToken).ConfigureAwait(false);
        try
        {
            await _notifier.JobUpdatedAsync(job.AccountId, AiStudioService.ToRes(job), cancelToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AiStudio: realtime notify failed for job {JobId}", job.GuidId);
        }
    }
}
