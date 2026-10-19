using George.Common;
using George.DB;
using George.Data;
using George.Services.Payments;
using George.Services.Payments.Cardcom;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Logging;

namespace George.Services.Marketing;

/// <summary>
/// Self-service SMS packages (מסלולי Giorgio): the shop picks a package, pays on the PLATFORM's Cardcom hosted page and
/// the messages land in its bank the moment Cardcom confirms - no super-admin in the loop. Confirmation arrives twice
/// (Cardcom's webhook and the shop's return page); <see cref="MarketingStorage.TryMarkPurchasePaidAsync"/> makes sure
/// only one of them credits the bank.
/// </summary>
public partial class MarketingService
{
    public const string ErrPurchaseUnavailable = "רכישה מקוונת עדיין לא פעילה - לטעינת הודעות פנה אלינו.";
    public const string ErrPurchaseAccountAdminOnly = "רק מנהל החשבון יכול לרכוש חבילות הודעות.";
    private const int MaxPurchasesListed = 100;

    //*************************    Catalog    *************************//

    public async Task<IApiResponse<MarketingPackagesRes>> GetPackagesAsync(int? accountId, bool includeInactive, CancellationToken cancelToken)
    {
        var response = new ApiResponse<MarketingPackagesRes>();
        var (scope, status, error) = await ResolveScopeAsync(accountId, null, cancelToken);
        if (scope == null)
            return CreateResponse(response, status, error);
        response.Data = await BuildPackagesAsync(includeInactive && scope.Unrestricted, cancelToken);
        return response;
    }

    /// <summary>Super-admin: the global catalog. Not scoped to an account (the super-admin's own account may have marketing OFF).</summary>
    public async Task<IApiResponse<MarketingPackagesRes>> GetPackagesForAdminAsync(CancellationToken cancelToken)
    {
        var response = new ApiResponse<MarketingPackagesRes>();
        if (!await IsUnrestrictedAsync(cancelToken))
            return CreateResponse(response, StatusCode.UnauthorizedData, "רק מנהל מערכת יכול לנהל את החבילות");
        response.Data = await BuildPackagesAsync(includeInactive: true, cancelToken);
        return response;
    }

    public async Task<IApiResponse<MarketingPackagesRes>> SavePackagesAsync(List<MarketingPackageSaveReq>? req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<MarketingPackagesRes>();
        if (!await IsUnrestrictedAsync(cancelToken))
            return CreateResponse(response, StatusCode.UnauthorizedData, "רק מנהל מערכת יכול לנהל את החבילות");
        var rows = req ?? new List<MarketingPackageSaveReq>();
        if (rows.Count > 20)
            return CreateResponse(response, StatusCode.InvalidRequest, "עד 20 חבילות");
        foreach (var r in rows)
        {
            if (r.Messages <= 0 || r.Messages > 10_000_000)
                return CreateResponse(response, StatusCode.InvalidRequest, "כמות הודעות לא חוקית");
            if (r.Price < 0 || r.Price > 1_000_000m)
                return CreateResponse(response, StatusCode.InvalidRequest, "מחיר לא חוקי");
        }
        var saved = await _storage.SavePackagesAsync(rows.Select(r => new MarketingPackage
        {
            Id = Math.Max(0, r.Id), Name = Truncate(r.Name?.Trim(), 100), Messages = r.Messages, Price = Math.Round(r.Price, 2, MidpointRounding.AwayFromZero),
            IsActive = r.IsActive, SortOrder = r.SortOrder, Badge = Truncate(r.Badge?.Trim(), 200),
        }).ToList(), AuthUser.Id > 0 ? AuthUser.Id : null, cancelToken);
        response.Data = BuildPackages(saved, includeInactive: true);
        return response;
    }

    private async Task<MarketingPackagesRes> BuildPackagesAsync(bool includeInactive, CancellationToken cancelToken) =>
        BuildPackages(await _storage.GetPackagesAsync(activeOnly: !includeInactive, cancelToken), includeInactive);

    private MarketingPackagesRes BuildPackages(List<MarketingPackage> packages, bool includeInactive)
    {
        var vat = PlatformBilling.VatPercent(_configuration);
        return new MarketingPackagesRes
        {
            VatPercent = vat,
            CanPurchase = PlatformBilling.FromConfiguration(_configuration) != null,
            PurchaseUrl = string.IsNullOrWhiteSpace(_configuration["Marketing:PurchaseUrl"]) ? null : _configuration["Marketing:PurchaseUrl"]!.Trim(),
            Packages = packages.Where(p => includeInactive || p.IsActive).Select(p => new MarketingPackageRes
            {
                Id = p.Id, Name = p.Name, Messages = p.Messages, Price = p.Price, VatPercent = vat,
                Total = PlatformBilling.TotalWithVat(p.Price, vat),
                PricePerMessageAgorot = PlatformBilling.PricePerMessageAgorot(p.Price, p.Messages),
                IsActive = p.IsActive, SortOrder = p.SortOrder, Badge = p.Badge,
            }).ToList(),
        };
    }

    //*************************    Super-admin overview    *************************//

    /// <summary>Super-admin: SMS volumes per account (operational / marketing), marketing bank and what each shop paid for packages.</summary>
    public async Task<IApiResponse<MarketingAdminOverviewRes>> GetAdminOverviewAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancelToken)
    {
        var response = new ApiResponse<MarketingAdminOverviewRes>();
        if (!await IsUnrestrictedAsync(cancelToken))
            return CreateResponse(response, StatusCode.UnauthorizedData, "רק מנהל מערכת יכול לראות את הסקירה");

        var to = toUtc ?? DateTime.UtcNow;
        var from = fromUtc ?? new DateTime(to.Year, to.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        if (from >= to)
            return CreateResponse(response, StatusCode.InvalidRequest, "טווח תאריכים לא חוקי");
        if ((to - from).TotalDays > 400)
            return CreateResponse(response, StatusCode.InvalidRequest, "עד 13 חודשים");

        var rows = await _storage.GetAdminOverviewAsync(from, to, cancelToken);
        var res = new MarketingAdminOverviewRes
        {
            FromUtc = from,
            ToUtc = to,
            PurchaseConfigured = PlatformBilling.FromConfiguration(_configuration) != null,
            Accounts = rows
                .OrderByDescending(r => r.OperationalUnits + r.MarketingUnits)
                .ThenBy(r => r.AccountName)
                .Select(r => new MarketingAdminAccountRes
                {
                    AccountId = r.AccountId,
                    AccountName = r.AccountName,
                    IsActive = r.IsActive,
                    MarketingEnabled = r.MarketingEnabled,
                    SmsProvider = r.SmsProvider,
                    SmsAccountKind = r.SmsProvider == null ? "system" : r.BilledByPlatform ? "inforu_platform" : "own",
                    OperationalMessages = r.OperationalMessages,
                    OperationalUnits = r.OperationalUnits,
                    MarketingMessages = r.MarketingMessages,
                    MarketingUnits = r.MarketingUnits,
                    SystemUnits = r.SystemUnits,
                    BankBalance = r.BankBalance,
                    BankPurchased = r.BankPurchased,
                    PurchasesInPeriod = r.PurchasesInPeriod,
                    RevenueInPeriod = r.RevenueInPeriod,
                    PurchasesTotal = r.PurchasesTotal,
                    RevenueTotal = r.RevenueTotal,
                    LastPurchaseAt = r.LastPurchaseAt,
                }).ToList(),
        };
        res.OperationalUnits = res.Accounts.Sum(a => a.OperationalUnits);
        res.MarketingUnits = res.Accounts.Sum(a => a.MarketingUnits);
        res.SystemUnits = res.Accounts.Sum(a => a.SystemUnits);
        res.PurchasesInPeriod = res.Accounts.Sum(a => a.PurchasesInPeriod);
        res.RevenueInPeriod = res.Accounts.Sum(a => a.RevenueInPeriod);
        res.RevenueTotal = res.Accounts.Sum(a => a.RevenueTotal);
        res.ActiveAccounts = res.Accounts.Count(a => a.TotalUnits > 0);
        response.Data = res;
        return response;
    }

    private async Task<bool> IsUnrestrictedAsync(CancellationToken cancelToken)
    {
        if (AuthUser.IsMaster) return true;
        var user = await _userStorage.GetThinUserAsync(AuthUser.Id, cancelToken).ConfigureAwait(false);
        return user?.RoleId == (int)UserRole.Admin;
    }


    //*************************    Purchase    *************************//

    /// <summary>Opens the hosted payment page for one package. The purchase row exists from this moment (status initiated).</summary>
    public async Task<IApiResponse<MarketingPurchaseStartRes>> StartPurchaseAsync(MarketingPurchaseStartReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<MarketingPurchaseStartRes>();
        var (scope, status, error) = await ResolveScopeAsync(req.AccountId, null, cancelToken);
        if (scope == null)
            return CreateResponse(response, status, error);
        if (!scope.IsAccountAdmin)
            return CreateResponse(response, StatusCode.UnauthorizedData, ErrPurchaseAccountAdminOnly);

        var creds = PlatformBilling.FromConfiguration(_configuration);
        if (creds == null)
            return CreateResponse(response, StatusCode.InvalidRequest, ErrPurchaseUnavailable);

        var smsConfig = await _accountSms.GetAccountConfigAsync(scope.AccountId, cancelToken);
        if (smsConfig != null && !smsConfig.BilledByPlatform)
            return CreateResponse(response, StatusCode.InvalidRequest, "החשבון שולח דרך חשבון SMS משלו, ולכן אין צורך לרכוש חבילה מג'ורג'יו.");

        var package = await _storage.GetPackageAsync(req.PackageId, cancelToken);
        if (package == null || !package.IsActive)
            return CreateResponse(response, StatusCode.InvalidRequest, "החבילה לא נמצאה או אינה זמינה יותר");
        var account = await _accountStorage.GetAccountAsync(scope.AccountId, cancelToken);
        if (account == null)
            return CreateResponse(response, StatusCode.ItemNotFound, ErrNoAccount);

        var vat = PlatformBilling.VatPercent(_configuration);
        var total = PlatformBilling.TotalWithVat(package.Price, vat);
        if (total <= 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "לחבילה אין מחיר - פנה אלינו");

        var purchase = await _storage.AddPurchaseAsync(new MarketingPurchase
        {
            AccountId = scope.AccountId,
            PackageId = package.Id,
            PackageName = PlatformBilling.PackageDisplayName(package.Name, package.Messages),
            Messages = package.Messages,
            Price = package.Price,
            VatPercent = vat,
            Total = total,
            Status = MarketingStorage.PurchaseStatus.Initiated,
            CreationUserId = AuthUser.Id > 0 ? AuthUser.Id : null,
        }, cancelToken);

        var appBase = PaymentService.ResolveReturnAppBase(_configuration["App:PublicBaseUrl"] ?? _configuration["PublicAppBaseUrl"] ?? _configuration["Client:BaseUrl"], req.AppOrigin);
        var apiBase = (_configuration["Payment:PublicApiBaseUrl"] ?? _configuration["App:ApiPublicBaseUrl"] ?? string.Empty).TrimEnd('/');
        var returnUrl = $"{appBase}/marketing/purchase/{purchase.Id}/return";

        var create = await _cardcom.CreateHostedSessionAsync(creds, new CreateHostedSessionRequest
        {
            OrderId = purchase.Id,
            Amount = total,
            ReturnValue = $"mktpkg:{purchase.Id}",
            ProductName = $"חבילת SMS {purchase.PackageName} - Giorgio",
            Language = "he",
            SaveCard = false,
            UseAuthorizationHold = false,
            MaxInstallments = creds.MaxInstallments,
            SuccessRedirectUrl = returnUrl + "?status=success",
            FailedRedirectUrl = returnUrl + "?status=failed",
            WebHookUrl = string.IsNullOrEmpty(apiBase) ? null : $"{apiBase}/Webhooks/Cardcom/Marketing",
            CustomerName = account.Name,
            CustomerPhone = account.Phone,
            CustomerEmail = account.ManagerEmail,
            // Tax invoice + receipt issued by Cardcom together with the charge, in the platform's name, to the shop.
            Document = BuildPurchaseDocument(account, purchase, creds.DocumentTypeToCreate),
        }, cancelToken);

        if (!create.Success || string.IsNullOrWhiteSpace(create.PaymentUrl))
        {
            _logger.LogWarning("Marketing package purchase {PurchaseId} for account {AccountId}: LowProfile/Create failed {Code} {Description}. {Raw}",
                purchase.Id, scope.AccountId, create.ErrorCode, create.ErrorDescription, create.RawJson);
            await _storage.UpdatePurchaseAsync(purchase.Id, p =>
            {
                p.Status = MarketingStorage.PurchaseStatus.Failed;
                p.ErrorDescription = Truncate($"פתיחת דף התשלום נכשלה: {create.ErrorDescription ?? create.ErrorCode}", 500);
            }, cancelToken);
            return CreateResponse(response, StatusCode.InvalidRequest, $"פתיחת דף התשלום נכשלה ({create.ErrorDescription ?? create.ErrorCode}). נסה שוב או פנה אלינו.");
        }

        await _storage.UpdatePurchaseAsync(purchase.Id, p => p.LowProfileId = create.LowProfileId, cancelToken);
        _logger.LogInformation("Marketing package purchase {PurchaseId}: account {AccountId} buys {Messages} messages for ₪{Total} (LowProfile {LowProfileId}).",
            purchase.Id, scope.AccountId, purchase.Messages, total, create.LowProfileId);

        response.Data = new MarketingPurchaseStartRes { PurchaseId = purchase.Id, PaymentUrl = create.PaymentUrl!, Total = total };
        return response;
    }

    /// <summary>The return page: settle (if the webhook has not yet) and answer with the purchase + the bank after it.</summary>
    public async Task<IApiResponse<MarketingPurchaseRes>> ConfirmPurchaseAsync(int purchaseId, int? accountId, CancellationToken cancelToken)
    {
        var response = new ApiResponse<MarketingPurchaseRes>();
        var (scope, status, error) = await ResolveScopeAsync(accountId, null, cancelToken);
        if (scope == null)
            return CreateResponse(response, status, error);
        var purchase = await _storage.GetPurchaseAsync(purchaseId, cancelToken);
        if (purchase == null || purchase.AccountId != scope.AccountId)
            return CreateResponse(response, StatusCode.ItemNotFound, "הרכישה לא נמצאה");

        await SettlePurchaseAsync(purchase, cancelToken);
        purchase = await _storage.GetPurchaseAsync(purchaseId, cancelToken) ?? purchase;
        var res = MapPurchase(purchase);
        res.Quota = await BuildQuotaAsync(scope.AccountId, includeLedger: false, cancelToken);
        response.Data = res;
        return response;
    }

    public async Task<IApiResponse<List<MarketingPurchaseRes>>> ListPurchasesAsync(int? accountId, CancellationToken cancelToken)
    {
        var response = new ApiResponse<List<MarketingPurchaseRes>>();
        var (scope, status, error) = await ResolveScopeAsync(accountId, null, cancelToken);
        if (scope == null)
            return CreateResponse(response, status, error);
        var rows = await _storage.ListPurchasesAsync(scope.AccountId, MaxPurchasesListed, cancelToken);
        response.Data = rows.Select(MapPurchase).ToList();
        return response;
    }

    /// <summary>Cardcom's server-to-server call for a package payment (WebHookUrl = /Webhooks/Cardcom/Marketing). No user context.</summary>
    public async Task ProcessCardcomWebhookAsync(string lowProfileId, CancellationToken cancelToken)
    {
        if (string.IsNullOrWhiteSpace(lowProfileId)) return;
        var purchase = await _storage.GetPurchaseByLowProfileIdAsync(lowProfileId.Trim(), cancelToken);
        if (purchase == null)
        {
            _logger.LogWarning("Marketing Cardcom webhook: no purchase for LowProfileId {Id}", lowProfileId);
            return;
        }
        await SettlePurchaseAsync(purchase, cancelToken);
    }

    /// <summary>
    /// Asks Cardcom what happened to the hosted session and applies it. Idempotent: a paid purchase only retries the
    /// provider top-up it may still owe; a failed one is left alone; a pending one (customer still on the page) is untouched.
    /// </summary>
    private async Task SettlePurchaseAsync(MarketingPurchase purchase, CancellationToken cancelToken)
    {
        if (purchase.Status == MarketingStorage.PurchaseStatus.Paid)
        {
            if (!string.IsNullOrWhiteSpace(purchase.ProviderSyncError))
                await TopUpProviderAsync(purchase, cancelToken);
            return;
        }
        if (purchase.Status != MarketingStorage.PurchaseStatus.Initiated || string.IsNullOrWhiteSpace(purchase.LowProfileId))
            return;

        var creds = PlatformBilling.FromConfiguration(_configuration);
        if (creds == null)
        {
            _logger.LogError("Marketing purchase {PurchaseId}: cannot settle - Billing:Cardcom is no longer configured.", purchase.Id);
            return;
        }

        ValidateCallbackResult result;
        try
        {
            result = await _cardcom.ValidateCallbackAsync(creds, new ValidateCallbackRequest { LowProfileId = purchase.LowProfileId }, cancelToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Marketing purchase {PurchaseId}: GetLpResult threw.", purchase.Id);
            return;
        }

        if (!result.Success)
        {
            if (result.IsPending)
                return;
            var reason = Truncate(string.IsNullOrWhiteSpace(result.Description) ? "התשלום לא הושלם" : result.Description, 500);
            if (await _storage.TryMarkPurchaseFailedAsync(purchase.Id, reason, cancelToken))
                _logger.LogInformation("Marketing purchase {PurchaseId} failed: {Code} {Description}", purchase.Id, result.ResponseCode, result.Description);
            return;
        }

        // The page was opened as ChargeOnly; a token-only answer would mean no money moved.
        if (!string.IsNullOrWhiteSpace(result.Operation) && result.Operation.Contains("CreateTokenOnly", StringComparison.OrdinalIgnoreCase))
        {
            await _storage.TryMarkPurchaseFailedAsync(purchase.Id, "לא בוצע חיוב (העסקה חזרה כשמירת כרטיס בלבד)", cancelToken);
            return;
        }
        if (result.Amount is decimal charged && Math.Abs(charged - purchase.Total) > 0.05m)
            _logger.LogWarning("Marketing purchase {PurchaseId}: charged ₪{Charged} but expected ₪{Expected}.", purchase.Id, charged, purchase.Total);

        var display = _cardcom.ExtractCardDisplayFields(result.RawJson);
        var last4 = Truncate(FirstNonEmpty(result.Last4Digits, display.Last4Digits), 4);
        var brand = Truncate(FirstNonEmpty(result.CardBrand, display.CardBrand), 50);
        var won = await _storage.TryMarkPurchasePaidAsync(purchase.Id, Truncate(result.TranzactionId, 100), Truncate(result.ApprovalNumber, 50), last4, brand,
            Truncate(result.DocumentNumber, 50), Truncate(result.DocumentUrl, 1000), cancelToken);
        if (!won)
            return; // the other caller (webhook / return page) got here first and is crediting

        _logger.LogInformation("Marketing purchase {PurchaseId} paid: account {AccountId} +{Messages} messages, ₪{Total}, tx {Tx}, doc {Doc}.",
            purchase.Id, purchase.AccountId, purchase.Messages, purchase.Total, result.TranzactionId, result.DocumentNumber);

        var card = string.IsNullOrWhiteSpace(last4) ? null : $"כרטיס ****{last4}";
        var ledger = new MarketingQuotaLedger
        {
            AccountId = purchase.AccountId,
            EntryType = MarketingStorage.LedgerEntryType.Purchase,
            Bucket = MarketingStorage.BucketBank,
            Amount = purchase.Messages,
            Note = Truncate(string.Join(" · ", new[] { $"רכישת חבילה {purchase.PackageName}", $"₪{purchase.Total:0.00}", card }.Where(s => s != null)), 300),
            CreationUserId = purchase.CreationUserId,
        };
        await _storage.AddLedgerEntryAsync(ledger, cancelToken);
        await _storage.UpdatePurchaseAsync(purchase.Id, p => p.LedgerEntryId = ledger.Id, cancelToken);

        await TopUpProviderAsync(purchase, cancelToken);

        if (string.IsNullOrWhiteSpace(result.DocumentNumber))
            await TryIssueDocumentAsync(purchase, creds, result.TranzactionId, cancelToken);
    }

    /// <summary>A platform-opened Inforu sub-account holds the real balance - push the messages there too. Failure is recorded, never fatal: the shop already paid.</summary>
    private async Task TopUpProviderAsync(MarketingPurchase purchase, CancellationToken cancelToken)
    {
        string? syncError = null;
        try
        {
            var config = await _accountSms.GetAccountConfigAsync(purchase.AccountId, cancelToken);
            if (config is { IsInforu: true, BilledByPlatform: true })
            {
                var (error, _) = await _inforuQuota.AddAsync(purchase.AccountId, config, purchase.Messages, cancelToken);
                syncError = error;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Marketing purchase {PurchaseId}: provider top-up threw.", purchase.Id);
            syncError = ex.Message;
        }
        if (syncError != null)
            _logger.LogWarning("Marketing purchase {PurchaseId}: paid but Inforu not topped up - {Error}", purchase.Id, syncError);
        await _storage.UpdatePurchaseAsync(purchase.Id, p => p.ProviderSyncError = Truncate(syncError, 500), cancelToken);
    }

    /// <summary>Cardcom did not issue the document with the charge - create it now, linked to the transaction (one attempt; a second one could duplicate).</summary>
    private async Task TryIssueDocumentAsync(MarketingPurchase purchase, SitePaymentCredentials creds, string? transactionId, CancellationToken cancelToken)
    {
        if (string.IsNullOrWhiteSpace(creds.ApiPassword) || string.IsNullOrWhiteSpace(transactionId))
        {
            _logger.LogWarning("Marketing purchase {PurchaseId}: no invoice (ApiPassword or transaction id missing).", purchase.Id);
            return;
        }
        try
        {
            var account = await _accountStorage.GetAccountAsync(purchase.AccountId, cancelToken);
            if (account == null) return;
            var created = await _cardcom.CreateDocumentAsync(creds, new CreateCardcomDocumentRequest
            {
                Document = BuildPurchaseDocument(account, purchase, creds.DocumentTypeToCreate),
                TranzactionId = transactionId,
            }, cancelToken);
            if (!created.Success)
            {
                _logger.LogWarning("Marketing purchase {PurchaseId}: CreateDocument failed {Code} {Description}", purchase.Id, created.ResponseCode, created.Description);
                return;
            }
            await _storage.UpdatePurchaseAsync(purchase.Id, p =>
            {
                p.DocumentNumber = Truncate(created.DocumentNumber, 50);
                p.DocumentUrl = Truncate(created.DocumentUrl, 1000);
            }, cancelToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Marketing purchase {PurchaseId}: CreateDocument threw.", purchase.Id);
        }
    }

    private static CardcomTransactionDocument BuildPurchaseDocument(Account account, MarketingPurchase purchase, string documentType) => new()
    {
        DocumentTypeToCreate = documentType,
        Name = Truncate(account.Name, 100) ?? "לקוח",
        Email = string.IsNullOrWhiteSpace(account.ManagerEmail) ? null : account.ManagerEmail.Trim(),
        Phone = string.IsNullOrWhiteSpace(account.Phone) ? null : account.Phone.Trim(),
        AddressLine1 = Truncate(account.Address?.Trim(), 50),
        City = Truncate(account.City?.Trim(), 50),
        SendByEmail = !string.IsNullOrWhiteSpace(account.ManagerEmail),
        Comments = Truncate($"רכישת חבילת SMS שיווקי #{purchase.Id} - {account.Name}", 250),
        ExternalId = $"mktpkg-{purchase.Id}",
        Language = "he",
        IsoCoinId = 1,
        Products = new[]
        {
            new CardcomDocumentProductLine
            {
                ProductId = $"SMS-{purchase.Messages}",
                Description = $"חבילת SMS שיווקי - {purchase.PackageName} (Giorgio)",
                Quantity = 1,
                UnitCost = purchase.Total, // gross: Cardcom document lines include VAT
            },
        },
    };

    private static MarketingPurchaseRes MapPurchase(MarketingPurchase p) => new()
    {
        Id = p.Id,
        CreationTime = p.CreationTime,
        PackageId = p.PackageId,
        PackageName = p.PackageName,
        Messages = p.Messages,
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
        ProviderSyncPending = p.Status == MarketingStorage.PurchaseStatus.Paid && !string.IsNullOrWhiteSpace(p.ProviderSyncError),
        ErrorDescription = p.ErrorDescription,
    };

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}
