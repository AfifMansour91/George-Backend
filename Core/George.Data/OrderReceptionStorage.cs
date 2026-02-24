using George.Common;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data;

public class OrderReceptionStorage : StorageBase
{
    public OrderReceptionStorage(GeorgeDBContext dbContext, ILogger<OrderReceptionStorage> logger)
        : base(dbContext, logger)
    {
    }

    public async Task<OrderReceptionData> GetForSiteAsync(int siteId, CancellationToken cancelToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var rows = await _dbContext.SiteOrderReceptionClosed
            .AsNoTracking()
            .Where(r => r.SiteId == siteId)
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);

        var res = new OrderReceptionData { SiteId = siteId };
        foreach (var r in rows)
        {
            var dateStr = r.ClosedDate.ToString("yyyy-MM-dd");
            if (r.ClosedDate == today)
            {
                if (r.Type == "Delivery") res.TodayDeliveryClosed = true;
                if (r.Type == "Pickup") res.TodayPickupClosed = true;
            }
            else if (r.ClosedDate > today)
            {
                if (r.Type == "Delivery") res.FutureDeliveryDates.Add(dateStr);
                if (r.Type == "Pickup") res.FuturePickupDates.Add(dateStr);
            }
        }
        res.FutureDeliveryDates = res.FutureDeliveryDates.OrderBy(x => x).ToList();
        res.FuturePickupDates = res.FuturePickupDates.OrderBy(x => x).ToList();
        return res;
    }

    public async Task SaveForSiteAsync(int siteId, OrderReceptionUpdateData req, CancellationToken cancelToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var existing = await _dbContext.SiteOrderReceptionClosed
            .Where(r => r.SiteId == siteId)
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);

        // Today toggles
        if (req.TodayDeliveryClosed.HasValue)
        {
            var hasTodayDelivery = existing.Any(r => r.ClosedDate == today && r.Type == "Delivery");
            if (req.TodayDeliveryClosed.Value && !hasTodayDelivery)
            {
                _dbContext.SiteOrderReceptionClosed.Add(new SiteOrderReceptionClosed { SiteId = siteId, ClosedDate = today, Type = "Delivery" });
            }
            else if (!req.TodayDeliveryClosed.Value && hasTodayDelivery)
            {
                var toRemove = existing.First(r => r.ClosedDate == today && r.Type == "Delivery");
                _dbContext.SiteOrderReceptionClosed.Remove(toRemove);
            }
        }
        if (req.TodayPickupClosed.HasValue)
        {
            var hasTodayPickup = existing.Any(r => r.ClosedDate == today && r.Type == "Pickup");
            if (req.TodayPickupClosed.Value && !hasTodayPickup)
            {
                _dbContext.SiteOrderReceptionClosed.Add(new SiteOrderReceptionClosed { SiteId = siteId, ClosedDate = today, Type = "Pickup" });
            }
            else if (!req.TodayPickupClosed.Value && hasTodayPickup)
            {
                var toRemove = existing.First(r => r.ClosedDate == today && r.Type == "Pickup");
                _dbContext.SiteOrderReceptionClosed.Remove(toRemove);
            }
        }

        // Future dates: replace all future rows for each type
        if (req.FutureDeliveryDates != null)
        {
            var toRemoveDelivery = existing.Where(r => r.ClosedDate > today && r.Type == "Delivery").ToList();
            foreach (var r in toRemoveDelivery) _dbContext.SiteOrderReceptionClosed.Remove(r);
            var futureDeliverySet = req.FutureDeliveryDates
                .Select(d => DateTime.TryParse(d, out var dt) ? dt.Date : (DateTime?)null)
                .Where(d => d.HasValue && d.Value > today)
                .Select(d => d!.Value)
                .Distinct()
                .ToList();
            foreach (var dateOnly in futureDeliverySet)
                _dbContext.SiteOrderReceptionClosed.Add(new SiteOrderReceptionClosed { SiteId = siteId, ClosedDate = dateOnly, Type = "Delivery" });
        }
        if (req.FuturePickupDates != null)
        {
            var toRemovePickup = existing.Where(r => r.ClosedDate > today && r.Type == "Pickup").ToList();
            foreach (var r in toRemovePickup) _dbContext.SiteOrderReceptionClosed.Remove(r);
            var futurePickupSet = req.FuturePickupDates
                .Select(d => DateTime.TryParse(d, out var dt) ? dt.Date : (DateTime?)null)
                .Where(d => d.HasValue && d.Value > today)
                .Select(d => d!.Value)
                .Distinct()
                .ToList();
            foreach (var dateOnly in futurePickupSet)
                _dbContext.SiteOrderReceptionClosed.Add(new SiteOrderReceptionClosed { SiteId = siteId, ClosedDate = dateOnly, Type = "Pickup" });
        }

        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
    }
}
