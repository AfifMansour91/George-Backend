using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using George.DB;

namespace George.Services
{
    /// <summary>
    /// Server-side twin of the frontend customer sticker
    /// (shop-manager <c>OrderLabelPrint.tsx</c>: <c>buildCustomerLabelHtml</c> / <c>buildCustomerLabelWideHtml</c>).
    /// Used by the backend auto-print on new orders (Site.PrintCustomerLabelOnNewOrder) so the sticker
    /// prints even when nobody has the orders screen open. Keep the two in sync when the layout changes.
    /// </summary>
    public static class CustomerLabelHtml
    {
        private const int LabelPaperWidthMm = 58;
        private const int LabelPaperHeightMm = 40;
        private const int WideLabelWidthMm = 120;
        private const int WideLabelHeightMm = 80;
        private const int WideLabelBrandColumnMm = 47;

        private static readonly string[] HebrewWeekdayNames =
            { "ראשון", "שני", "שלישי", "רביעי", "חמישי", "שישי", "שבת" };

        public static string Build(Order order, bool wideFormat)
            => wideFormat ? BuildWide(order) : BuildStandard(order);

        /// <summary>58x40 sticker: order number, name, phone; full address only for Shipping orders.</summary>
        public static string BuildStandard(Order order)
        {
            var orderNum = Esc(OrderNumber(order));
            var contact = ResolveContact(order);
            var isShipping = IsShipping(order);
            var address = isShipping ? Esc(FirstNonEmpty(ShippingAddressOneLine(order), "-")) : "";

            return $@"<!DOCTYPE html>
<html dir=""rtl"" lang=""he"">
<head>
  <meta charset=""utf-8"" />
  <title>מדבקה {orderNum}</title>
  <style>
    @page {{ size: {LabelPaperWidthMm}mm {LabelPaperHeightMm}mm; margin: 0; }}
    html, body {{
      margin: 0; padding: 0; width: {LabelPaperWidthMm}mm; height: {LabelPaperHeightMm}mm;
      font-family: Arial, ""Segoe UI"", sans-serif; background: #fff; color: #111;
      -webkit-print-color-adjust: exact; print-color-adjust: exact;
    }}
    #label-root {{
      box-sizing: border-box; width: 100%; height: 100%;
      padding: 2mm; display: flex; flex-direction: column;
      justify-content: flex-start; gap: 1.5mm;
      text-align: right; line-height: 1.12;
    }}
    .order-num {{ font-size: 16pt; font-weight: 900; word-break: break-word; }}
    .customer {{ font-size: 14pt; font-weight: 800; word-break: break-word; }}
    .phone {{ font-size: 13pt; font-weight: 700; }}
    .phone bdi {{ unicode-bidi: plaintext; }}
    .address {{ font-size: 11.5pt; font-weight: 600; word-break: break-word; }}
    .orderer {{ font-size: 9pt; font-weight: 600; color: #333; word-break: break-word; }}
  </style>
</head>
<body>
  <div id=""label-root"">
    <div class=""order-num"">#{orderNum}</div>
    <div class=""customer"">{Esc(contact.Name)}</div>
    <div class=""phone""><bdi>{Esc(contact.Phone)}</bdi></div>
    {(address.Length > 0 ? $@"<div class=""address"">{address}</div>" : "")}
    {(contact.OrdererLine != null ? $@"<div class=""orderer"">{Esc(contact.OrdererLine)}</div>" : "")}
  </div>
</body>
</html>";
        }

        /// <summary>
        /// 120x80 pre-printed branded sticker (Site.CustomerLabelWideFormat, e.g. Zano Dagim): the branded
        /// right column stays empty; order info prints on the white left area only.
        /// </summary>
        public static string BuildWide(Order order)
        {
            var orderNum = Esc(OrderNumber(order));
            var contact = ResolveContact(order);
            var isShipping = IsShipping(order);
            var deliveryMethodLabel = isShipping ? "משלוח" : "איסוף עצמי";

            var street = Trim(order.DeliveryStreet);
            var city = Trim(order.DeliveryCity);
            var addressMain = street.Length > 0 || city.Length > 0
                ? string.Join(", ", new[] { street, city }.Where(s => s.Length > 0))
                : ShippingAddressOneLine(order);
            var addressExtras = new List<string>();
            if (Trim(order.DeliveryFloor).Length > 0) addressExtras.Add($"קומה: {Trim(order.DeliveryFloor)}");
            if (Trim(order.DeliveryApartment).Length > 0) addressExtras.Add($"דירה: {Trim(order.DeliveryApartment)}");
            if (Trim(order.DeliveryEntranceCode).Length > 0) addressExtras.Add($"קוד כניסה: {Trim(order.DeliveryEntranceCode)}");

            var fullAddressHtml = string.Join(" · ",
                new[] { addressMain }.Concat(addressExtras)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select((seg, i) => i == 0 ? Esc(seg) : $@"<span class=""seg"">{Esc(seg)}</span>"));

            var supplyDate = FormatSupplyDate(isShipping ? order.DeliveryDate : order.PickupDate);
            var pickupTime = !isShipping ? Trim(order.PickupTime) : "";
            var paymentLabel = ResolvePaymentLabel(order);

            var rows = new List<string>();
            if (contact.OrdererLine != null)
            {
                var orderer = contact.OrdererLine.StartsWith("מזמין: ", StringComparison.Ordinal)
                    ? contact.OrdererLine.Substring("מזמין: ".Length)
                    : contact.OrdererLine;
                rows.Add($@"<div class=""row""><span class=""row-label"">מזמין:</span> <span class=""row-value"">{Esc(orderer)}</span></div>");
            }
            if (isShipping && fullAddressHtml.Length > 0)
                rows.Add($@"<div class=""row row-address""><span class=""row-label"">כתובת:</span> <span class=""row-value"">{fullAddressHtml}</span></div>");
            if (supplyDate.Length > 0)
                rows.Add($@"<div class=""row""><span class=""row-label"">יום אספקה:</span> <span class=""row-value""><span class=""seg"">{Esc(supplyDate)}</span></span></div>");
            if (pickupTime.Length > 0)
                rows.Add($@"<div class=""row""><span class=""row-label"">שעת איסוף:</span> <span class=""row-value""><span class=""seg"">{Esc(pickupTime)}</span></span></div>");
            rows.Add($@"<div class=""row""><span class=""row-label"">שיטת תשלום:</span> <span class=""row-value"">{Esc(paymentLabel)}</span></div>");
            var infoRows = string.Join("\n      ", rows);

            return $@"<!DOCTYPE html>
<html dir=""rtl"" lang=""he"">
<head>
  <meta charset=""utf-8"" />
  <title>מדבקה {orderNum}</title>
  <style>
    @page {{ size: {WideLabelWidthMm}mm {WideLabelHeightMm}mm; margin: 0; }}
    html, body {{
      margin: 0; padding: 0; width: {WideLabelWidthMm}mm; height: {WideLabelHeightMm}mm;
      font-family: Arial, ""Segoe UI"", sans-serif; background: #fff; color: #111;
      -webkit-print-color-adjust: exact; print-color-adjust: exact;
    }}
    #label-root {{
      box-sizing: border-box; width: 100%; height: 100%;
      display: flex; flex-direction: row; align-items: stretch;
      text-align: right; line-height: 1.15;
    }}
    .brand-col {{ box-sizing: border-box; width: {WideLabelBrandColumnMm}mm; flex: 0 0 auto; }}
    .info-col {{
      box-sizing: border-box; flex: 1 1 auto; min-width: 0;
      padding: 0 17mm 2mm 1mm; display: flex; flex-direction: column; gap: 1.5mm;
      overflow: hidden;
    }}
    .order-line {{ line-height: 1; word-break: break-word; }}
    .order-num {{ font-size: 25pt; font-weight: 900; }}
    .order-delivery {{ font-size: 15pt; font-weight: 900; }}
    .customer-line {{ font-size: 28pt; font-weight: 800; word-break: break-word; }}
    .customer-line .phone {{ font-size: 14pt; font-weight: 800; white-space: nowrap; }}
    .customer-line bdi {{ unicode-bidi: plaintext; }}
    .row {{ font-size: 13pt; font-weight: 800; word-break: break-word; }}
    .row-label {{ font-weight: 800; }}
    .row-address .row-value {{ font-weight: 600; }}
    .row-value .seg {{ white-space: nowrap; }}
  </style>
</head>
<body>
  <div id=""label-root"">
    <div class=""brand-col""></div>
    <div class=""info-col"">
      <div class=""order-line""><span class=""order-num"">#{orderNum}</span> <span class=""order-delivery"">{Esc(deliveryMethodLabel)}</span></div>
      <div class=""customer-line"">{Esc(contact.Name)} <span class=""phone""><bdi>{Esc(contact.Phone)}</bdi></span></div>
      {infoRows}
    </div>
  </div>
</body>
</html>";
        }

        private sealed class LabelContact
        {
            public string Name = "-";
            public string Phone = "-";
            public string? OrdererLine;
        }

        /// <summary>
        /// Labels are delivery-facing: when the order was placed FOR someone else the sticker leads with the
        /// recipient (who the driver contacts) and the orderer moves to a secondary "מזמין:" line.
        /// </summary>
        private static LabelContact ResolveContact(Order order)
        {
            var recipientName = Trim(order.DeliveryRecipientName);
            if (recipientName.Length == 0)
            {
                return new LabelContact
                {
                    Name = FirstNonEmpty(Trim(order.CustomerName), "-"),
                    Phone = FirstNonEmpty(Trim(order.CustomerPhone), "-"),
                };
            }
            var orderer = string.Join(" ", new[] { Trim(order.CustomerName), Trim(order.CustomerPhone) }.Where(s => s.Length > 0));
            return new LabelContact
            {
                Name = recipientName,
                Phone = FirstNonEmpty(Trim(order.DeliveryRecipientPhone), "-"),
                OrdererLine = orderer.Length > 0 ? $"מזמין: {orderer}" : null,
            };
        }

        private static bool IsShipping(Order order)
            => string.Equals(Trim(order.DeliveryType), "Shipping", StringComparison.OrdinalIgnoreCase);

        /// <summary>Street + city (fallback: free-text address), then apartment / floor / entrance code.</summary>
        private static string ShippingAddressOneLine(Order order)
        {
            var street = Trim(order.DeliveryStreet);
            var city = Trim(order.DeliveryCity);
            var main = street.Length > 0 || city.Length > 0
                ? string.Join(", ", new[] { street, city }.Where(s => s.Length > 0))
                : Trim(order.DeliveryAddress);
            var parts = new[] { main, Trim(order.DeliveryApartment), Trim(order.DeliveryFloor), Trim(order.DeliveryEntranceCode) }
                .Where(s => s.Length > 0);
            return string.Join(", ", parts);
        }

        /// <summary>Mirror of the frontend wide-label payment text (specific cash-like method, else אשראי/מזומן).</summary>
        private static string ResolvePaymentLabel(Order order)
        {
            var method = Trim(order.PaymentMethod).ToLowerInvariant();
            switch (method)
            {
                case "onaccount": return "בהקפה";
                case "banktransfer": return "העברה בנקאית";
                case "externalcredit": return "אשראי חיצוני";
                case "cash":
                case "cod":
                case "cheque":
                case "check":
                    return "מזומן";
            }
            if (string.Equals(Trim(order.GatewayPaymentMethodCode), "cod", StringComparison.OrdinalIgnoreCase))
                return "מזומן";
            if (method.Contains("credit") || method.Contains("card") || method.Contains("cardcom") || method.Contains("payplus")
                || Trim(order.PaymentMethod).Contains("אשראי")
                || Trim(order.PaymentGateway).Length > 0
                || Trim(order.CardcomLowProfileId).Length > 0)
                return "אשראי";
            var label = Trim(order.PaymentLabel);
            if (label.Length > 0) return label;
            if (Trim(order.PaymentMethod).Contains("מזומן")) return "מזומן";
            return FirstNonEmpty(Trim(order.PaymentMethodTitle), "-");
        }

        /// <summary>"שלישי 10/09" - same shape as the frontend voucher supply-date line.</summary>
        private static string FormatSupplyDate(DateTime? date)
        {
            if (date == null) return "";
            var d = date.Value;
            return $"{HebrewWeekdayNames[(int)d.DayOfWeek]} {d.Day:00}/{d.Month:00}";
        }

        private static string OrderNumber(Order order)
            => FirstNonEmpty(Trim(order.OrderNumber), order.Id.ToString(CultureInfo.InvariantCulture));

        private static string Trim(string? s) => (s ?? "").Trim();

        private static string FirstNonEmpty(string value, string fallback)
            => string.IsNullOrWhiteSpace(value) ? fallback : value;

        private static string Esc(string s) => WebUtility.HtmlEncode(s ?? "");
    }
}
