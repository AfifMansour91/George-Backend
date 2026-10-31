using AutoMapper;
using George.Common;
using George.Data;
using George.DB;
using George.Services.Marketing;
using George.Services.Payments;
using George.Services.Payments.Cardcom;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace George.Services.AiStudio;

/// <summary>
/// AI Studio credits (spec §7, phase 3): the shop's balance and history, the global package catalog, self-service purchases on
/// the PLATFORM's Cardcom terminal (the same terminal and VAT rule as the SMS packages, <see cref="PlatformBilling"/>), the
/// super-admin allocation and overview. Debit / refund of single jobs lives in <see cref="AiStudioService"/> and
/// <see cref="AiStudioJobRunner"/>; this service never touches a job.
/// </summary>
public class AiCreditService : ServiceBase
{
    public const string ErrPurchaseUnavailable = "רכישה מקוונת עדיין לא פעילה - לטעינת קרדיטים פנה אלינו.";
    public const string ErrAccountAdminOnly = "רק מנהל החשבון יכול לרכוש קרדיטים.";
    private const string ErrNoAccount = "לא נמצא חשבון לפעולה";
    private const string ErrAdminOnly = "רק מנהל מערכת יכול לבצע את הפעולה הזו";
    private const int MaxListed = 100;

    private readonly AiCreditStorage _storage;
    private readonly UserStorage _userStorage;
    private readonly AccountStorage _accountStorage;
    private readonly IConfiguration _configuration;
    private readonly CardcomGateway _cardcom;
    private readonly AiStudioOptions _aiOptions;
    private readonly AiStudioStorage _studioStorage;

    public AiCreditService(
        ILogger<AiCreditService> logger,
        IMapper mapper,
        CacheManager cache,
        AiCreditStorage storage,
        UserStorage userStorage,
        AccountStorage accountStorage,
        IConfiguration configuration,
        CardcomGateway cardcom,
        Microsoft.Extensions.Options.IOptions<AiStudioOptions> aiOptions,
        AiStudioStorage studioStorage) : base(logger, mapper, cache)
    {
        _aiOptions = aiOptions.Value;
        _studioStorage = studioStorage;
        _storage = storage;
        _userStorage = userStorage;
        _accountStorage = accountStorage;
        _configuration = configuration;
        _cardcom = cardcom;
    }

    //*************************    Balance    *************************//

    public async Task<IApiResponse<AiCreditsRes>> GetCreditsAsync(int? accountId, CancellationToken cancelToken)
    {
        var response = new ApiResponse<AiCreditsRes>();
        var (scope, status, error) = await ResolveScopeAsync(accountId, cancelToken).ConfigureAwait(false);
        if (scope == null)
            return CreateResponse(response, status, error!);
        response.Data = await BuildCreditsAsync(scope.Account, includeHistory: true, cancelToken).ConfigureAwait(false);
        return response;
    }

    public async Task<AiCreditsRes> BuildCreditsAsync(Account account, bool includeHistory, CancellationToken cancelToken)
    {
        var totals = await _storage.GetTotalsAsync(account.Id, DateTime.UtcNow, cancelToken).ConfigureAwait(false);
        var res = new AiCreditsRes
        {
            AccountId = account.Id,
            Mode = ModeOf(account),
            Balance = totals.Balance,
            Purchased = totals.Purchased,
            Consumed = totals.Consumed,
            Refunded = totals.Refunded,
            ConsumedLast30Days = totals.ConsumedLast30Days,
            LastPurchaseAt = totals.LastPurchaseAt,
            CanPurchase = PlatformBilling.FromConfiguration(_configuration) != null,
            PoolConsentAt = account.AiStudioPoolConsentAt,
        };
        if (includeHistory)
        {
            res.Ledger = (await _storage.GetLedgerAsync(account.Id, 100, cancelToken).ConfigureAwait(false))
                .Select(l => new AiCreditLedgerEntryRes { Id = l.Id, CreationTime = l.CreationTime, EntryType = l.EntryType, Amount = l.Amount, RefJobId = l.RefJobId, Note = l.Note })
                .ToList();
            res.Purchases = (await _storage.ListPurchasesAsync(account.Id, MaxListed, cancelToken).ConfigureAwait(false)).Select(MapPurchase).ToList();
        }
        return res;
    }

    //*************************    Catalog    *************************//

    public async Task<IApiResponse<AiCreditPackagesRes>> GetPackagesAsync(int? accountId, CancellationToken cancelToken)
    {
        var response = new ApiResponse<AiCreditPackagesRes>();
        var (scope, status, error) = await ResolveScopeAsync(accountId, cancelToken).ConfigureAwait(false);
        if (scope == null)
            return CreateResponse(response, status, error!);
        response.Data = BuildPackages(await _storage.GetPackagesAsync(activeOnly: true, cancelToken).ConfigureAwait(false), includeInactive: false);
        return response;
    }

    public async Task<IApiResponse<AiCreditPackagesRes>> GetPackagesForAdminAsync(CancellationToken cancelToken)
    {
        var response = new ApiResponse<AiCreditPackagesRes>();
        if (!await IsUnrestrictedAsync(cancelToken).ConfigureAwait(false))
            return CreateResponse(response, StatusCode.UnauthorizedData, ErrAdminOnly);
        response.Data = BuildPackages(await _storage.GetPackagesAsync(activeOnly: false, cancelToken).ConfigureAwait(false), includeInactive: true);
        return response;
    }

    public async Task<IApiResponse<AiCreditPackagesRes>> SavePackagesAsync(List<AiCreditPackageSaveReq>? req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<AiCreditPackagesRes>();
        if (!await IsUnrestrictedAsync(cancelToken).ConfigureAwait(false))
            return CreateResponse(response, StatusCode.UnauthorizedData, ErrAdminOnly);
        var rows = req ?? new List<AiCreditPackageSaveReq>();
        if (rows.Count > 20)
            return CreateResponse(response, StatusCode.InvalidRequest, "עד 20 חבילות");
        foreach (var r in rows)
        {
            if (r.Credits <= 0 || r.Credits > 1_000_000)
                return CreateResponse(response, StatusCode.InvalidRequest, "כמות קרדיטים לא חוקית");
            if (r.Price < 0 || r.Price > 1_000_000m)
                return CreateResponse(response, StatusCode.InvalidRequest, "מחיר לא חוקי");
        }
        var saved = await _storage.SavePackagesAsync(rows.Select(r => new AiCreditPackage
        {
            Id = Math.Max(0, r.Id), Name = Trunc(r.Name, 100), Credits = r.Credits, Price = Math.Round(r.Price, 2, MidpointRounding.AwayFromZero),
            IsActive = r.IsActive, SortOrder = r.SortOrder, Badge = Trunc(r.Badge, 200),
        }).ToList(), AuthUser.Id > 0 ? AuthUser.Id : null, cancelToken).ConfigureAwait(false);
        response.Data = BuildPackages(saved, includeInactive: true);
        return response;
    }

    private AiCreditPackagesRes BuildPackages(List<AiCreditPackage> packages, bool includeInactive)
    {
        var vat = PlatformBilling.VatPercent(_configuration);
        return new AiCreditPackagesRes
        {
            VatPercent = vat,
            CanPurchase = PlatformBilling.FromConfiguration(_configuration) != null,
            Looks = AiStudioLooks.All.Select(l => new AiLookRes { Key = l.Key, Label = l.Label, Description = l.Description, Credits = l.Credits, NeedsSource = l.NeedsSource, IsBackground = AiStudioLooks.IsBackground(l.Key) }).ToList(),
            Packages = packages.Where(p => includeInactive || p.IsActive).Select(p => new AiCreditPackageRes
            {
                Id = p.Id, Name = p.Name, Credits = p.Credits, Price = p.Price, VatPercent = vat,
                Total = PlatformBilling.TotalWithVat(p.Price, vat),
                PricePerCreditAgorot = PlatformBilling.PricePerMessageAgorot(p.Price, p.Credits),
                IsActive = p.IsActive, SortOrder = p.SortOrder, Badge = p.Badge,
            }).ToList(),
        };
    }

    //*************************    Purchase    *************************//

    /// <summary>Opens the hosted payment page for one package. The purchase row exists from this moment (status initiated).</summary>
    public async Task<IApiResponse<AiCreditPurchaseStartRes>> StartPurchaseAsync(AiCreditPurchaseStartReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<AiCreditPurchaseStartRes>();
        var (scope, status, error) = await ResolveScopeAsync(req.AccountId, cancelToken).ConfigureAwait(false);
        if (scope == null)
            return CreateResponse(response, status, error!);
        if (!scope.IsAccountAdmin)
            return CreateResponse(response, StatusCode.UnauthorizedData, ErrAccountAdminOnly);

        var creds = PlatformBilling.FromConfiguration(_configuration);
        if (creds == null)
            return CreateResponse(response, StatusCode.InvalidRequest, ErrPurchaseUnavailable);

        var package = await _storage.GetPackageAsync(req.PackageId, cancelToken).ConfigureAwait(false);
        if (package == null || !package.IsActive)
            return CreateResponse(response, StatusCode.InvalidRequest, "החבילה לא נמצאה או אינה זמינה יותר");
        var account = scope.Account;

        var vat = PlatformBilling.VatPercent(_configuration);
        var total = PlatformBilling.TotalWithVat(package.Price, vat);
        if (total <= 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "לחבילה אין מחיר - פנה אלינו");

        var purchase = await _storage.AddPurchaseAsync(new AiCreditPurchase
        {
            AccountId = scope.AccountId,
            PackageId = package.Id,
            PackageName = PackageDisplayName(package.Name, package.Credits),
            Credits = package.Credits,
            Price = package.Price,
            VatPercent = vat,
            Total = total,
            Status = AiCreditPurchase.StatusInitiated,
            CreationUserId = AuthUser.Id > 0 ? AuthUser.Id : null,
        }, cancelToken).ConfigureAwait(false);

        var appBase = PaymentService.ResolveReturnAppBase(_configuration["App:PublicBaseUrl"] ?? _configuration["PublicAppBaseUrl"] ?? _configuration["Client:BaseUrl"], req.AppOrigin);
        var apiBase = (_configuration["Payment:PublicApiBaseUrl"] ?? _configuration["App:ApiPublicBaseUrl"] ?? string.Empty).TrimEnd('/');
        var returnUrl = $"{appBase}/aistudio/purchase/{purchase.Id}/return";

        var create = await _cardcom.CreateHostedSessionAsync(creds, new CreateHostedSessionRequest
        {
            OrderId = purchase.Id,
            Amount = total,
            ReturnValue = $"aicredits:{purchase.Id}",
            ProductName = $"קרדיטים לסטודיו AI {purchase.PackageName} - Giorgio",
            Language = "he",
            SaveCard = false,
            UseAuthorizationHold = false,
            MaxInstallments = creds.MaxInstallments,
            SuccessRedirectUrl = returnUrl + "?status=success",
            FailedRedirectUrl = returnUrl + "?status=failed",
            WebHookUrl = string.IsNullOrEmpty(apiBase) ? null : $"{apiBase}/Webhooks/Cardcom/AiStudio",
            CustomerName = account.Name,
            CustomerPhone = account.Phone,
            CustomerEmail = account.ManagerEmail,
            Document = BuildPurchaseDocument(account, purchase, creds.DocumentTypeToCreate),
        }, cancelToken).ConfigureAwait(false);

        if (!create.Success || string.IsNullOrWhiteSpace(create.PaymentUrl))
        {
            _logger.LogWarning("AiStudio credit purchase {PurchaseId} for account {AccountId}: LowProfile/Create failed {Code} {Description}. {Raw}",
                purchase.Id, scope.AccountId, create.ErrorCode, create.ErrorDescription, create.RawJson);
            await _storage.UpdatePurchaseAsync(purchase.Id, p =>
            {
                p.Status = AiCreditPurchase.StatusFailed;
                p.ErrorDescription = Trunc($"פתיחת דף התשלום נכשלה: {create.ErrorDescription ?? create.ErrorCode}", 500);
            }, cancelToken).ConfigureAwait(false);
            return CreateResponse(response, StatusCode.InvalidRequest, $"פתיחת דף התשלום נכשלה ({create.ErrorDescription ?? create.ErrorCode}). נסה שוב או פנה אלינו.");
        }

        await _storage.UpdatePurchaseAsync(purchase.Id, p => p.LowProfileId = create.LowProfileId, cancelToken).ConfigureAwait(false);
        _logger.LogInformation("AiStudio credit purchase {PurchaseId}: account {AccountId} buys {Credits} credits for ₪{Total} (LowProfile {LowProfileId}).",
            purchase.Id, scope.AccountId, purchase.Credits, total, create.LowProfileId);

        response.Data = new AiCreditPurchaseStartRes { PurchaseId = purchase.Id, PaymentUrl = create.PaymentUrl!, Total = total };
        return response;
    }

    /// <summary>The return page: settle (if the webhook has not yet) and answer with the purchase + the balance after it.</summary>
    public async Task<IApiResponse<AiCreditPurchaseRes>> ConfirmPurchaseAsync(int purchaseId, int? accountId, CancellationToken cancelToken)
    {
        var response = new ApiResponse<AiCreditPurchaseRes>();
        var (scope, status, error) = await ResolveScopeAsync(accountId, cancelToken).ConfigureAwait(false);
        if (scope == null)
            return CreateResponse(response, status, error!);
        var purchase = await _storage.GetPurchaseAsync(purchaseId, cancelToken).ConfigureAwait(false);
        if (purchase == null || purchase.AccountId != scope.AccountId)
            return CreateResponse(response, StatusCode.ItemNotFound, "הרכישה לא נמצאה");

        await SettlePurchaseAsync(purchase, cancelToken).ConfigureAwait(false);
        purchase = await _storage.GetPurchaseAsync(purchaseId, cancelToken).ConfigureAwait(false) ?? purchase;
        var res = MapPurchase(purchase);
        res.Credits = await BuildCreditsAsync(scope.Account, includeHistory: false, cancelToken).ConfigureAwait(false);
        response.Data = res;
        return response;
    }

    public async Task<IApiResponse<List<AiCreditPurchaseRes>>> ListPurchasesAsync(int? accountId, CancellationToken cancelToken)
    {
        var response = new ApiResponse<List<AiCreditPurchaseRes>>();
        var (scope, status, error) = await ResolveScopeAsync(accountId, cancelToken).ConfigureAwait(false);
        if (scope == null)
            return CreateResponse(response, status, error!);
        var rows = await _storage.ListPurchasesAsync(scope.AccountId, MaxListed, cancelToken).ConfigureAwait(false);
        response.Data = rows.Select(MapPurchase).ToList();
        return response;
    }

    /// <summary>Cardcom's server-to-server call for a credit payment (WebHookUrl = /Webhooks/Cardcom/AiStudio). No user context.</summary>
    public async Task ProcessCardcomWebhookAsync(string lowProfileId, CancellationToken cancelToken)
    {
        if (string.IsNullOrWhiteSpace(lowProfileId)) return;
        var purchase = await _storage.GetPurchaseByLowProfileIdAsync(lowProfileId.Trim(), cancelToken).ConfigureAwait(false);
        if (purchase == null)
        {
            _logger.LogWarning("AiStudio Cardcom webhook: no purchase for LowProfileId {Id}", lowProfileId);
            return;
        }
        await SettlePurchaseAsync(purchase, cancelToken).ConfigureAwait(false);
    }

    /// <summary>Asks Cardcom what happened to the hosted session and applies it. Idempotent: only the caller that flips initiated → paid credits the ledger.</summary>
    private async Task SettlePurchaseAsync(AiCreditPurchase purchase, CancellationToken cancelToken)
    {
        if (purchase.Status != AiCreditPurchase.StatusInitiated || string.IsNullOrWhiteSpace(purchase.LowProfileId))
            return;

        var creds = PlatformBilling.FromConfiguration(_configuration);
        if (creds == null)
        {
            _logger.LogError("AiStudio purchase {PurchaseId}: cannot settle - Billing:Cardcom is no longer configured.", purchase.Id);
            return;
        }

        ValidateCallbackResult result;
        try
        {
            result = await _cardcom.ValidateCallbackAsync(creds, new ValidateCallbackRequest { LowProfileId = purchase.LowProfileId }, cancelToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AiStudio purchase {PurchaseId}: GetLpResult threw.", purchase.Id);
            return;
        }

        if (!result.Success)
        {
            if (result.IsPending)
                return;
            var reason = Trunc(string.IsNullOrWhiteSpace(result.Description) ? "התשלום לא הושלם" : result.Description, 500);
            if (await _storage.TryMarkPurchaseFailedAsync(purchase.Id, reason, cancelToken).ConfigureAwait(false))
                _logger.LogInformation("AiStudio purchase {PurchaseId} failed: {Code} {Description}", purchase.Id, result.ResponseCode, result.Description);
            return;
        }
        if (!string.IsNullOrWhiteSpace(result.Operation) && result.Operation.Contains("CreateTokenOnly", StringComparison.OrdinalIgnoreCase))
        {
            await _storage.TryMarkPurchaseFailedAsync(purchase.Id, "לא בוצע חיוב (העסקה חזרה כשמירת כרטיס בלבד)", cancelToken).ConfigureAwait(false);
            return;
        }
        if (result.Amount is decimal charged && Math.Abs(charged - purchase.Total) > 0.05m)
            _logger.LogWarning("AiStudio purchase {PurchaseId}: charged ₪{Charged} but expected ₪{Expected}.", purchase.Id, charged, purchase.Total);

        var display = _cardcom.ExtractCardDisplayFields(result.RawJson);
        var last4 = Trunc(FirstNonEmpty(result.Last4Digits, display.Last4Digits), 4);
        var brand = Trunc(FirstNonEmpty(result.CardBrand, display.CardBrand), 50);
        var won = await _storage.TryMarkPurchasePaidAsync(purchase.Id, Trunc(result.TranzactionId, 100), Trunc(result.ApprovalNumber, 50), last4, brand,
            Trunc(result.DocumentNumber, 50), Trunc(result.DocumentUrl, 1000), cancelToken).ConfigureAwait(false);
        if (!won)
            return;

        _logger.LogInformation("AiStudio purchase {PurchaseId} paid: account {AccountId} +{Credits} credits, ₪{Total}, tx {Tx}, doc {Doc}.",
            purchase.Id, purchase.AccountId, purchase.Credits, purchase.Total, result.TranzactionId, result.DocumentNumber);

        var card = string.IsNullOrWhiteSpace(last4) ? null : $"כרטיס ****{last4}";
        var ledger = await _storage.AddLedgerEntryAsync(new AiCreditLedger
        {
            AccountId = purchase.AccountId,
            EntryType = AiCreditLedger.TypePurchase,
            Amount = purchase.Credits,
            RefPurchaseId = purchase.Id,
            Note = Trunc(string.Join(" · ", new[] { $"רכישת חבילה {purchase.PackageName}", $"₪{purchase.Total:0.00}", card }.Where(s => s != null)), 300),
            CreationUserId = purchase.CreationUserId,
        }, cancelToken).ConfigureAwait(false);
        await _storage.UpdatePurchaseAsync(purchase.Id, p => p.LedgerEntryId = ledger.Id, cancelToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(result.DocumentNumber))
            await TryIssueDocumentAsync(purchase, creds, result.TranzactionId, cancelToken).ConfigureAwait(false);
    }

    private async Task TryIssueDocumentAsync(AiCreditPurchase purchase, SitePaymentCredentials creds, string? transactionId, CancellationToken cancelToken)
    {
        if (string.IsNullOrWhiteSpace(creds.ApiPassword) || string.IsNullOrWhiteSpace(transactionId))
        {
            _logger.LogWarning("AiStudio purchase {PurchaseId}: no invoice (ApiPassword or transaction id missing).", purchase.Id);
            return;
        }
        try
        {
            var account = await _accountStorage.GetAccountAsync(purchase.AccountId, cancelToken).ConfigureAwait(false);
            if (account == null) return;
            var created = await _cardcom.CreateDocumentAsync(creds, new CreateCardcomDocumentRequest
            {
                Document = BuildPurchaseDocument(account, purchase, creds.DocumentTypeToCreate),
                TranzactionId = transactionId,
            }, cancelToken).ConfigureAwait(false);
            if (!created.Success)
            {
                _logger.LogWarning("AiStudio purchase {PurchaseId}: CreateDocument failed {Code} {Description}", purchase.Id, created.ResponseCode, created.Description);
                return;
            }
            await _storage.UpdatePurchaseAsync(purchase.Id, p =>
            {
                p.DocumentNumber = Trunc(created.DocumentNumber, 50);
                p.DocumentUrl = Trunc(created.DocumentUrl, 1000);
            }, cancelToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AiStudio purchase {PurchaseId}: CreateDocument threw.", purchase.Id);
        }
    }

    private static CardcomTransactionDocument BuildPurchaseDocument(Account account, AiCreditPurchase purchase, string documentType) => new()
    {
        DocumentTypeToCreate = documentType,
        Name = Trunc(account.Name, 100) ?? "לקוח",
        Email = string.IsNullOrWhiteSpace(account.ManagerEmail) ? null : account.ManagerEmail.Trim(),
        Phone = string.IsNullOrWhiteSpace(account.Phone) ? null : account.Phone.Trim(),
        AddressLine1 = Trunc(account.Address, 50),
        City = Trunc(account.City, 50),
        SendByEmail = !string.IsNullOrWhiteSpace(account.ManagerEmail),
        Comments = Trunc($"רכישת קרדיטים לסטודיו AI #{purchase.Id} - {account.Name}", 250),
        ExternalId = $"aicredits-{purchase.Id}",
        Language = "he",
        IsoCoinId = 1,
        Products = new[]
        {
            new CardcomDocumentProductLine
            {
                ProductId = $"AICR-{purchase.Credits}",
                Description = $"קרדיטים לסטודיו AI - {purchase.PackageName} (Giorgio)",
                Quantity = 1,
                UnitCost = purchase.Total, // gross: Cardcom document lines include VAT
            },
        },
    };

    //*************************    Super-admin    *************************//

    /// <summary>Super-admin: load (or correct) a shop's credits by hand. A negative amount corrects a mistake, never below zero.</summary>
    public async Task<IApiResponse<AiCreditsRes>> AllocateAsync(int accountId, AiCreditAllocateReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<AiCreditsRes>();
        if (!await IsUnrestrictedAsync(cancelToken).ConfigureAwait(false))
            return CreateResponse(response, StatusCode.UnauthorizedData, ErrAdminOnly);
        if (req.Amount == 0 || Math.Abs(req.Amount) > 1_000_000)
            return CreateResponse(response, StatusCode.InvalidRequest, "כמות לא חוקית");
        var account = await _accountStorage.GetAccountAsync(accountId, cancelToken).ConfigureAwait(false);
        if (account == null)
            return CreateResponse(response, StatusCode.ItemNotFound, ErrNoAccount);
        var balance = await _storage.GetBalanceAsync(accountId, cancelToken).ConfigureAwait(false);
        if (balance + req.Amount < 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "אי אפשר להוריד מתחת לאפס");

        await _storage.AddLedgerEntryAsync(new AiCreditLedger
        {
            AccountId = accountId,
            EntryType = AiCreditLedger.TypeAllocation,
            Amount = req.Amount,
            Note = Trunc(req.Note, 300),
            CreationUserId = AuthUser.Id > 0 ? AuthUser.Id : null,
        }, cancelToken).ConfigureAwait(false);
        _logger.LogInformation("AiStudio credits: admin {UserId} allocated {Amount} to account {AccountId} ({Note})", AuthUser.Id, req.Amount, accountId, req.Note);
        response.Data = await BuildCreditsAsync(account, includeHistory: true, cancelToken).ConfigureAwait(false);
        return response;
    }

    public async Task<IApiResponse<AiStudioAdminOverviewRes>> GetAdminOverviewAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancelToken)
    {
        var response = new ApiResponse<AiStudioAdminOverviewRes>();
        if (!await IsUnrestrictedAsync(cancelToken).ConfigureAwait(false))
            return CreateResponse(response, StatusCode.UnauthorizedData, ErrAdminOnly);
        var to = toUtc ?? DateTime.UtcNow;
        var from = fromUtc ?? new DateTime(to.Year, to.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        if (from >= to)
            return CreateResponse(response, StatusCode.InvalidRequest, "טווח תאריכים לא חוקי");
        if ((to - from).TotalDays > 400)
            return CreateResponse(response, StatusCode.InvalidRequest, "עד 13 חודשים");

        var rows = await _storage.GetAdminOverviewAsync(from, to, cancelToken).ConfigureAwait(false);
        var res = new AiStudioAdminOverviewRes
        {
            FromUtc = from,
            ToUtc = to,
            PurchaseConfigured = PlatformBilling.FromConfiguration(_configuration) != null,
            Accounts = rows.OrderByDescending(r => r.JobsInPeriod).ThenByDescending(r => r.JobsTotal).ThenBy(r => r.AccountName).Select(r => new AiStudioAdminAccountRes
            {
                AccountId = r.AccountId,
                AccountName = r.AccountName,
                IsActive = r.IsActive,
                AiStudioEnabled = r.AiStudioEnabled,
                Mode = r.Mode,
                DailyCap = r.DailyCap,
                PoolConsentAt = r.PoolConsentAt,
                JobsInPeriod = r.JobsInPeriod,
                DoneInPeriod = r.DoneInPeriod,
                FailedInPeriod = r.FailedInPeriod,
                SharedInPeriod = r.SharedInPeriod,
                JobsTotal = r.JobsTotal,
                ProviderCostUsdInPeriod = r.ProviderCostUsdInPeriod,
                ProviderCostUsdTotal = r.ProviderCostUsdTotal,
                AvgFidelityInPeriod = r.AvgFidelityInPeriod,
                ThumbsUpInPeriod = r.ThumbsUpInPeriod,
                ThumbsDownInPeriod = r.ThumbsDownInPeriod,
                LastJobAt = r.LastJobAt,
                CreditsConsumedInPeriod = r.CreditsConsumedInPeriod,
                CreditsBalance = r.CreditsBalance,
                PurchasesInPeriod = r.PurchasesInPeriod,
                RevenueInPeriod = r.RevenueInPeriod,
                PurchasesTotal = r.PurchasesTotal,
                RevenueTotal = r.RevenueTotal,
                LastPurchaseAt = r.LastPurchaseAt,
            }).ToList(),
        };
        res.JobsInPeriod = res.Accounts.Sum(a => a.JobsInPeriod);
        res.DoneInPeriod = res.Accounts.Sum(a => a.DoneInPeriod);
        res.FailedInPeriod = res.Accounts.Sum(a => a.FailedInPeriod);
        res.SharedInPeriod = res.Accounts.Sum(a => a.SharedInPeriod);
        res.ProviderCostUsdInPeriod = res.Accounts.Sum(a => a.ProviderCostUsdInPeriod);
        res.RevenueInPeriod = res.Accounts.Sum(a => a.RevenueInPeriod);
        res.PurchasesInPeriod = res.Accounts.Sum(a => a.PurchasesInPeriod);
        res.RevenueTotal = res.Accounts.Sum(a => a.RevenueTotal);
        res.ActiveAccounts = res.Accounts.Count(a => a.JobsInPeriod > 0);
        res.Providers = await BuildWalletsAsync(from, to, cancelToken).ConfigureAwait(false);
        response.Data = res;
        return response;
    }

    //*************************    Provider wallets    *************************//

    private const decimal DefaultAlertBelowUsd = 20m;

    private static readonly (string Key, string Label, string ConsoleUrl)[] KnownProviders =
    {
        (AiProviderSpend.ProviderOpenAi, "OpenAI", "https://platform.openai.com/settings/organization/billing/overview"),
        (AiProviderSpend.ProviderGemini, "Google Gemini", "https://aistudio.google.com/billing"),
        (AiProviderSpend.ProviderAnthropic, "Anthropic (Claude)", "https://platform.claude.com/settings/billing"),
    };

    private bool IsProviderConfigured(string provider)
    {
        var o = _aiOptions;
        return provider switch
        {
            AiProviderSpend.ProviderOpenAi => !string.IsNullOrWhiteSpace(o.OpenAI.ApiKey),
            AiProviderSpend.ProviderGemini => !string.IsNullOrWhiteSpace(o.Gemini.ApiKey),
            AiProviderSpend.ProviderAnthropic => !string.IsNullOrWhiteSpace(o.Anthropic.ApiKey) && o.UseVision,
            _ => false,
        };
    }

    private async Task<List<AiProviderWalletRes>> BuildWalletsAsync(DateTime from, DateTime to, CancellationToken cancelToken)
    {
        var rows = await _storage.GetProviderWalletsAsync(from, to, cancelToken).ConfigureAwait(false);
        var list = new List<AiProviderWalletRes>();
        foreach (var r in rows)
        {
            var known = KnownProviders.FirstOrDefault(k => string.Equals(k.Key, r.Provider, StringComparison.OrdinalIgnoreCase));
            var alert = r.LastTopUp?.AlertBelowUsd ?? DefaultAlertBelowUsd;
            decimal? remaining = r.LastTopUp != null ? r.LastTopUp.BalanceUsd - (r.SpentSinceTopUp ?? 0m) : null;
            var burn = Math.Round(r.SpentLast30Days / 30m, 4);
            int? daysLeft = remaining != null && burn > 0m ? (int)Math.Floor(Math.Max(0m, remaining.Value) / burn) : null;
            var level = remaining == null ? "unknown" : remaining <= 0m ? "empty" : remaining < alert || (daysLeft != null && daysLeft < 14) ? "low" : "ok";
            list.Add(new AiProviderWalletRes
            {
                Provider = r.Provider,
                Label = known.Label ?? r.Provider,
                ConsoleUrl = known.ConsoleUrl ?? string.Empty,
                Configured = IsProviderConfigured(r.Provider),
                SpentInPeriod = r.SpentInPeriod,
                CallsInPeriod = r.CallsInPeriod,
                SpentLast30Days = r.SpentLast30Days,
                SpentTotal = r.SpentTotal,
                LastCallAt = r.LastCallAt,
                BalanceEnteredUsd = r.LastTopUp?.BalanceUsd,
                BalanceEnteredAt = r.LastTopUp?.CreationTime,
                BalanceNote = r.LastTopUp?.Note,
                SpentSinceEntered = r.SpentSinceTopUp,
                RemainingEstimateUsd = remaining != null ? Math.Round(remaining.Value, 2) : null,
                AlertBelowUsd = alert,
                BurnPerDayUsd = burn,
                DaysLeft = daysLeft,
                Level = level,
            });
        }
        // configured providers first, then by spend
        return list.OrderByDescending(w => w.Configured).ThenByDescending(w => w.SpentTotal).ToList();
    }

    /// <summary>Super-admin writes down the balance seen in a provider console; the wallet estimate re-anchors on it.</summary>
    public async Task<IApiResponse<AiProviderWalletRes>> TopUpAsync(AiProviderTopUpReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<AiProviderWalletRes>();
        if (!await IsUnrestrictedAsync(cancelToken).ConfigureAwait(false))
            return CreateResponse(response, StatusCode.UnauthorizedData, ErrAdminOnly);
        var provider = (req.Provider ?? string.Empty).Trim().ToLowerInvariant();
        if (!KnownProviders.Any(k => k.Key == provider))
            return CreateResponse(response, StatusCode.InvalidRequest, "ספק לא מוכר");
        if (req.BalanceUsd < 0m || req.BalanceUsd > 1_000_000m)
            return CreateResponse(response, StatusCode.InvalidRequest, "יתרה לא חוקית");
        await _storage.AddTopUpAsync(new AiProviderTopUp
        {
            Provider = provider,
            BalanceUsd = Math.Round(req.BalanceUsd, 2),
            AlertBelowUsd = req.AlertBelowUsd is > 0m ? Math.Round(req.AlertBelowUsd.Value, 2) : null,
            Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim()[..Math.Min(300, req.Note.Trim().Length)],
            CreationUserId = AuthUser.Id > 0 ? AuthUser.Id : null,
        }, cancelToken).ConfigureAwait(false);
        _logger.LogInformation("AiStudio wallet: {Provider} balance set to {Balance} USD by user {UserId}", provider, req.BalanceUsd, AuthUser.Id);
        var now = DateTime.UtcNow;
        var wallets = await BuildWalletsAsync(new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc), now, cancelToken).ConfigureAwait(false);
        response.Data = wallets.First(w => w.Provider == provider);
        return response;
    }

    public async Task<IApiResponse<AiStudioAdminJobsRes>> GetAdminJobsAsync(int? accountId, string? status, DateTime? fromUtc, DateTime? toUtc, int skip, int take, CancellationToken cancelToken)
    {
        var response = new ApiResponse<AiStudioAdminJobsRes>();
        if (!await IsUnrestrictedAsync(cancelToken).ConfigureAwait(false))
            return CreateResponse(response, StatusCode.UnauthorizedData, ErrAdminOnly);
        take = take <= 0 ? 20 : Math.Min(take, 200);
        skip = Math.Max(0, skip);
        var page = await _storage.GetAdminJobsAsync(accountId, status, fromUtc, toUtc, skip, take, cancelToken).ConfigureAwait(false);
        response.Data = new AiStudioAdminJobsRes { Total = page.Total, Skip = skip, Take = take };
        response.Data.Items = page.Items.Select(r =>
        {
            var job = AiStudioService.ToRes(r.Job);
            var options = AiStudioService.ParseOptions(r.Job.OptionsJson);
            return new AiStudioAdminJobRes
            {
                Job = job,
                AccountName = r.AccountName,
                Model = r.Job.Model,
                InputTokens = r.Job.InputTokens,
                OutputTokens = r.Job.OutputTokens,
                ProviderCostUsd = r.Job.ProviderCostUsd,
                ProductName = options.ProductName ?? options.IdentifiedName,
                Error = r.Job.Error,
            };
        }).ToList();
        return response;
    }

    //*************************    Helpers    *************************//

    private static AiCreditPurchaseRes MapPurchase(AiCreditPurchase p) => new()
    {
        Id = p.Id,
        CreationTime = p.CreationTime,
        PackageId = p.PackageId,
        PackageName = p.PackageName,
        CreditsAmount = p.Credits,
        Price = p.Price,
        VatPercent = p.VatPercent,
        Total = p.Total,
        Currency = p.Currency,
        Status = p.Status,
        PaidAt = p.PaidAt,
        CardLast4 = p.CardLast4,
        CardBrand = p.CardBrand,
        DocumentNumber = p.DocumentNumber,
        DocumentUrl = p.DocumentUrl,
        ErrorDescription = p.ErrorDescription,
    };

    public static string PackageDisplayName(string? name, int credits) =>
        !string.IsNullOrWhiteSpace(name) ? name.Trim() : string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:N0} קרדיטים", credits);

    private static string ModeOf(Account a) =>
        string.Equals(a.AiStudioMode, Account.AiStudioModePaid, StringComparison.OrdinalIgnoreCase) ? Account.AiStudioModePaid : Account.AiStudioModeCommunity;

    private static string? Trunc(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().Length <= max ? s.Trim() : s.Trim()[..max];

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private sealed class Scope
    {
        public int AccountId { get; init; }
        public Account Account { get; init; } = null!;
        public bool Unrestricted { get; init; }
        public bool IsAccountAdmin { get; init; }
    }

    //*************************    Admin: looks / presets (phase 4)    *************************//

    /// <summary>One row per look: what the shops see, the code default behind it, and the period's quality numbers - the
    /// feedback loop for prompt tuning (judge scores, thumbs, the judge's recurring complaints).</summary>
    public async Task<IApiResponse<AiLooksAdminRes>> GetAdminLooksAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancelToken)
    {
        var response = new ApiResponse<AiLooksAdminRes>();
        if (!await IsUnrestrictedAsync(cancelToken).ConfigureAwait(false))
            return CreateResponse(response, StatusCode.UnauthorizedData, ErrAdminOnly);
        var to = toUtc ?? DateTime.UtcNow;
        var from = fromUtc ?? new DateTime(to.Year, to.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        if (from >= to)
            return CreateResponse(response, StatusCode.InvalidRequest, "טווח תאריכים לא חוקי");

        var presets = await _studioStorage.GetPresetsAsync(cancelToken).ConfigureAwait(false);
        AiStudioLooks.Apply(presets);
        var stats = (await _storage.GetLookStatsAsync(from, to, cancelToken).ConfigureAwait(false)).ToDictionary(s => s.Look, StringComparer.OrdinalIgnoreCase);
        var global = presets.Where(p => p.AccountId == null).ToDictionary(p => p.Look, StringComparer.OrdinalIgnoreCase);

        response.Data = new AiLooksAdminRes
        {
            FromUtc = from, ToUtc = to,
            DefaultProvider = _aiOptions.DefaultProvider,
            Providers = new List<string> { OpenAiImageProvider.ProviderKey, GeminiImageProvider.ProviderKey },
            HdCredits = _aiOptions.HdCredits,
            Looks = AiStudioLooks.All.Select(l =>
            {
                var d = AiStudioLooks.Defaults.First(x => x.Key == l.Key);
                global.TryGetValue(l.Key, out var p);
                stats.TryGetValue(l.Key, out var s);
                return new AiLookAdminRes
                {
                    Key = l.Key, NeedsSource = l.NeedsSource,
                    Label = l.Label, Description = l.Description, Prompt = l.Template, Provider = l.Provider, Credits = l.Credits, IsActive = l.IsActive, SortOrder = p?.SortOrder ?? 0,
                    DefaultLabel = d.Label, DefaultDescription = d.Description, DefaultPrompt = d.Template, DefaultCredits = d.Credits,
                    HasOverride = p != null, UpdatedAt = p?.UpdateTime,
                    Jobs = s?.Jobs ?? 0, Done = s?.Done ?? 0, Failed = s?.Failed ?? 0, Retries = s?.Retries ?? 0,
                    AvgFidelity = s?.AvgFidelity, AvgAesthetic = s?.AvgAesthetic, ThumbsUp = s?.Up ?? 0, ThumbsDown = s?.Down ?? 0,
                    CostUsd = s?.CostUsd ?? 0m, AvgPreviewMs = s?.AvgPreviewMs,
                    TopIssues = TopIssues(s?.IssueJson),
                };
            }).ToList(),
        };
        return response;
    }

    /// <summary>Save the global preset of one look and re-apply the overlay, so the next job and the next /Status see it.</summary>
    public async Task<IApiResponse<AiLooksAdminRes>> SaveAdminLookAsync(AiLookPresetSaveReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<AiLooksAdminRes>();
        if (!await IsUnrestrictedAsync(cancelToken).ConfigureAwait(false))
            return CreateResponse(response, StatusCode.UnauthorizedData, ErrAdminOnly);
        var look = AiStudioLooks.Defaults.FirstOrDefault(l => string.Equals(l.Key, req.Look?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (look == null)
            return CreateResponse(response, StatusCode.InvalidRequest, "מראה לא מוכר");
        var provider = string.IsNullOrWhiteSpace(req.Provider) ? null : req.Provider.Trim().ToLowerInvariant();
        if (provider != null && provider != OpenAiImageProvider.ProviderKey && provider != GeminiImageProvider.ProviderKey)
            return CreateResponse(response, StatusCode.InvalidRequest, "מנוע לא מוכר (openai | gemini)");
        if (req.Credits is < 0 or > 100)
            return CreateResponse(response, StatusCode.InvalidRequest, "מחיר בקרדיטים: 0 עד 100");
        var prompt = string.IsNullOrWhiteSpace(req.Prompt) || req.Prompt.Trim() == look.Template ? null : req.Prompt.Trim();
        if (prompt != null && look.NeedsSource && !prompt.Contains("{category}") && !prompt.Contains("{ratio}"))
            return CreateResponse(response, StatusCode.InvalidRequest, "הפרומפט חייב להכיל לפחות את {category} או {ratio}");
        if (prompt != null && prompt.Length > 4000)
            return CreateResponse(response, StatusCode.InvalidRequest, "פרומפט ארוך מדי (עד 4000 תווים)");

        await _studioStorage.SaveGlobalPresetAsync(new AiStylePreset
        {
            Look = look.Key,
            Label = string.Equals(req.Label?.Trim(), look.Label) ? null : req.Label,
            Description = string.Equals(req.Description?.Trim(), look.Description) ? null : req.Description,
            Prompt = prompt,
            Provider = provider,
            Credits = req.Credits == look.Credits ? null : req.Credits,
            IsActive = req.IsActive,
            SortOrder = req.SortOrder,
        }, AuthUser.Id > 0 ? AuthUser.Id : null, cancelToken).ConfigureAwait(false);
        _logger.LogInformation("AiStudio: look {Look} preset saved by user {UserId} (prompt override: {HasPrompt}, provider: {Provider}, active: {Active})", look.Key, AuthUser.Id, prompt != null, provider ?? "default", req.IsActive);
        return await GetAdminLooksAsync(null, null, cancelToken).ConfigureAwait(false);
    }

    /// <summary>The judge writes short English issue strings; the same complaint comes back in slightly different words, so group
    /// case-insensitively after trimming punctuation and keep the ten most frequent.</summary>
    private static List<AiLookIssueRes> TopIssues(List<string>? issueJson)
    {
        var counts = new Dictionary<string, (string Text, int Count)>(StringComparer.OrdinalIgnoreCase);
        foreach (var json in issueJson ?? new List<string>())
        {
            List<string> issues;
            try { issues = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
            catch { continue; }
            foreach (var raw in issues)
            {
                var text = raw.Trim().TrimEnd('.', ';', ',');
                if (text.Length < 4) continue;
                var key = text.ToLowerInvariant();
                counts[key] = counts.TryGetValue(key, out var c) ? (c.Text, c.Count + 1) : (text, 1);
            }
        }
        return counts.Values.OrderByDescending(c => c.Count).ThenBy(c => c.Text).Take(10).Select(c => new AiLookIssueRes { Text = c.Text, Count = c.Count }).ToList();
    }

    private async Task<bool> IsUnrestrictedAsync(CancellationToken cancelToken)
    {
        if (AuthUser.IsMaster) return true;
        var user = await _userStorage.GetThinUserAsync(AuthUser.Id, cancelToken).ConfigureAwait(false);
        return user?.RoleId == (int)UserRole.Admin;
    }

    /// <summary>The caller's own account, or any account for a master / system admin. The studio must be ON (credits are part of it).</summary>
    private async Task<(Scope? Scope, StatusCode Status, string? Error)> ResolveScopeAsync(int? requestedAccountId, CancellationToken cancelToken)
    {
        var user = await _userStorage.GetThinUserAsync(AuthUser.Id, cancelToken).ConfigureAwait(false);
        var unrestricted = AuthUser.IsMaster || user?.RoleId == (int)UserRole.Admin;
        var accountId = unrestricted && requestedAccountId is > 0 ? requestedAccountId : user?.AccountId;
        if (accountId is not > 0)
            return (null, StatusCode.InvalidRequest, ErrNoAccount);
        var account = await _accountStorage.GetAccountAsync(accountId.Value, cancelToken).ConfigureAwait(false);
        if (account == null)
            return (null, StatusCode.ItemNotFound, ErrNoAccount);
        if (!account.AiStudioEnabled && !unrestricted)
            return (null, StatusCode.UnauthorizedData, AiStudioService.ErrDisabled);
        var isAccountAdmin = unrestricted || user?.RoleId == (int)UserRole.AccountAdmin;
        return (new Scope { AccountId = accountId.Value, Account = account, Unrestricted = unrestricted, IsAccountAdmin = isAccountAdmin }, StatusCode.Ok, null);
    }
}
